using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApi.IntegrationTests;

public class CatalogueTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 301, "Mia Manager", "mia.cat@hmt.test", "Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 302, "Eve Engineer", "eve.cat@hmt.test", "Engineer", "Password1!");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetItems_RoleFiltersOutItemsAboveCallerRank()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);
        await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 3);

        var client = await AuthedClientAsync(302, "Password1!"); // Engineer, rank 1

        var response = await client.GetAsync("/api/v1/items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(1);
        items[0].GetProperty("minRankLevelToRequest").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Manager_CanCreateItem()
    {
        var (category, _) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var client = await AuthedClientAsync(301, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/items", new
        {
            itemName = "Stapler",
            categoryId = category.Id,
            unitOfMeasure = "Each",
            unitCost = 4.5,
            reorderLevel = 5,
            minRankLevelToRequest = 1,
            supplierId = (int?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Engineer_CannotCreateItem_Receives403()
    {
        var (category, _) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var client = await AuthedClientAsync(302, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/items", new
        {
            itemName = "Stapler",
            categoryId = category.Id,
            unitOfMeasure = "Each",
            unitCost = 4.5,
            reorderLevel = 5,
            minRankLevelToRequest = 1,
            supplierId = (int?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
