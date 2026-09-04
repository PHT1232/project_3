using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApi.IntegrationTests;

public class InventoryTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 401, "Mia Manager", "mia.inv@hmt.test", "Manager", "Password1!");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ReceiveEndpoint_IsGone_StockCannotBeRaisedAdHoc()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1, quantityAvailable: 50);

        var client = await AuthedClientAsync(401, "Password1!");

        // Removed 2026-09-04: raising the balance without a confirmed delivery is exactly the bug.
        // Receipts now only come from a Business Manager confirming a supplier order's arrival
        // (see SupplierRequestsTests).
        var response = await client.PostAsJsonAsync($"/api/v1/inventory/{item.Id}/receive", new
        {
            quantity = 20,
            supplierId = supplier.Id,
            reference = "PO-1",
            rowVersion = item.RowVersion,
        });

        // 405, not 404: the SPA fallback (MapFallbackToFile) claims unmatched paths for GET, so a
        // POST to a route that no longer exists comes back Method Not Allowed. Either is proof the
        // endpoint is gone; what matters is the balance below.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);

        var inventory = await client.GetAsync("/api/v1/inventory?pageSize=200");
        var body = await inventory.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("page").GetProperty("items").EnumerateArray()
            .Single(r => r.GetProperty("itemId").GetInt32() == item.Id)
            .GetProperty("quantityAvailable").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task AdjustStock_StaleRowVersion_Returns409()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1, quantityAvailable: 50);

        var client = await AuthedClientAsync(401, "Password1!");

        // First adjustment succeeds and changes the RowVersion server-side.
        var first = await client.PostAsJsonAsync($"/api/v1/inventory/{item.Id}/adjust", new
        {
            changeQuantity = -5,
            reason = "Damaged stock",
            rowVersion = item.RowVersion,
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second request replays the now-stale RowVersion — simulates a concurrent editor.
        var second = await client.PostAsJsonAsync($"/api/v1/inventory/{item.Id}/adjust", new
        {
            changeQuantity = -3,
            reason = "Another adjustment",
            rowVersion = item.RowVersion,
        });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AdjustStock_BelowZero_Returns400()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1, quantityAvailable: 5);

        var client = await AuthedClientAsync(401, "Password1!");

        var response = await client.PostAsJsonAsync($"/api/v1/inventory/{item.Id}/adjust", new
        {
            changeQuantity = -10,
            reason = "Too much",
            rowVersion = item.RowVersion,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpClient> AuthedClientAsync(int employeeNumber, string password)
    {
        var anonymousClient = _factory.CreateClient();
        var loginResponse = await anonymousClient.PostAsJsonAsync(
            "/api/v1/auth/login", new { employeeNumber, password });
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
