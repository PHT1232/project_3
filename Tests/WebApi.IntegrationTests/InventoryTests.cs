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
    public async Task ReceiveGoods_PersistsTransactionAndUpdatesBalance()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1, quantityAvailable: 50);

        var client = await AuthedClientAsync(401, "Password1!");

        var response = await client.PostAsJsonAsync($"/api/v1/inventory/{item.Id}/receive", new
        {
            quantity = 20,
            supplierId = supplier.Id,
            reference = "PO-1",
            rowVersion = item.RowVersion,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("quantityAvailable").GetInt32().Should().Be(70);

        var historyResponse = await client.GetAsync($"/api/v1/inventory/{item.Id}/transactions");
        var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement>();
        history.EnumerateArray().Should().Contain(t => t.GetProperty("reference").GetString() == "PO-1");
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
