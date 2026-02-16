using LogJammer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogJammer.Infrastructure.Data.Configurations;

public class FingerprintAliasConfiguration : IEntityTypeConfiguration<FingerprintAlias>
{
    public void Configure(EntityTypeBuilder<FingerprintAlias> builder)
    {
        builder.ToTable("fingerprint_aliases");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.FingerprintHash).HasColumnName("fingerprint_hash").HasMaxLength(64).IsRequired();
        builder.Property(e => e.KnownErrorId).HasColumnName("known_error_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.FingerprintHash).IsUnique();

        builder.HasOne(e => e.KnownError)
            .WithMany(k => k.FingerprintAliases)
            .HasForeignKey(e => e.KnownErrorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
