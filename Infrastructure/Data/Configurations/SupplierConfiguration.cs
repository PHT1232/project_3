using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.LeadTimeDays).IsRequired();
        builder.Property(s => s.IsActive).IsRequired();

        // App-managed concurrency token — IsConcurrencyToken(), not IsRowVersion(), so behavior
        // is identical on SQL Server (production) and SQLite (integration tests). See m2 plan §4.1.
        builder.Property(s => s.RowVersion).IsConcurrencyToken();
    }
}
