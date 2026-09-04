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

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue(SupplierRequest.StatusPendingArrival);

        builder.HasIndex(r => r.SupplierId);
        builder.HasIndex(r => r.CreatedAtUtc);

        // The Business Manager's arrival queue reads this; also stops a typo'd status silently
        // becoming a third state the confirm guard would not recognise.
        builder.HasIndex(r => r.Status);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReceivedByEmployeeNumber)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("SupplierRequests", t =>
            t.HasCheckConstraint(
                "CK_SupplierRequests_Status",
                $"[Status] IN ('{SupplierRequest.StatusPendingArrival}', '{SupplierRequest.StatusReceived}')"));

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
