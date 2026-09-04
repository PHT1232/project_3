using System.Net;
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
/// Audit finding C8 / Plan §3.6: "Pending → Approved · Stock ≥ quantity for every line ·
/// Decrement stock + write Issue transactions + notify both — one DB transaction", and
/// "CancellationPending → Cancelled · Restore stock + write Adjustment transactions".
///
/// Before this, nothing checked or moved stock on approval and <c>IStockService</c>'s issue
/// path had no callers at all.
/// </summary>
public class ApprovalStockTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 951, "Ann Approver", "ann.stock@hmt.test", "Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 952, "Rob Requestor", "rob.stock@hmt.test", "Engineer", "Password1!", superiorEmployeeNumber: 951);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Approve_DecrementsStockAndWritesOneIssueRowPerLine()
    {
        var item = await SeedItemAsync(quantityAvailable: 50);
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item.Id, 8);
        await ApproveAsync(approver, pending, DecisionsFor(pending, "approved"));

        (await StockOfAsync(item.Id)).Should().Be(42);

        var ledger = await LedgerForAsync(item.Id);
        ledger.Should().ContainSingle();
        ledger[0].TxType.Should().Be(StockTransactionType.Issue);
        ledger[0].ChangeQuantity.Should().Be(-8);
        ledger[0].RequestId.Should().Be(pending.GetProperty("requestId").GetInt32());
        ledger[0].CreatedByEmployeeNumber.Should().Be(951);
    }

    [Fact]
    public async Task Approve_ModifiedLine_IssuesTheApprovedQuantityNotTheRequestedOne()
    {
        var item = await SeedItemAsync(quantityAvailable: 50);
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item.Id, 10);
        var lineId = pending.GetProperty("items")[0].GetProperty("requestItemId").GetInt32();

        await ApproveAsync(approver, pending,
            [new { requestItemId = lineId, decision = "modified", modifiedQuantity = 3 }]);

        (await StockOfAsync(item.Id)).Should().Be(47);
        (await LedgerForAsync(item.Id))[0].ChangeQuantity.Should().Be(-3);
    }

    [Fact]
    public async Task Approve_RejectedLine_MovesNoStock()
    {
        var item = await SeedItemAsync(quantityAvailable: 50);
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item.Id, 10);
        await ApproveAsync(approver, pending, DecisionsFor(pending, "rejected"));

        (await StockOfAsync(item.Id)).Should().Be(50);
        (await LedgerForAsync(item.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Approve_WithoutEnoughStock_Returns422AndCommitsNothing()
    {
        var item = await SeedItemAsync(quantityAvailable: 5);
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item.Id, 9);
        var requestId = pending.GetProperty("requestId").GetInt32();

        var res = await ApproveRawAsync(approver, pending, DecisionsFor(pending, "approved"));

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain("5 in stock, 9 approved");

        // Nothing committed: Plan §3.6 requires status + stock + ledger to move together.
        (await StockOfAsync(item.Id)).Should().Be(5);
        (await LedgerForAsync(item.Id)).Should().BeEmpty();

        var after = await requestor.GetFromJsonAsync<JsonElement>($"/api/v1/requests/{requestId}");
        after.GetProperty("status").GetString().Should().Be("Pending");
    }

    [Fact]
    public async Task ApprovedThenCancelled_RestoresStockWithAnAdjustmentRow()
    {
        var item = await SeedItemAsync(quantityAvailable: 50);
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item.Id, 6);
        var approved = await ApproveAsync(approver, pending, DecisionsFor(pending, "approved"));
        var requestId = approved.GetProperty("requestId").GetInt32();
        (await StockOfAsync(item.Id)).Should().Be(44);

        var cancelPending = await PostAsync(requestor, $"/api/v1/requests/{requestId}/request-cancellation", new
        {
            requestId,
            rowVersion = RowVersionOf(approved),
            reason = "Ordered by mistake",
        });

        await PostAsync(approver, $"/api/v1/approvals/{requestId}/cancel-approval", new
        {
            requestId,
            rowVersion = RowVersionOf(cancelPending),
            approved = true,
            reason = "Agreed",
        });

        (await StockOfAsync(item.Id)).Should().Be(50);

        var ledger = await LedgerForAsync(item.Id);
        ledger.Should().HaveCount(2);
        ledger[1].TxType.Should().Be(StockTransactionType.Adjustment);
        ledger[1].ChangeQuantity.Should().Be(6);
        ledger[1].RequestId.Should().Be(requestId);
    }

    [Fact]
    public async Task RefusedCancellation_LeavesStockAlone()
    {
        var item = await SeedItemAsync(quantityAvailable: 50);
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item.Id, 6);
        var approved = await ApproveAsync(approver, pending, DecisionsFor(pending, "approved"));
        var requestId = approved.GetProperty("requestId").GetInt32();

        var cancelPending = await PostAsync(requestor, $"/api/v1/requests/{requestId}/request-cancellation", new
        {
            requestId,
            rowVersion = RowVersionOf(approved),
            reason = "Changed my mind",
        });

        await PostAsync(approver, $"/api/v1/approvals/{requestId}/cancel-approval", new
        {
            requestId,
            rowVersion = RowVersionOf(cancelPending),
            approved = false,
            reason = "Already picked",
        });

        (await StockOfAsync(item.Id)).Should().Be(44);
        (await LedgerForAsync(item.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task LedgerBalance_MatchesTheItemsCachedQuantity()
    {
        // CLAUDE.md principle #5 / Plan T2.6: QuantityAvailable is a cached balance and the
        // ledger is the truth. They must agree after a full approve-then-cancel round trip.
        var item = await SeedItemAsync(quantityAvailable: 30);
        var (requestor, approver) = await ClientsAsync();

        var pending = await CreateAndSubmitAsync(requestor, item.Id, 7);
        var approved = await ApproveAsync(approver, pending, DecisionsFor(pending, "approved"));
        var requestId = approved.GetProperty("requestId").GetInt32();

        var cancelPending = await PostAsync(requestor, $"/api/v1/requests/{requestId}/request-cancellation", new
        {
            requestId,
            rowVersion = RowVersionOf(approved),
            reason = "No longer needed",
        });
        await PostAsync(approver, $"/api/v1/approvals/{requestId}/cancel-approval", new
        {
            requestId,
            rowVersion = RowVersionOf(cancelPending),
            approved = true,
            reason = "Fine",
        });

        var ledger = await LedgerForAsync(item.Id);
        var netMovement = ledger.Sum(t => t.ChangeQuantity);
        (30 + netMovement).Should().Be(await StockOfAsync(item.Id));
    }

    // ---------------------------------------------------------------------------------------

    private static object[] DecisionsFor(JsonElement request, string decision) =>
        request.GetProperty("items").EnumerateArray()
            .Select(i => (object)new
            {
                requestItemId = i.GetProperty("requestItemId").GetInt32(),
                decision,
                modifiedQuantity = (int?)null,
            })
            .ToArray();

    private static Guid RowVersionOf(JsonElement request) =>
        Guid.Parse(request.GetProperty("rowVersion").GetString()!);

    private async Task<StationeryItem> SeedItemAsync(int quantityAvailable)
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        return await CatalogueTestData.SeedItemAsync(
            _factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1, quantityAvailable: quantityAvailable);
    }

    private async Task<(HttpClient Requestor, HttpClient Approver)> ClientsAsync() =>
        (await AuthedClientAsync(952), await AuthedClientAsync(951));

    private static async Task<JsonElement> CreateAndSubmitAsync(HttpClient requestor, int itemId, int quantity)
    {
        var created = await (await requestor.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId, quantity } },
        })).Content.ReadFromJsonAsync<JsonElement>();

        var requestId = created.GetProperty("requestId").GetInt32();
        var submitted = await requestor.PostAsJsonAsync($"/api/v1/requests/{requestId}/submit", new
        {
            requestId,
            rowVersion = RowVersionOf(created),
        });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        return await submitted.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> ApproveRawAsync(HttpClient approver, JsonElement request, object[] decisions)
    {
        var requestId = request.GetProperty("requestId").GetInt32();
        return approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/approve", new
        {
            requestId,
            rowVersion = RowVersionOf(request),
            lineDecisions = decisions,
            comment = "ok",
        });
    }

    private static async Task<JsonElement> ApproveAsync(HttpClient approver, JsonElement request, object[] decisions)
    {
        var res = await ApproveRawAsync(approver, request, decisions);
        res.StatusCode.Should().Be(HttpStatusCode.OK, "approve failed: {0}", await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string url, object body)
    {
        var res = await client.PostAsJsonAsync(url, body);
        res.StatusCode.Should().Be(HttpStatusCode.OK, "{0} failed: {1}", url, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<int> StockOfAsync(int itemId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        return (await db.StationeryItems.AsNoTracking().FirstAsync(i => i.Id == itemId)).QuantityAvailable;
    }

    private async Task<List<StockTransaction>> LedgerForAsync(int itemId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        return await db.StockTransactions.AsNoTracking()
            .Where(t => t.ItemId == itemId)
            .OrderBy(t => t.Id)
            .ToListAsync();
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
