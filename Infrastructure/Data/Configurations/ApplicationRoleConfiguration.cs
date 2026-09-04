using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.Property(r => r.RankLevel).IsRequired();

        // decimal(18,2) to match StationerySchema.sql's RoleThresholds columns and the rest
        // of the money in this schema (Plan §9.3 — never float/double).
        builder.Property(r => r.MaxAmountPerRequest).HasPrecision(18, 2);
        builder.Property(r => r.MaxAmountPerMonth).HasPrecision(18, 2);
    }
}
