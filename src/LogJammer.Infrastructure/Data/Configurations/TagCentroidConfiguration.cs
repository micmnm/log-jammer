using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class TagCentroidConfiguration : IEntityTypeConfiguration<TagCentroid>
{
    public void Configure(EntityTypeBuilder<TagCentroid> builder)
    {
        builder.ToTable("tag_centroids");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.TagId).HasColumnName("tag_id");
        builder.Property(e => e.CentroidVector).HasColumnName("centroid_vector").HasColumnType("vector(384)");
        builder.Property(e => e.ErrorCount).HasColumnName("error_count");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.TagId).IsUnique();

        builder.HasOne(e => e.Tag)
            .WithOne()
            .HasForeignKey<TagCentroid>(e => e.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
