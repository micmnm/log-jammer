using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class KnownErrorConfiguration : IEntityTypeConfiguration<KnownError>
{
    public void Configure(EntityTypeBuilder<KnownError> builder)
    {
        builder.ToTable("known_errors");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.FingerprintHash).HasColumnName("fingerprint_hash").HasMaxLength(64).IsRequired();
        builder.Property(e => e.RepresentativeMessage).HasColumnName("representative_message").IsRequired();
        builder.Property(e => e.RepresentativeStackTrace).HasColumnName("representative_stack_trace");
        builder.Property(e => e.EmbeddingVector).HasColumnName("embedding_vector").HasColumnType("vector(384)");
        builder.Property(e => e.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.FirstSeen).HasColumnName("first_seen");
        builder.Property(e => e.LastSeen).HasColumnName("last_seen");
        builder.Property(e => e.TotalOccurrences).HasColumnName("total_occurrences");
        builder.Property(e => e.OccurrenceWindows).HasColumnName("occurrence_windows").HasColumnType("jsonb");
        builder.Property(e => e.DataSourceId).HasColumnName("data_source_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.FingerprintHash).IsUnique();

        builder.HasOne(e => e.DataSource)
            .WithMany(d => d.KnownErrors)
            .HasForeignKey(e => e.DataSourceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
