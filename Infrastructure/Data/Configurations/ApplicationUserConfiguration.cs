using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.ToTable(t => t.HasCheckConstraint("CK_Users_EmployeeNumber", "[Id] BETWEEN 1 AND 1000"));

        builder.Property(u => u.Name).HasMaxLength(15).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(25).IsRequired();
        builder.Property(u => u.UserName).HasMaxLength(25).IsRequired();
        builder.Property(u => u.Grade).HasMaxLength(50);
        builder.Property(u => u.Location).HasMaxLength(100);
        builder.Property(u => u.RankLevel).IsRequired().HasDefaultValue(1);
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAtUtc).IsRequired();

        builder
            .HasOne(u => u.Superior)
            .WithMany()
            .HasForeignKey(u => u.SuperiorEmployeeNumber)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
