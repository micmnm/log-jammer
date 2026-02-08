using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class ClassificationQueueItemConfiguration : IEntityTypeConfiguration<ClassificationQueueItem>
{
    public void Configure(EntityTypeBuilder<ClassificationQueueItem> builder)
    {
        builder.ToTable("classification_queue");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.KnownErrorId).HasColumnName("known_error_id");
        builder.Property(e => e.SuggestedTags).HasColumnName("suggested_tags").HasColumnType("jsonb");
        builder.Property(e => e.Confidence).HasColumnName("confidence");
        builder.Property(e => e.Reviewed).HasColumnName("reviewed");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");

        builder.HasOne(e => e.KnownError)
            .WithMany()
            .HasForeignKey(e => e.KnownErrorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.Reviewed).HasFilter("reviewed = false");
    }
}
