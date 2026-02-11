using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.ML;

public class ModelDownloader
{
    private const string ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";
    private const string VocabUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt";

    private readonly string _modelDir;
    private readonly ILogger _logger;

    public ModelDownloader(string modelDir, ILogger logger)
    {
        _modelDir = modelDir;
        _logger = logger;
    }

    public string ModelPath => Path.Combine(_modelDir, "model.onnx");
    public string VocabPath => Path.Combine(_modelDir, "vocab.txt");

    public async Task EnsureModelDownloadedAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(ModelPath) && File.Exists(VocabPath))
        {
            _logger.LogDebug("Model files already exist at {ModelDir}", _modelDir);
            return;
        }

        Directory.CreateDirectory(_modelDir);

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        if (!File.Exists(ModelPath))
        {
            _logger.LogInformation("Downloading ONNX model to {Path}...", ModelPath);
            await DownloadFileAsync(httpClient, ModelUrl, ModelPath, cancellationToken);
            _logger.LogInformation("Model download complete");
        }

        if (!File.Exists(VocabPath))
        {
            _logger.LogInformation("Downloading vocab file to {Path}...", VocabPath);
            await DownloadFileAsync(httpClient, VocabUrl, VocabPath, cancellationToken);
            _logger.LogInformation("Vocab download complete");
        }
    }

    private static async Task DownloadFileAsync(HttpClient client, string url, string destination, CancellationToken ct)
    {
        var tempPath = destination + ".tmp";
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream, ct);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }

        File.Move(tempPath, destination);
    }
}
