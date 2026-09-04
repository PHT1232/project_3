using Microsoft.Data.SqlClient;

namespace WebApi.IntegrationTests;

/// <summary>
/// Finds a local SQL Server to run the migration test against, once per test run.
///
/// The rest of the suite runs on SQLite in memory, which is deliberate (Plan §10.2) but means
/// the migration files themselves are never executed — <c>EnsureCreatedAsync</c> builds the
/// schema from the model and skips them entirely, so a broken migration passes CI (audit
/// finding M3). The migrations are also unavoidably SQL Server-flavoured: <c>NEWID()</c>,
/// <c>GETUTCDATE()</c>, <c>ALTER TABLE … ADD CONSTRAINT … CHECK</c> and bracketed T-SQL in the
/// hand-written data fixes, none of which SQLite accepts. So they can only be proven against the
/// real provider.
///
/// Several instance names are tried because the team has several in play: the checked-in dev
/// connection string targets LocalDB, at least one machine only has SQLEXPRESS (audit finding
/// L5/P7), and another runs SQL Server on the *default* instance — which the first version of
/// this probe missed, so these tests silently skipped there and audit P3 was only nominally
/// closed. Set SQLSERVER_TEST_CONNECTION to point CI at anything else.
/// </summary>
internal static class SqlServerAvailability
{
    /// <summary>Explicit override, tried first — the only option for a host whose instance is
    /// not at a name worth guessing.</summary>
    internal const string OverrideVariable = "SQLSERVER_TEST_CONNECTION";

    private static readonly string[] Candidates =
    [
        @"Server=(localdb)\mssqllocaldb;Integrated Security=true;Connect Timeout=5;TrustServerCertificate=True",
        @"Server=.\SQLEXPRESS;Integrated Security=true;Connect Timeout=5;TrustServerCertificate=True",
        @"Server=localhost;Integrated Security=true;Connect Timeout=5;TrustServerCertificate=True",
    ];

    /// <summary>A master connection string that opened, or null when no SQL Server is reachable.</summary>
    internal static readonly string? MasterConnectionString = Probe();

    private static string? Probe()
    {
        var configured = Environment.GetEnvironmentVariable(OverrideVariable);
        string[] candidates = string.IsNullOrWhiteSpace(configured)
            ? Candidates
            : [configured, .. Candidates];

        foreach (var candidate in candidates)
        {
            try
            {
                using var connection = new SqlConnection(candidate);
                connection.Open();
                return candidate;
            }
            catch
            {
                // Not this one — try the next. A machine with neither simply skips the test.
            }
        }

        return null;
    }

    internal static string ForDatabase(string master, string databaseName) =>
        new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
}

/// <summary>
/// A <see cref="FactAttribute"/> that skips instead of failing when no local SQL Server is
/// installed, so the suite stays green on a machine that has none while still executing for real
/// wherever one exists. xUnit 2.x has no <c>Assert.Skip</c>, and the skip reason has to be set at
/// discovery time, hence the attribute rather than a runtime check.
/// </summary>
public sealed class RequiresSqlServerFactAttribute : FactAttribute
{
    public RequiresSqlServerFactAttribute()
    {
        if (SqlServerAvailability.MasterConnectionString is null)
        {
            Skip = "No local SQL Server reachable (tried LocalDB, SQLEXPRESS and localhost; "
                 + $"set {SqlServerAvailability.OverrideVariable} to point at one) — migration test skipped.";
        }
    }
}
