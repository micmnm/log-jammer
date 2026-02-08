using System.Text.Json;
using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Data.Seeding;

public static class TagSeeder
{
    public static async Task SeedAsync(LogJammerDbContext context, ILogger logger)
    {
        if (await context.Tags.AnyAsync())
        {
            logger.LogDebug("Tags already seeded, skipping");
            return;
        }

        var assembly = typeof(TagSeeder).Assembly;
        var resourceName = "LogJammer.Infrastructure.Data.Seeding.default-tags.json";

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            logger.LogWarning("Could not find embedded resource {Resource}", resourceName);
            return;
        }

        var tagDefs = await JsonSerializer.DeserializeAsync<List<TagSeedEntry>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (tagDefs is null || tagDefs.Count == 0)
        {
            logger.LogWarning("No tags found in seed file");
            return;
        }

        foreach (var def in tagDefs)
        {
            context.Tags.Add(new Tag
            {
                Name = def.Name,
                TagType = def.TagType,
                Color = def.Color
            });
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} default tags", tagDefs.Count);
    }

    private record TagSeedEntry(string Name, string TagType, string Color);
}
