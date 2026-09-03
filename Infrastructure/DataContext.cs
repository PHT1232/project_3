using Core.Entities;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class DataContext(DbContextOptions<DataContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, int>(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<StationeryItem> StationeryItems => Set<StationeryItem>();

    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

    public DbSet<SupplierRequest> SupplierRequests => Set<SupplierRequest>();

    public DbSet<SupplierRequestItem> SupplierRequestItems => Set<SupplierRequestItem>();

    public DbSet<Request> Requests => Set<Request>();

    public DbSet<RequestItem> RequestItems => Set<RequestItem>();

    public DbSet<RequestStatusHistory> RequestStatusHistories => Set<RequestStatusHistory>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AiInteractionLog> AiInteractionLogs => Set<AiInteractionLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(DataContext).Assembly);
    }
}
