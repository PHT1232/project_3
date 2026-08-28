using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class StationeryItemConfiguration : IEntityTypeConfiguration<StationeryItem>
{
    public void Configure(EntityTypeBuilder<StationeryItem> builder)
    {
        builder.Property(i => i.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.UnitOfMeasure).IsRequired().HasMaxLength(50);
        builder.Property(i => i.UnitCost).HasPrecision(18, 2);
        builder.Property(i => i.QuantityAvailable).HasDefaultValue(0);
        builder.Property(i => i.ReorderLevel).HasDefaultValue(0);
        builder.Property(i => i.MinRankLevelToRequest).HasDefaultValue(1);
        builder.Property(i => i.IsActive).IsRequired();

        // App-managed concurrency token — see SupplierConfiguration / m2 plan §4.1.
        builder.Property(i => i.RowVersion).IsConcurrencyToken();

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_StationeryItems_MinRankLevelToRequest", "[MinRankLevelToRequest] BETWEEN 1 AND 4"));

        builder.HasOne(i => i.Category)
            .WithMany()
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Supplier)
            .WithMany()
            .HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
