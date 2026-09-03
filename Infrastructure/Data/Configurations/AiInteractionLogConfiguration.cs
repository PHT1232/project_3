using Core.Entities;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class AiInteractionLogConfiguration : IEntityTypeConfiguration<AiInteractionLog>
{
    public void Configure(EntityTypeBuilder<AiInteractionLog> builder)
    {
        builder.Property(l => l.Feature).IsRequired().HasMaxLength(50);
        builder.Property(l => l.Model).IsRequired().HasMaxLength(100);
        builder.Property(l => l.FallbackReason).HasMaxLength(50);
        builder.Property(l => l.UserText).IsRequired().HasMaxLength(1000);
        builder.Property(l => l.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // The usage report reads newest-first; the per-user rate limit reads by user + time.
        builder.HasIndex(l => l.CreatedAtUtc);
        builder.HasIndex(l => new { l.EmployeeNumber, l.CreatedAtUtc });

        // No navigation property to ApplicationUser — same pattern as Notification.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.EmployeeNumber)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("AiInteractionLogs");
    }
}
