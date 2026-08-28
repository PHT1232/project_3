using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class SupplierRequestItemConfiguration : IEntityTypeConfiguration<SupplierRequestItem>
{
    public void Configure(EntityTypeBuilder<SupplierRequestItem> builder)
    {
        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitCostSnapshot).HasPrecision(18, 2);
        builder.Property(i => i.LineTotal).HasPrecision(18, 2);

        builder.ToTable(t =>
            t.HasCheckConstraint("CK_SupplierRequestItems_Quantity", "[Quantity] > 0"));

        // An item may appear only once per order — the same rule the validator enforces, kept at
        // the database level too so it cannot be bypassed.
        builder.HasIndex(i => new { i.SupplierRequestId, i.ItemId }).IsUnique();

        builder.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
