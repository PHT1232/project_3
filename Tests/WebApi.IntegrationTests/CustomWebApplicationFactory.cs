using Infrastructure;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WebApi.IntegrationTests;

/// <summary>
/// EF Core SQLite in-memory (not the EF InMemory provider — plan §10) behind the real
/// DataContext/Identity stack, so migrations-shaped model behaviour is exercised for real.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // appsettings.Testing.json (loaded automatically by WebApplicationBuilder before our
        // Program.cs code runs) supplies Jwt:SigningKey — ConfigureAppConfiguration here is too
        // late, since Program.cs reads builder.Configuration eagerly before Build() completes.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // AddDbContext composes every IDbContextOptionsConfiguration<T> registration when
            // it builds DbContextOptions<T> — removing only DbContextOptions<T> leaves
            // Program.cs's UseSqlServer(...) configuration action registered alongside ours,
            // and EF Core rejects two providers configured on the same options.
            services.RemoveAll<DbContextOptions<DataContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DataContext>>();

            _connection.Open();
            services.AddDbContext<DataContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeAsync()
    {
        // Force host creation so ConfigureServices above has run before we reach into it.
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await DbSeeder.SeedRolesAsync(roleManager);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
