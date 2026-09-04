using FluentAssertions;
using Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace WebApi.IntegrationTests;

/// <summary>
/// Runs the migration chain end to end against a real SQL Server, on a throwaway database that
/// is dropped afterwards (audit finding M3).
///
/// Every other integration test builds its schema with <c>EnsureCreatedAsync</c> on SQLite, which
/// never opens a migration file. That is the right trade for the other ~130 tests — it is fast
/// and provider-portable — but it means the eight migrations, their CHECK constraints and their
/// hand-written data fixes were completely unexercised. Three migrations landed in a single day
/// during the audit window without CI ever proving one of them applies.
///
/// Skips cleanly when no SQL Server is installed, so nobody gets a red build for not having one.
/// </summary>
public class MigrationTests : IAsyncLifetime
{
    // A fresh name per run so a crashed previous run cannot poison this one, and two runs on the
    // same machine never collide.
    private readonly string _databaseName = $"StationeryMS_MigrationTest_{Guid.NewGuid():N}";

    private string? _connectionString;

    public Task InitializeAsync()
    {
        if (SqlServerAvailability.MasterConnectionString is { } master)
        {
            _connectionString = SqlServerAvailability.ForDatabase(master, _databaseName);
        }

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (SqlServerAvailability.MasterConnectionString is not { } master)
        {
            return;
        }

        // SINGLE_USER WITH ROLLBACK IMMEDIATE: EF's pooled connections may still be open, and a
        // plain DROP would block behind them.
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();

        await using var drop = connection.CreateCommand();
        drop.CommandText = $"""
            IF DB_ID('{_databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_databaseName}];
            END
            """;
        await drop.ExecuteNonQueryAsync();
    }

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>().UseSqlServer(_connectionString!).Options);

    [RequiresSqlServerFact]
    public async Task EveryMigration_AppliesCleanly_FromAnEmptyDatabase()
    {
        await using var db = CreateContext();

        // The assertion is simply that this does not throw: a migration with invalid T-SQL, a
        // constraint that contradicts existing data, or an ordering mistake fails right here.
        await db.Database.MigrateAsync();

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        applied.Should().NotBeEmpty();
        pending.Should().BeEmpty("a fresh Migrate() must leave nothing outstanding");

        // Guards the failure mode that bit this branch twice during the audit: a migration
        // generated against a stale model snapshot, so its designer state disagrees with the
        // chain it sits in. When that happens the model has changes the migrations do not.
        db.Database.HasPendingModelChanges().Should().BeFalse(
            "the model and the migration chain must agree — regenerate the last migration if this fails");
    }

    [RequiresSqlServerFact]
    public async Task MigratedSchema_CarriesTheCheckConstraintsTheModelPromises()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var constraints = await ReadCheckConstraintsAsync();

        // The status vocabularies the workflow depends on. Draft was added by the C1 fix and
        // Fulfilled removed by C8; PendingArrival/Received came with goods-arrival confirmation.
        constraints.Should().ContainKey("CK_Requests_Status");
        constraints["CK_Requests_Status"].Should().Contain("Draft").And.Contain("CancellationPending");
        constraints["CK_Requests_Status"].Should().NotContain("Fulfilled",
            "C8 removed it — nothing can set it, so allowing it invites dead data back");

        constraints.Should().ContainKey("CK_RequestItems_Decision");
        constraints.Should().ContainKey("CK_SupplierRequests_Status");
        constraints["CK_SupplierRequests_Status"].Should().Contain("PendingArrival").And.Contain("Received");

        // Pre-existing invariants that must survive every future migration.
        constraints.Should().ContainKey("CK_StationeryItems_MinRankLevelToRequest");
        constraints.Should().ContainKey("CK_StockTransactions_ChangeQuantity");
    }

    private async Task<Dictionary<string, string>> ReadCheckConstraintsAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, definition FROM sys.check_constraints";

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }
}
