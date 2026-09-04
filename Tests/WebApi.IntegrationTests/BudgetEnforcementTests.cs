using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests;

/// <summary>
/// Audit finding C7 / Plan §3.6 (Draft → Pending guard "Total ≤ role threshold"), T3.4, TC-05.
///
/// Seeded item cost is 5.00 and an Engineer's monthly allowance is 500.00, so 100 units is
/// exactly the limit and every figure below is chosen off that. Each test method gets a fresh
/// in-memory database (xUnit builds a new class instance per test), so month-to-date spend
/// never leaks between them.
/// </summary>
public class BudgetEnforcementTests : IAsyncLifetime
{
    private const decimal EngineerMonthlyLimit = 500.00m;
    private const int UnitsAtTheLimit = 100;

    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 901, "Bea Boss", "bea.budget@hmt.test", "Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 902, "Eve Engineer", "eve.budget@hmt.test", "Engineer", "Password1!", superiorEmployeeNumber: 901);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Submit_ExactlyAtTheLimit_Succeeds()
    {
        var (client, itemId) = await SetupAsync();

        var draft = await CreateDraftAsync(client, itemId, UnitsAtTheLimit);
        draft.GetProperty("totalEstimatedCost").GetDecimal().Should().Be(EngineerMonthlyLimit);

        var submit = await SubmitAsync(client, draft);

        submit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Submit_OneUnitOverTheLimit_Returns422NamingTheLimitAndOverage()
    {
        var (client, itemId) = await SetupAsync();

        var draft = await CreateDraftAsync(client, itemId, UnitsAtTheLimit + 1);
        var submit = await SubmitAsync(client, draft);

        submit.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await submit.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().Should().Be("Business rule violation");
        var detail = problem.GetProperty("detail").GetString()!;
        detail.Should().Contain("505.00");  // the request total
        detail.Should().Contain("5.00");    // the overage
        detail.Should().Contain("500.00");  // the limit
    }

    [Fact]
    public async Task Submit_OverLimitRequest_StaysDraftAndNeverReachesTheApprover()
    {
        var (client, itemId) = await SetupAsync();
        var approver = await AuthedClientAsync(901);

        var draft = await CreateDraftAsync(client, itemId, UnitsAtTheLimit + 1);
        var requestId = draft.GetProperty("requestId").GetInt32();

        await SubmitAsync(client, draft);

        var after = await client.GetFromJsonAsync<JsonElement>($"/api/v1/requests/{requestId}");
        after.GetProperty("status").GetString().Should().Be("Draft");

        var queue = await approver.GetFromJsonAsync<JsonElement>("/api/v1/approvals/pending");
        queue.GetProperty("items").EnumerateArray()
            .Select(r => r.GetProperty("requestId").GetInt32())
            .Should().NotContain(requestId);
    }

    [Fact]
    public async Task Submit_IsJudgedOnRemainingBudget_NotTheFullAllowance()
    {
        var (client, itemId) = await SetupAsync();

        // Commit 300.00 of the 500.00 allowance.
        var first = await CreateDraftAsync(client, itemId, 60);
        (await SubmitAsync(client, first)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 205.00 fits the allowance but not the 200.00 that is left.
        var tooBig = await CreateDraftAsync(client, itemId, 41);
        (await SubmitAsync(client, tooBig)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 200.00 is exactly what remains.
        var fits = await CreateDraftAsync(client, itemId, 40);
        (await SubmitAsync(client, fits)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithdrawingARequest_ReleasesItsBudget()
    {
        var (client, itemId) = await SetupAsync();

        var first = await CreateDraftAsync(client, itemId, UnitsAtTheLimit);
        var submitted = await (await SubmitAsync(client, first)).Content.ReadFromJsonAsync<JsonElement>();
        var firstId = submitted.GetProperty("requestId").GetInt32();

        // Nothing left this month.
        var blocked = await CreateDraftAsync(client, itemId, 1);
        (await SubmitAsync(client, blocked)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await client.PostAsJsonAsync($"/api/v1/requests/{firstId}/withdraw", new
        {
            requestId = firstId,
            rowVersion = Guid.Parse(submitted.GetProperty("rowVersion").GetString()!),
        });

        // Withdrawn is not a committed status, so the allowance is free again.
        var retry = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/requests/{blocked.GetProperty("requestId").GetInt32()}");
        (await SubmitAsync(client, retry)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatingAnOverLimitDraft_IsAllowed_TheGuardIsOnSubmit()
    {
        var (client, itemId) = await SetupAsync();

        // Plan §3.6 puts the eligibility guard on the Draft -> Pending transition, so a user may
        // build and save an over-budget basket; they just cannot send it.
        var draft = await CreateDraftAsync(client, itemId, UnitsAtTheLimit * 10);

        draft.GetProperty("status").GetString().Should().Be("Draft");
        draft.GetProperty("totalEstimatedCost").GetDecimal().Should().Be(5000.00m);
    }

    [Fact]
    public async Task Submit_AsManager_UsesTheManagersHigherAllowance()
    {
        // 901 is a Manager (2 000/month) and reports to nobody by default, so give them a
        // superior to approve; a requestor with no superior cannot raise a request at all.
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 903, "Dee Director", "dee.budget@hmt.test", "Managing Director", "Password1!");

        var (_, itemId) = await SetupAsync();
        await SetSuperiorAsync(901, 903);

        var manager = await AuthedClientAsync(901);

        // 600.00 — over an Engineer's limit, well inside a Manager's.
        var draft = await CreateDraftAsync(manager, itemId, 120);
        (await SubmitAsync(manager, draft)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------------------------

    private async Task<(HttpClient Client, int ItemId)> SetupAsync()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(
            _factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1, quantityAvailable: 100_000);

        return (await AuthedClientAsync(902), item.Id);
    }

    private static async Task<JsonElement> CreateDraftAsync(HttpClient client, int itemId, int quantity)
    {
        var res = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId, quantity } },
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, JsonElement request)
    {
        var requestId = request.GetProperty("requestId").GetInt32();
        return client.PostAsJsonAsync($"/api/v1/requests/{requestId}/submit", new
        {
            requestId,
            rowVersion = Guid.Parse(request.GetProperty("rowVersion").GetString()!),
        });
    }

    private async Task SetSuperiorAsync(int employeeNumber, int superiorEmployeeNumber)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.DataContext>();
        var user = await db.Users.FirstAsync(u => u.Id == employeeNumber);
        user.SuperiorEmployeeNumber = superiorEmployeeNumber;
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> AuthedClientAsync(int employeeNumber)
    {
        var anon = _factory.CreateClient();
        var login = await anon.PostAsJsonAsync("/api/v1/auth/login", new { employeeNumber, password = "Password1!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }
}
