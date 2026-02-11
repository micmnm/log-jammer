using FluentAssertions;
using LogJammer.Infrastructure.ML;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogJammer.Tests.Unit.ML;

public class OnnxEmbeddingProviderTests : IDisposable
{
    private readonly OnnxEmbeddingProvider _provider;

    public OnnxEmbeddingProviderTests()
    {
        var modelDir = Path.Combine(Path.GetTempPath(), "logjammer-test-models", "all-MiniLM-L6-v2");
        var downloader = new ModelDownloader(modelDir, NullLogger<ModelDownloader>.Instance);
        _provider = new OnnxEmbeddingProvider(downloader, NullLogger<OnnxEmbeddingProvider>.Instance);
    }

    [Fact]
    public void Dimensions_ShouldBe384()
    {
        _provider.Dimensions.Should().Be(384);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ShouldReturn384DimensionalVector()
    {
        var embedding = await _provider.GenerateEmbeddingAsync("NullReferenceException in UserService.GetUser");

        embedding.Should().HaveCount(384);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ShouldReturnNormalizedVector()
    {
        var embedding = await _provider.GenerateEmbeddingAsync("Connection timeout to database");

        var norm = Math.Sqrt(embedding.Sum(x => (double)x * x));
        norm.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_BatchShouldWork()
    {
        var texts = new List<string>
        {
            "NullReferenceException",
            "Connection timeout",
            "Out of memory"
        };

        var embeddings = await _provider.GenerateEmbeddingsAsync(texts);

        embeddings.Should().HaveCount(3);
        embeddings.All(e => e.Length == 384).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_EmptyListShouldReturnEmpty()
    {
        var embeddings = await _provider.GenerateEmbeddingsAsync([]);

        embeddings.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_EmptyTextShouldNotThrow()
    {
        var embedding = await _provider.GenerateEmbeddingAsync("");

        embedding.Should().HaveCount(384);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SimilarTextsShouldHaveHighSimilarity()
    {
        var emb1 = await _provider.GenerateEmbeddingAsync("NullReferenceException in UserService");
        var emb2 = await _provider.GenerateEmbeddingAsync("NullReferenceException in UserController");

        var similarity = CosineSimilarity(emb1, emb2);
        similarity.Should().BeGreaterThan(0.6, "similar error messages should have high similarity");
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_DifferentTextsShouldHaveLowerSimilarity()
    {
        var emb1 = await _provider.GenerateEmbeddingAsync("NullReferenceException in UserService");
        var emb2 = await _provider.GenerateEmbeddingAsync("Connection timeout to Redis cache");

        var similarity = CosineSimilarity(emb1, emb2);
        similarity.Should().BeLessThan(0.8, "different error types should have lower similarity");
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public void Dispose()
    {
        _provider.Dispose();
    }
}
