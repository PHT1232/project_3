using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApi.IntegrationTests;

public class SuppliersTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 501, "Mia Manager", "mia.sup@hmt.test", "Manager", "Password1!");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DeactivateSupplier_WithActiveItem_Returns409()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PatchAsync($"/api/v1/suppliers/{supplier.Id}/deactivate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeactivateSupplier_WithNoActiveItems_Succeeds()
    {
        var (_, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PatchAsync($"/api/v1/suppliers/{supplier.Id}/deactivate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
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
