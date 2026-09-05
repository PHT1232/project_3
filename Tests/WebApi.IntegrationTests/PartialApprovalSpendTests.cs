using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApi.IntegrationTests;

/// <summary>
/// A partial approval must cost what the approver granted, not what the requestor asked for.
///
/// Before this, stock moved by ApprovedQuantity (audit C8) while the budget charged
/// Request.TotalEstimatedCost and every cost report summed RequestItems.LineTotal — both the
/// *requested* figure. Someone who asked for 100 and was granted 10 was charged for 100, which
/// after C7 started hard-blocking submissions locked them out of budget they still had.
/// </summary>
public class PartialApprovalSpendTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 961, "Ada Approver", "ada.spend@hmt.test", "Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 962, "Rex Requestor", "rex.spend@hmt.test", "Engineer", "Password1!", superiorEmployeeNumber: 961);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Budget_ChargesTheApprovedQuantity_NotTheRequestedOne()
    {
        // Seeded unit cost is 5.00 and the Engineer allowance is 500.00.
        var item = await SeedItemAsync();
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item, 60);   // asked 300.00

        // While it is only Pending the whole ask is held against the allowance.
        (await RemainingAsync(requestor)).Should().Be(200.00m);

        await ApproveModifiedAsync(approver, pending, 10);               // granted 50.00

        // Charged 50, not 300 — the 250 the approver struck off comes back.
        var eligibility = await EligibilityAsync(requestor);
        eligibility.GetProperty("monthToDateSpend").GetDecimal().Should().Be(50.00m);
        eligibility.GetProperty("remainingThisMonth").GetDecimal().Should().Be(450.00m);
    }

    [Fact]
    public async Task Budget_FreedByAPartialApproval_IsUsableAgain()
    {
        var item = await SeedItemAsync();
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item, 100);  // asked the whole 500.00
        await ApproveModifiedAsync(approver, pending, 1);                // granted 5.00

        // 495.00 is free again, so a 400.00 request must go through. Before the fix this was a 422.
        var second = await CreateDraftAsync(requestor, item, 80);
        (await SubmitAsync(requestor, second)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RejectedLines_CostNothing()
    {
        var item = await SeedItemAsync();
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item, 20);
        var lineId = pending.GetProperty("items")[0].GetProperty("requestItemId").GetInt32();

        await ApproveAsync(approver, pending,
            [new { requestItemId = lineId, decision = "rejected", modifiedQuantity = (int?)null }]);

        (await EligibilityAsync(requestor)).GetProperty("monthToDateSpend").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task CostReports_CountTheApprovedQuantity()
    {
        var item = await SeedItemAsync();
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item, 60);   // asked 300.00
        await ApproveModifiedAsync(approver, pending, 10);               // granted 50.00

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var report = await approver.GetFromJsonAsync<JsonElement>(
            $"/api/v1/reports/cost-by-item?fromDate={today}&toDate={today}");

        report.GetProperty("totalApprovedCost").GetDecimal().Should().Be(50.00m);

        var rows = report.GetProperty("rows").EnumerateArray().ToList();
        rows.Should().ContainSingle();
        rows[0].GetProperty("approvedCost").GetDecimal().Should().Be(50.00m);
    }

    // ---------------------------------------------------------------------------------------

    private async Task<int> SeedItemAsync()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(
            _factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1, quantityAvailable: 100_000);
        return item.Id;
    }

    private async Task<(HttpClient Requestor, HttpClient Approver)> ClientsAsync() =>
        (await AuthedClientAsync(962), await AuthedClientAsync(961));

    private static Guid RowVersionOf(JsonElement e) => Guid.Parse(e.GetProperty("rowVersion").GetString()!);

    private static async Task<JsonElement> CreateDraftAsync(HttpClient c, int itemId, int quantity)
    {
        var res = await c.PostAsJsonAsync("/api/v1/requests", new { items = new[] { new { itemId, quantity } } });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient c, JsonElement draft)
    {
        var id = draft.GetProperty("requestId").GetInt32();
        return c.PostAsJsonAsync($"/api/v1/requests/{id}/submit", new { requestId = id, rowVersion = RowVersionOf(draft) });
    }

    private static async Task<JsonElement> CreateAndSubmitAsync(HttpClient c, int itemId, int quantity)
    {
        var draft = await CreateDraftAsync(c, itemId, quantity);
        var res = await SubmitAsync(c, draft);
        res.StatusCode.Should().Be(HttpStatusCode.OK, "submit failed: {0}", await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> ApproveAsync(HttpClient approver, JsonElement request, object[] decisions)
    {
        var id = request.GetProperty("requestId").GetInt32();
        var res = await approver.PostAsJsonAsync($"/api/v1/approvals/{id}/approve", new
        {
            requestId = id,
            rowVersion = RowVersionOf(request),
            lineDecisions = decisions,
            comment = "partial",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK, "approve failed: {0}", await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<JsonElement> ApproveModifiedAsync(HttpClient approver, JsonElement request, int grantedQuantity)
    {
        var lineId = request.GetProperty("items")[0].GetProperty("requestItemId").GetInt32();
        return ApproveAsync(approver, request,
            [new { requestItemId = lineId, decision = "modified", modifiedQuantity = grantedQuantity }]);
    }

    private static async Task<JsonElement> EligibilityAsync(HttpClient c) =>
        await c.GetFromJsonAsync<JsonElement>("/api/v1/users/me/eligibility");

    private static async Task<decimal> RemainingAsync(HttpClient c) =>
        (await EligibilityAsync(c)).GetProperty("remainingThisMonth").GetDecimal();

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
