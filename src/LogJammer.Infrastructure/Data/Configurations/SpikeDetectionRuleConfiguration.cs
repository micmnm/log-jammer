using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class SpikeDetectionRuleConfiguration : IEntityTypeConfiguration<SpikeDetectionRule>
{
    public void Configure(EntityTypeBuilder<SpikeDetectionRule> builder)
    {
        builder.ToTable("spike_detection_rules");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.KnownErrorId).HasColumnName("known_error_id");
        builder.Property(e => e.ThresholdType).HasColumnName("threshold_type").HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ThresholdValue).HasColumnName("threshold_value");
        builder.Property(e => e.WindowMinutes).HasColumnName("window_minutes");
        builder.Property(e => e.LookbackMinutes).HasColumnName("lookback_minutes");
        builder.Property(e => e.Enabled).HasColumnName("enabled");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.KnownErrorId).IsUnique();

        builder.HasOne(e => e.KnownError)
            .WithMany()
            .HasForeignKey(e => e.KnownErrorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
