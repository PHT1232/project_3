using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for RequestItem entity — maps to [RequestItems] table.
/// Maintains snapshotted unit cost and computed line total for audit trail.
/// </summary>
public class RequestItemConfiguration : IEntityTypeConfiguration<RequestItem>
{
    public void Configure(EntityTypeBuilder<RequestItem> builder)
    {
        // Primary key
        builder.HasKey(ri => ri.Id);

        // Foreign keys
        builder.HasOne(ri => ri.Request)
            .WithMany(r => r.Items)
            .HasForeignKey(ri => ri.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ri => ri.Item)
            .WithMany()
            .HasForeignKey(ri => ri.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Properties
        builder.Property(ri => ri.RequestId).IsRequired();
        builder.Property(ri => ri.ItemId).IsRequired();

        builder.Property(ri => ri.Quantity)
            .IsRequired();

        builder.Property(ri => ri.UnitCostSnapshot)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(ri => ri.LineTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        // Approver's per-line outcome (audit finding C2). Both nullable: null = not yet decided.
        builder.Property(ri => ri.Decision)
            .HasMaxLength(20);

        builder.Property(ri => ri.ApprovedQuantity);

        // Table name with the same value set ApproveRequestCommandValidator accepts
        builder.ToTable("RequestItems", t =>
            t.HasCheckConstraint(
                "CK_RequestItems_Decision",
                "[Decision] IS NULL OR [Decision] IN ('approved', 'rejected', 'modified')"));
    }
}
