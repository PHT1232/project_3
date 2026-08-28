using Core.Entities;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class SupplierRequestConfiguration : IEntityTypeConfiguration<SupplierRequest>
{
    public void Configure(EntityTypeBuilder<SupplierRequest> builder)
    {
        builder.Property(r => r.TotalCost).HasPrecision(18, 2);
        builder.Property(r => r.CreatedAtUtc).IsRequired();

        builder.HasIndex(r => r.SupplierId);
        builder.HasIndex(r => r.CreatedAtUtc);

        builder.HasOne(r => r.Supplier)
            .WithMany()
            .HasForeignKey(r => r.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // Scalar FK only — Core.SupplierRequest has no navigation to ApplicationUser, same as
        // StockTransaction (see that entity's doc comment / K8).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.CreatedByEmployeeNumber)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items)
            .WithOne(i => i.SupplierRequest)
            .HasForeignKey(i => i.SupplierRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
