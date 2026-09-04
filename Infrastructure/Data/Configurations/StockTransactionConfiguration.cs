using Core.Entities;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.Property(t => t.TxType).IsRequired();
        builder.Property(t => t.ChangeQuantity).IsRequired();
        builder.Property(t => t.UnitCostSnapshot).HasPrecision(18, 2);
        builder.Property(t => t.Reference).HasMaxLength(200);
        builder.Property(t => t.CreatedAtUtc).IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("CK_StockTransactions_ChangeQuantity", "[ChangeQuantity] <> 0"));

        builder.HasOne(t => t.Item)
            .WithMany()
            .HasForeignKey(t => t.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Supplier)
            .WithMany()
            .HasForeignKey(t => t.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // Set null rather than Restrict: the ledger is append-only truth and must survive the
        // one request deletion the system allows (a Draft — which by definition never moved
        // stock, so in practice this never fires). No navigation property on the Request side;
        // nothing needs to walk from a request to its ledger rows.
        builder.HasOne<Request>()
            .WithMany()
            .HasForeignKey(t => t.RequestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.RequestId);

        // No navigation property on StockTransaction to ApplicationUser — Core.StockTransaction
        // only has the scalar CreatedByEmployeeNumber FK (see that entity's doc comment / K8).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.CreatedByEmployeeNumber)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
