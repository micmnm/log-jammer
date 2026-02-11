using System.Text.Json;
using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace LogJammer.Infrastructure.ML;

public class ClassificationService(
    LogJammerDbContext context,
    IEmbeddingProvider embeddingProvider,
    IClassificationConfigRepository configRepo,
    IUserOverrideRepository overrideRepo,
    ILogger<ClassificationService> logger) : IClassificationService
{
    public async Task<ClassificationResult> ClassifyAsync(KnownError error, CancellationToken ct = default)
    {
        // Check for pinned classification override
        var classificationOverride = await overrideRepo.GetByKnownErrorAndTypeAsync(error.Id, "classification", ct);
        if (classificationOverride is not null)
        {
            logger.LogDebug("Skipping classification for error {ErrorId} — has pinned override", error.Id);
            var pinnedTags = JsonSerializer.Deserialize<List<Guid>>(classificationOverride.OverrideData) ?? [];
            var tagSuggestions = new List<TagSuggestion>();
            foreach (var tagId in pinnedTags)
            {
                var tag = await context.Tags.FirstOrDefaultAsync(t => t.Id == tagId, ct);
                if (tag is not null)
                    tagSuggestions.Add(new TagSuggestion(tag.Id, tag.Name, 1.0));
            }
            return new ClassificationResult(null, 0, tagSuggestions, false);
        }

        // Generate embedding
        var text = error.RepresentativeMessage;
        if (!string.IsNullOrWhiteSpace(error.RepresentativeStackTrace))
            text += " " + error.RepresentativeStackTrace;

        var embedding = await embeddingProvider.GenerateEmbeddingAsync(text, ct);
        var vector = new Vector(embedding);

        // Store embedding on the KnownError
        error.EmbeddingVector = vector;
        context.KnownErrors.Update(error);
        await context.SaveChangesAsync(ct);

        // Load thresholds
        var similarityThreshold = await GetConfigDoubleAsync("SimilarityThreshold", 0.85, ct);
        var autoTagThreshold = await GetConfigDoubleAsync("AutoTagConfidenceThreshold", 0.7, ct);
        var maxSuggestedTags = (int)await GetConfigDoubleAsync("MaxSuggestedTags", 3, ct);

        // Nearest-neighbor search against other known errors (exclude self)
        var neighbors = await context.KnownErrors
            .Where(e => e.Id != error.Id && e.EmbeddingVector != null)
            .OrderBy(e => e.EmbeddingVector!.CosineDistance(vector))
            .Take(5)
            .Select(e => new { e.Id, Distance = e.EmbeddingVector!.CosineDistance(vector) })
            .ToListAsync(ct);

        Guid? matchedGroupId = null;
        double bestSimilarity = 0;

        if (neighbors.Count > 0)
        {
            var topMatch = neighbors[0];
            bestSimilarity = 1.0 - topMatch.Distance; // cosine distance to similarity
            if (bestSimilarity >= similarityThreshold)
                matchedGroupId = topMatch.Id;
        }

        // Tag centroid matching
        var centroids = await context.TagCentroids
            .Include(tc => tc.Tag)
            .Where(tc => tc.CentroidVector != null)
            .ToListAsync(ct);

        var suggestions = new List<TagSuggestion>();
        foreach (var centroid in centroids)
        {
            var distance = CosineSimilarity(embedding, centroid.CentroidVector!.ToArray());
            if (distance >= autoTagThreshold)
                suggestions.Add(new TagSuggestion(centroid.TagId, centroid.Tag.Name, distance));
        }

        suggestions = suggestions
            .OrderByDescending(s => s.Confidence)
            .Take(maxSuggestedTags)
            .ToList();

        var needsReview = suggestions.Count == 0 || suggestions.All(s => s.Confidence < autoTagThreshold);

        return new ClassificationResult(matchedGroupId, bestSimilarity, suggestions, needsReview);
    }

    public async Task RecalculateTagCentroidAsync(Guid tagId, CancellationToken ct = default)
    {
        var errorIds = await context.ErrorTags
            .Where(et => et.TagId == tagId)
            .Select(et => et.KnownErrorId)
            .ToListAsync(ct);

        var embeddings = await context.KnownErrors
            .Where(e => errorIds.Contains(e.Id) && e.EmbeddingVector != null)
            .Select(e => e.EmbeddingVector!)
            .ToListAsync(ct);

        var centroid = await context.TagCentroids
            .FirstOrDefaultAsync(tc => tc.TagId == tagId, ct);

        if (embeddings.Count == 0)
        {
            if (centroid is not null)
            {
                context.TagCentroids.Remove(centroid);
                await context.SaveChangesAsync(ct);
            }
            return;
        }

        var avgVector = ComputeCentroid(embeddings);

        if (centroid is null)
        {
            centroid = new TagCentroid
            {
                TagId = tagId,
                CentroidVector = new Vector(avgVector),
                ErrorCount = embeddings.Count
            };
            context.TagCentroids.Add(centroid);
        }
        else
        {
            centroid.CentroidVector = new Vector(avgVector);
            centroid.ErrorCount = embeddings.Count;
            context.TagCentroids.Update(centroid);
        }

        await context.SaveChangesAsync(ct);
        logger.LogDebug("Recalculated centroid for tag {TagId} from {Count} errors", tagId, embeddings.Count);
    }

    public async Task RecalculateAllCentroidsAsync(CancellationToken ct = default)
    {
        var tagIds = await context.Tags.Select(t => t.Id).ToListAsync(ct);
        foreach (var tagId in tagIds)
        {
            await RecalculateTagCentroidAsync(tagId, ct);
        }
    }

    private async Task<double> GetConfigDoubleAsync(string key, double defaultValue, CancellationToken ct)
    {
        var config = await configRepo.GetAsync(key, ct);
        return config is not null && double.TryParse(config.Value, out var val) ? val : defaultValue;
    }

    private static float[] ComputeCentroid(List<Vector> embeddings)
    {
        var dims = embeddings[0].ToArray().Length;
        var sum = new float[dims];

        foreach (var emb in embeddings)
        {
            var arr = emb.ToArray();
            for (int i = 0; i < dims; i++)
                sum[i] += arr[i];
        }

        for (int i = 0; i < dims; i++)
            sum[i] /= embeddings.Count;

        // Normalize
        double norm = 0;
        foreach (var v in sum)
            norm += v * v;
        norm = Math.Sqrt(norm);

        if (norm > 0)
        {
            for (int i = 0; i < sum.Length; i++)
                sum[i] = (float)(sum[i] / norm);
        }

        return sum;
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

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom > 0 ? dot / denom : 0;
    }
}
