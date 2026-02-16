using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Data.Seeding;

public static class SpikeDetectionRuleSeeder
{
    public static async Task SeedAsync(LogJammerDbContext context, ILogger logger)
    {
        var hasGlobalDefault = await context.SpikeDetectionRules
            .AnyAsync(r => r.KnownErrorId == null);

        if (hasGlobalDefault) return;

        context.SpikeDetectionRules.Add(new SpikeDetectionRule
        {
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 100,
            WindowMinutes = 5,
            LookbackMinutes = 1440,
            Enabled = true
        });

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded global default spike detection rule");
    }
}
