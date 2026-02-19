using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("data_sources");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.AdapterType).HasColumnName("adapter_type").HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.ConnectionConfig).HasColumnName("connection_config").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.PollIntervalSeconds).HasColumnName("poll_interval_seconds");
        builder.Property(e => e.SchemaMapping).HasColumnName("schema_mapping").HasColumnType("jsonb");
        builder.Property(e => e.SamplingBudget).HasColumnName("sampling_budget");
        builder.Property(e => e.Enabled).HasColumnName("enabled");
        builder.Property(e => e.LastIngestAt).HasColumnName("last_ingest_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}
