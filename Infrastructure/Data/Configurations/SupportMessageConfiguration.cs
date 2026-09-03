using Core.Entities;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessage>
{
    public void Configure(EntityTypeBuilder<SupportMessage> builder)
    {
        builder.Property(m => m.Area).IsRequired().HasMaxLength(80);
        builder.Property(m => m.Subject).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Body).IsRequired().HasMaxLength(4000);
        builder.Property(m => m.Diagnostics).HasMaxLength(4000);
        builder.Property(m => m.Status).IsRequired().HasMaxLength(20);

        builder.Property(m => m.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");
        builder.Property(m => m.ResolvedAtUtc).HasColumnType("datetime2");

        // The triage screen reads newest-first and filters by status.
        builder.HasIndex(m => m.CreatedAtUtc);
        builder.HasIndex(m => m.Status);

        // No navigation properties to ApplicationUser — same pattern as Notification /
        // AiInteractionLog. Restrict so a user row can't be deleted out from under a message.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.SenderEmployeeNumber)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.ResolvedByEmployeeNumber)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("SupportMessages");
    }
}
