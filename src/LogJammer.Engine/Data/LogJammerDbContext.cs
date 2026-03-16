using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Engine.Data;

public class LogJammerDbContext(DbContextOptions<LogJammerDbContext> options) : DbContext(options)
{
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<DrainState> DrainStates => Set<DrainState>();
    public DbSet<LogPattern> LogPatterns => Set<LogPattern>();
    public DbSet<PatternOccurrence> PatternOccurrences => Set<PatternOccurrence>();
    public DbSet<PatternBaseline> PatternBaselines => Set<PatternBaseline>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataSource>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>();
        });

        modelBuilder.Entity<DrainState>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DataSourceId).IsUnique();
            e.HasOne(x => x.DataSource)
                .WithOne(x => x.DrainState)
                .HasForeignKey<DrainState>(x => x.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LogPattern>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Severity).HasConversion<string>();
            e.HasOne(x => x.DataSource)
                .WithMany(x => x.Patterns)
                .HasForeignKey(x => x.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PatternOccurrence>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PatternId, x.WindowStart }).IsUnique();
            e.HasOne(x => x.Pattern)
                .WithMany(x => x.Occurrences)
                .HasForeignKey(x => x.PatternId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PatternBaseline>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PatternId, x.HourOfWeek }).IsUnique();
            e.HasOne(x => x.Pattern)
                .WithMany(x => x.Baselines)
                .HasForeignKey(x => x.PatternId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
