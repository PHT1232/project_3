using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Entities;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests;

/// <summary>
/// The security-critical part of the Reports feature: a role only ever gets spend data for
/// its own reporting scope (Self / Team / Group / Org), enforced server-side in
/// Infrastructure.Queries.ReportQueries — never by the client hiding a tab.
///
/// Hierarchy under test:
///   MD (900)
///     BM1 (910) -- Mgr1 (920) -- Eng1 (930)
///                -- Mgr2 (921)
///     BM2 (911) -- MgrX (940) -- EngX (950)
/// </summary>
public class ReportsTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();
    private const string Password = "Password1!";

    private const int Md = 900;
    private const int Bm1 = 910;
    private const int Bm2 = 911;
    private const int Mgr1 = 920;
    private const int Mgr2 = 921;
    private const int Eng1 = 930;
    private const int MgrX = 940;
    private const int EngX = 950;

    private int _itemId;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(_factory.Services, Md, "Test MD", "md@reports.test", "Managing Director", Password);
        await TestUserFactory.CreateUserAsync(_factory.Services, Bm1, "Test BM1", "bm1@reports.test", "Business Manager", Password, Md);
        await TestUserFactory.CreateUserAsync(_factory.Services, Bm2, "Test BM2", "bm2@reports.test", "Business Manager", Password, Md);
        await TestUserFactory.CreateUserAsync(_factory.Services, Mgr1, "Test Mgr1", "mgr1@reports.test", "Manager", Password, Bm1);
        await TestUserFactory.CreateUserAsync(_factory.Services, Mgr2, "Test Mgr2", "mgr2@reports.test", "Manager", Password, Bm1);
        await TestUserFactory.CreateUserAsync(_factory.Services, Eng1, "Test Eng1", "eng1@reports.test", "Engineer", Password, Mgr1);
        await TestUserFactory.CreateUserAsync(_factory.Services, MgrX, "Test MgrX", "mgrx@reports.test", "Manager", Password, Bm2);
        await TestUserFactory.CreateUserAsync(_factory.Services, EngX, "Test EngX", "engx@reports.test", "Engineer", Password, MgrX);

        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);
        _itemId = item.Id;

        // One approved request each for Eng1 (Mgr1/BM1's group), EngX (MgrX/BM2's group), and
        // Mgr1 personally — distinct, recognisable amounts so scope leaks are obvious.
        await SeedApprovedRequestAsync(Eng1, Mgr1, unitCost: 10m, quantity: 1); // 10.00
        await SeedApprovedRequestAsync(EngX, MgrX, unitCost: 10m, quantity: 5); // 50.00
        await SeedApprovedRequestAsync(Mgr1, Bm1, unitCost: 10m, quantity: 2);  // 20.00
    }

    private async Task SeedApprovedRequestAsync(int requestor, int approver, decimal unitCost, int quantity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var lineTotal = unitCost * quantity;
        var request = new Request
        {
            RequestorEmployeeNumber = requestor,
            ApproverEmployeeNumber = approver,
            Status = "Approved",
            TotalEstimatedCost = lineTotal,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            DecidedAtUtc = DateTime.UtcNow.AddDays(-1),
            Items =
            [
                new RequestItem { ItemId = _itemId, Quantity = quantity, UnitCostSnapshot = unitCost, LineTotal = lineTotal },
            ],
        };
        db.Requests.Add(request);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static (string From, string To) LastWeek() =>
        (DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)).ToString("yyyy-MM-dd"),
         DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));

    private async Task<decimal> CostByItemTotalAsync(int employeeNumber)
    {
        var (from, to) = LastWeek();
        var client = await AuthedClientAsync(employeeNumber);
        var response = await client.GetAsync($"/api/v1/reports/cost-by-item?fromDate={from}&toDate={to}");
        var raw = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"expected 2xx, got {(int)response.StatusCode}: {raw}");
        var body = JsonDocument.Parse(raw).RootElement;
        return body.GetProperty("totalApprovedCost").GetDecimal();
    }

    [Fact]
    public async Task Engineer_SeesOnlyOwnSpend()
    {
        var total = await CostByItemTotalAsync(Eng1);
        total.Should().Be(10.00m); // not EngX's 50, not Mgr1's 20
    }

    [Fact]
    public async Task Manager_SeesOwnTeamOnly_NotOtherManagersTeam()
    {
        // Mgr1's team = Mgr1 (20) + Eng1 (10) = 30. MgrX's team (EngX, 50) must not leak in.
        var total = await CostByItemTotalAsync(Mgr1);
        total.Should().Be(30.00m);
    }

    [Fact]
    public async Task BusinessManager_SeesOwnGroupOnly_NotSiblingGroup()
    {
        // BM1's group = Mgr1 (20) + Mgr2 (0) + Eng1 (10) = 30. BM2's group (MgrX/EngX, 50) excluded.
        var total = await CostByItemTotalAsync(Bm1);
        total.Should().Be(30.00m);
    }

    [Fact]
    public async Task ManagingDirector_SeesEverything()
    {
        var total = await CostByItemTotalAsync(Md);
        total.Should().Be(80.00m); // 10 + 50 + 20
    }

    [Fact]
    public async Task MyActivity_IsAlwaysSelfOnly_EvenForAManagerWithATeam()
    {
        // Mgr1's team-scoped report includes Eng1's 10, but "my activity" must not.
        var (from, to) = LastWeek();
        var client = await AuthedClientAsync(Mgr1);
        var response = await client.GetAsync($"/api/v1/reports/my-activity?fromDate={from}&toDate={to}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("approvedCost").GetDecimal().Should().Be(20.00m);
    }

    [Fact]
    public async Task ByTeam_ForBusinessManager_BreaksDownPerManager_WithoutSiblingGroup()
    {
        var (from, to) = LastWeek();
        var client = await AuthedClientAsync(Bm1);
        var response = await client.GetAsync($"/api/v1/reports/by-team?fromDate={from}&toDate={to}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalApprovedCost").GetDecimal().Should().Be(30.00m);
        var teamNames = body.GetProperty("rows").EnumerateArray()
            .Select(r => r.GetProperty("teamName").GetString())
            .ToList();
        teamNames.Should().Contain(n => n!.Contains("Mgr1"));
        teamNames.Should().NotContain(n => n!.Contains("MgrX"));
    }

    private async Task<HttpClient> AuthedClientAsync(int employeeNumber)
    {
        var anonymousClient = _factory.CreateClient();
        var loginResponse = await anonymousClient.PostAsJsonAsync(
            "/api/v1/auth/login", new { employeeNumber, password = Password });
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
