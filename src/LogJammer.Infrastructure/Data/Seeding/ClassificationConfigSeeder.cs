using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Data.Seeding;

public static class ClassificationConfigSeeder
{
    private static readonly (string Key, string Value, string Description)[] Defaults =
    [
        ("SimilarityThreshold", "0.85", "Minimum cosine similarity to consider two errors as duplicates"),
        ("AutoTagConfidenceThreshold", "0.7", "Minimum confidence score to auto-assign a tag"),
        ("MaxSuggestedTags", "3", "Maximum number of tags to suggest per error"),
        ("IngestionSimilarityThreshold", "0.80", "Cosine similarity threshold for embedding-based grouping at ingestion time"),
        ("IngestionSimilarityEnabled", "true", "Enable/disable embedding-based similarity search during ingestion")
    ];

    public static async Task SeedAsync(LogJammerDbContext context, ILogger logger)
    {
        foreach (var (key, value, description) in Defaults)
        {
            if (await context.ClassificationConfigs.AnyAsync(c => c.Key == key))
                continue;

            context.ClassificationConfigs.Add(new ClassificationConfig
            {
                Key = key,
                Value = value,
                Description = description
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded classification config defaults");
        }
    }
}
