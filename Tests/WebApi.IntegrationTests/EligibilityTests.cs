using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Entities;
using FluentAssertions;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests;

/// <summary>
/// GET /api/v1/users/me/eligibility — per-role monthly spending allowance, this employee's
/// month-to-date committed spend, and what's left.
/// </summary>
public class EligibilityTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();
    private const string Password = "Password1!";

    private const int Eng = 700;
    private const int Mgr = 701;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        await TestUserFactory.CreateUserAsync(_factory.Services, Eng, "Test Eng", "eng@elig.test", "Engineer", Password);
        await TestUserFactory.CreateUserAsync(_factory.Services, Mgr, "Test Mgr", "mgr@elig.test", "Manager", Password);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Engineer_WithNoRequests_HasTheFullMonthlyAllowance()
    {
        var body = await GetEligibilityAsync(Eng);

        body.GetProperty("role").GetString().Should().Be("Engineer");
        body.GetProperty("rankLevel").GetInt32().Should().Be(1);
        body.GetProperty("maxAmountPerMonth").GetDecimal().Should().Be(500m);
        body.GetProperty("monthToDateSpend").GetDecimal().Should().Be(0m);
        body.GetProperty("remainingThisMonth").GetDecimal().Should().Be(500m);
    }

    [Fact]
    public async Task Manager_HasTheHigherAllowance()
    {
        var body = await GetEligibilityAsync(Mgr);
        body.GetProperty("maxAmountPerMonth").GetDecimal().Should().Be(2000m);
        body.GetProperty("remainingThisMonth").GetDecimal().Should().Be(2000m);
    }

    [Fact]
    public async Task MonthToDateSpend_CountsCommittedRequestsThisMonth_NotRejectedWithdrawnOrLastMonth()
    {
        var now = DateTime.UtcNow;
        await SeedRequestAsync(Eng, "Approved", 120m, now.AddDays(-1));
        await SeedRequestAsync(Eng, "Rejected", 300m, now.AddDays(-2));
        await SeedRequestAsync(Eng, "Withdrawn", 50m, now.AddDays(-3));
        await SeedRequestAsync(Eng, "Approved", 999m, now.AddMonths(-1)); // previous month

        var body = await GetEligibilityAsync(Eng);

        body.GetProperty("monthToDateSpend").GetDecimal().Should().Be(120m);
        body.GetProperty("remainingThisMonth").GetDecimal().Should().Be(380m);
    }

    [Fact]
    public async Task RemainingThisMonth_IsClampedAtZero_WhenSpendExceedsTheLimit()
    {
        await SeedRequestAsync(Eng, "Approved", 650m, DateTime.UtcNow.AddDays(-1));

        var body = await GetEligibilityAsync(Eng);

        body.GetProperty("monthToDateSpend").GetDecimal().Should().Be(650m);
        body.GetProperty("remainingThisMonth").GetDecimal().Should().Be(0m);
    }

    /// <summary>
    /// Seeds a request carrying a single line worth <paramref name="total"/>.
    ///
    /// The line is not decoration: month-to-date spend is summed from RequestItems, not from
    /// Request.TotalEstimatedCost, so that a partial approval charges what was granted rather
    /// than what was asked for. A request with no lines cannot exist through the API either —
    /// CreateRequestCommandValidator requires at least one — so seeding one without lines would
    /// be testing a state the system cannot produce.
    /// </summary>
    private async Task SeedRequestAsync(int requestor, string status, decimal total, DateTime createdAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var item = await db.StationeryItems.FirstOrDefaultAsync();
        if (item is null)
        {
            var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
            item = await CatalogueTestData.SeedItemAsync(
                _factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);
        }

        db.Requests.Add(new Request
        {
            RequestorEmployeeNumber = requestor,
            Status = status,
            TotalEstimatedCost = total,
            CreatedAtUtc = createdAtUtc,
            Items =
            [
                new RequestItem
                {
                    ItemId = item.Id,
                    Quantity = 1,
                    UnitCostSnapshot = total,
                    LineTotal = total,
                },
            ],
        });
        await db.SaveChangesAsync();
    }

    private async Task<JsonElement> GetEligibilityAsync(int employeeNumber)
    {
        var client = await AuthedClientAsync(employeeNumber);
        var response = await client.GetAsync("/api/v1/users/me/eligibility");
        var raw = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"expected 2xx, got {(int)response.StatusCode}: {raw}");
        return JsonDocument.Parse(raw).RootElement;
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
