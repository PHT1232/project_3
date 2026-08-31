using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for RequestStatusHistory entity — maps to [RequestStatusHistory] table.
/// Append-only audit trail of status transitions with actor and comment for notifications and reporting.
/// </summary>
public class RequestStatusHistoryConfiguration : IEntityTypeConfiguration<RequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<RequestStatusHistory> builder)
    {
        // Primary key
        builder.HasKey(rsh => rsh.Id);

        // Foreign key
        builder.HasOne(rsh => rsh.Request)
            .WithMany(r => r.StatusHistory)
            .HasForeignKey(rsh => rsh.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Properties
        builder.Property(rsh => rsh.RequestId).IsRequired();

        builder.Property(rsh => rsh.FromStatus)
            .HasMaxLength(50);

        builder.Property(rsh => rsh.ToStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(rsh => rsh.ActorEmployeeNumber)
            .IsRequired();

        builder.Property(rsh => rsh.Comment)
            .HasMaxLength(500);

        builder.Property(rsh => rsh.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Table name
        builder.ToTable("RequestStatusHistory");
    }
}
