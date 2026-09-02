using Core.Entities;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.EventType).IsRequired();
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Message).IsRequired().HasMaxLength(1000);
        builder.Property(n => n.IsRead).IsRequired();
        builder.Property(n => n.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Plan §3.3: "Unread-count poll — runs every 30s per user, must be cheap" —
        // this index is what keeps that query a single indexed lookup rather than a scan.
        builder.HasIndex(n => new { n.RecipientEmployeeNumber, n.IsRead });

        // No navigation property to ApplicationUser — same pattern as StockTransaction/
        // RequestStatusHistory (Core stays framework-independent).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.RecipientEmployeeNumber)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Request)
            .WithMany()
            .HasForeignKey(n => n.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Notifications");
    }
}
