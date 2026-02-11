using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.KnownErrorId).HasColumnName("known_error_id");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ThresholdType).HasColumnName("threshold_type").HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ThresholdValue).HasColumnName("threshold_value");
        builder.Property(e => e.ActualValue).HasColumnName("actual_value");
        builder.Property(e => e.NotificationCount).HasColumnName("notification_count");
        builder.Property(e => e.LastNotifiedAt).HasColumnName("last_notified_at");
        builder.Property(e => e.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(e => e.ConsecutiveBelowThreshold).HasColumnName("consecutive_below_threshold");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.KnownError)
            .WithMany(k => k.Alerts)
            .HasForeignKey(e => e.KnownErrorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
