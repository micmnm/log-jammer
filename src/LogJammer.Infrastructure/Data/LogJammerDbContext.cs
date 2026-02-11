using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Data;

public class LogJammerDbContext : DbContext
{
    public LogJammerDbContext(DbContextOptions<LogJammerDbContext> options) : base(options) { }

    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<FingerprintConfig> FingerprintConfigs => Set<FingerprintConfig>();
    public DbSet<KnownError> KnownErrors => Set<KnownError>();
    public DbSet<ErrorOccurrence> ErrorOccurrences => Set<ErrorOccurrence>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ErrorTag> ErrorTags => Set<ErrorTag>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<SpikeDetectionRule> SpikeDetectionRules => Set<SpikeDetectionRule>();
    public DbSet<CorrelatedSpikeAlert> CorrelatedSpikeAlerts => Set<CorrelatedSpikeAlert>();
    public DbSet<UserOverride> UserOverrides => Set<UserOverride>();
    public DbSet<ClassificationQueueItem> ClassificationQueue => Set<ClassificationQueueItem>();
    public DbSet<ClassificationConfig> ClassificationConfigs => Set<ClassificationConfig>();
    public DbSet<TagCentroid> TagCentroids => Set<TagCentroid>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LogJammerDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Properties.Any(p => p.Metadata.Name == "CreatedAt"))
                    entry.Property("CreatedAt").CurrentValue = now;
                if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                    entry.Property("UpdatedAt").CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                    entry.Property("UpdatedAt").CurrentValue = now;
            }
        }
    }
}
