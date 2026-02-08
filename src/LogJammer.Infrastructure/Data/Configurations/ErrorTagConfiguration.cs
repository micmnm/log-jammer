using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class ErrorTagConfiguration : IEntityTypeConfiguration<ErrorTag>
{
    public void Configure(EntityTypeBuilder<ErrorTag> builder)
    {
        builder.ToTable("error_tags");

        builder.HasKey(e => new { e.KnownErrorId, e.TagId });
        builder.Property(e => e.KnownErrorId).HasColumnName("known_error_id");
        builder.Property(e => e.TagId).HasColumnName("tag_id");
        builder.Property(e => e.IsAutoAssigned).HasColumnName("is_auto_assigned");
        builder.Property(e => e.Confidence).HasColumnName("confidence");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne(e => e.KnownError)
            .WithMany(k => k.ErrorTags)
            .HasForeignKey(e => e.KnownErrorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Tag)
            .WithMany(t => t.ErrorTags)
            .HasForeignKey(e => e.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
