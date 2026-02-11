using BERTTokenizers;
using LogJammer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace LogJammer.Infrastructure.ML;

public class OnnxEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private readonly ModelDownloader _downloader;
    private readonly ILogger<OnnxEmbeddingProvider> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private InferenceSession? _session;
    private BertUncasedBaseTokenizer? _tokenizer;
    private bool _initialized;
    private bool _disposed;

    public int Dimensions => 384;

    public OnnxEmbeddingProvider(ModelDownloader downloader, ILogger<OnnxEmbeddingProvider> logger)
    {
        _downloader = downloader;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await GenerateEmbeddingsAsync([text], cancellationToken);
        return results[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return [];

        await EnsureInitializedAsync(cancellationToken);

        var results = new List<float[]>(texts.Count);
        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var embedding = RunInference(text);
            results.Add(embedding);
        }

        return results;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            await _downloader.EnsureModelDownloadedAsync(cancellationToken);

            _logger.LogInformation("Loading ONNX model from {Path}", _downloader.ModelPath);
            var sessionOptions = new SessionOptions();
            _session = new InferenceSession(_downloader.ModelPath, sessionOptions);
            _tokenizer = new BertUncasedBaseTokenizer();
            _initialized = true;
            _logger.LogInformation("ONNX model loaded successfully");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private float[] RunInference(string text)
    {
        if (_session is null || _tokenizer is null)
            throw new InvalidOperationException("Model not initialized");

        var safeText = string.IsNullOrWhiteSpace(text) ? "[UNK]" : text;

        // Tokenize: BertBaseUncasedTokenizer.Encode returns List<(long InputIds, long TokenTypeIds, long AttentionMask)>
        const int maxSequenceLength = 256;
        var encoded = _tokenizer.Encode(maxSequenceLength, safeText);

        var inputIds = encoded.Select(t => t.InputIds).ToArray();
        var attentionMask = encoded.Select(t => t.AttentionMask).ToArray();
        var tokenTypeIds = encoded.Select(t => t.TokenTypeIds).ToArray();

        var seqLen = inputIds.Length;

        var inputIdsTensor = new DenseTensor<long>(inputIds, [1, seqLen]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, seqLen]);
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, [1, seqLen]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        using var results = _session.Run(inputs);

        // last_hidden_state shape: [1, seq_len, 384]
        var lastHiddenState = results.First().AsTensor<float>();

        // Mean pooling with attention mask
        var embedding = MeanPool(lastHiddenState, attentionMask, seqLen);

        // L2 normalize
        Normalize(embedding);

        return embedding;
    }

    private float[] MeanPool(Tensor<float> hiddenState, long[] attentionMask, int seqLen)
    {
        var dims = Dimensions;
        var pooled = new float[dims];
        float maskSum = 0;

        for (int i = 0; i < seqLen; i++)
        {
            if (attentionMask[i] == 0) continue;
            maskSum += 1;
            for (int j = 0; j < dims; j++)
            {
                pooled[j] += hiddenState[0, i, j];
            }
        }

        if (maskSum > 0)
        {
            for (int j = 0; j < dims; j++)
                pooled[j] /= maskSum;
        }

        return pooled;
    }

    private static void Normalize(float[] vector)
    {
        double norm = 0;
        foreach (var v in vector)
            norm += v * v;
        norm = Math.Sqrt(norm);

        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
                vector[i] = (float)(vector[i] / norm);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session?.Dispose();
        _initLock.Dispose();
    }
}
