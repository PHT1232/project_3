using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for Request entity — maps to [Requests] table with concurrency control,
/// FK relationships, status checks, and default values per StationerySchema.sql.
/// </summary>
public class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        // Primary key and concurrency token
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RowVersion)
            .HasDefaultValueSql("NEWID()")
            .IsConcurrencyToken();

        // Required fields with constraints
        builder.Property(r => r.RequestorEmployeeNumber)
            .IsRequired();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Pending");

        builder.Property(r => r.TotalEstimatedCost)
            .HasPrecision(18, 2)
            .IsRequired()
            .HasDefaultValue(0m);

        builder.Property(r => r.RequiredByDate)
            .HasColumnType("datetime2");

        builder.Property(r => r.DecisionComment)
            .HasMaxLength(500);

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(r => r.DecidedAtUtc)
            .HasColumnType("datetime2");

        // Navigation properties
        builder.HasMany(r => r.Items)
            .WithOne(ri => ri.Request)
            .HasForeignKey(ri => ri.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.StatusHistory)
            .WithOne(rsh => rsh.Request)
            .HasForeignKey(rsh => rsh.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Table name with status check constraint
        builder.ToTable("Requests", t =>
            t.HasCheckConstraint(
                "CK_Requests_Status",
                "[Status] IN ('Pending', 'Approved', 'PartiallyApproved', 'Rejected', 'Withdrawn', 'CancellationPending', 'Cancelled', 'Fulfilled')"
            )
        );
    }
}
