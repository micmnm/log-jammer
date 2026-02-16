using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class ClassificationConfigConfiguration : IEntityTypeConfiguration<ClassificationConfig>
{
    public void Configure(EntityTypeBuilder<ClassificationConfig> builder)
    {
        builder.ToTable("classification_config");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Value).HasColumnName("value").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.Key).IsUnique();
    }
}
