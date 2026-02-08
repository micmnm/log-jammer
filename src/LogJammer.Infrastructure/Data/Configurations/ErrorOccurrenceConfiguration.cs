using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class ErrorOccurrenceConfiguration : IEntityTypeConfiguration<ErrorOccurrence>
{
    public void Configure(EntityTypeBuilder<ErrorOccurrence> builder)
    {
        builder.ToTable("error_occurrences");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.KnownErrorId).HasColumnName("known_error_id");
        builder.Property(e => e.WindowStart).HasColumnName("window_start");
        builder.Property(e => e.WindowEnd).HasColumnName("window_end");
        builder.Property(e => e.Count).HasColumnName("count");
        builder.Property(e => e.SampleRatio).HasColumnName("sample_ratio");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne(e => e.KnownError)
            .WithMany(k => k.Occurrences)
            .HasForeignKey(e => e.KnownErrorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.KnownErrorId, e.WindowStart });
    }
}
