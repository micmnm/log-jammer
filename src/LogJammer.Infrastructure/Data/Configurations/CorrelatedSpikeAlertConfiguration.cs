using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class CorrelatedSpikeAlertConfiguration : IEntityTypeConfiguration<CorrelatedSpikeAlert>
{
    public void Configure(EntityTypeBuilder<CorrelatedSpikeAlert> builder)
    {
        builder.ToTable("correlated_spike_alerts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.DataSourceId).HasColumnName("data_source_id");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.AlertIds).HasColumnName("alert_ids");
        builder.Property(e => e.GroupCount).HasColumnName("group_count");
        builder.Property(e => e.DetectedAt).HasColumnName("detected_at");
        builder.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.DataSource)
            .WithMany()
            .HasForeignKey(e => e.DataSourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
