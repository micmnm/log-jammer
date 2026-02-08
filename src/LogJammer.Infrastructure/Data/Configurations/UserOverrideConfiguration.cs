using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class UserOverrideConfiguration : IEntityTypeConfiguration<UserOverride>
{
    public void Configure(EntityTypeBuilder<UserOverride> builder)
    {
        builder.ToTable("user_overrides");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.KnownErrorId).HasColumnName("known_error_id");
        builder.Property(e => e.OverrideType).HasColumnName("override_type").HasMaxLength(30).IsRequired();
        builder.Property(e => e.OverrideData).HasColumnName("override_data").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne(e => e.KnownError)
            .WithMany(k => k.UserOverrides)
            .HasForeignKey(e => e.KnownErrorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
