using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class FingerprintConfigConfiguration : IEntityTypeConfiguration<FingerprintConfig>
{
    public void Configure(EntityTypeBuilder<FingerprintConfig> builder)
    {
        builder.ToTable("fingerprint_configs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.DataSourceId).HasColumnName("data_source_id");
        builder.Property(e => e.FieldName).HasColumnName("field_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Order).HasColumnName("order");
        builder.Property(e => e.NormalizeBeforeHash).HasColumnName("normalize_before_hash");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne(e => e.DataSource)
            .WithMany(d => d.FingerprintConfigs)
            .HasForeignKey(e => e.DataSourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
