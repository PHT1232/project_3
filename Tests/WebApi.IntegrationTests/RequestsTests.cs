using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests;

public class RequestsTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        // 601: Manager / Approver
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 601, "Manny Manager", "manny.req@hmt.test", "Manager", "Password1!");

        // 602: Engineer whose superior is 601
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 602, "Enid Engineer", "enid.req@hmt.test", "Engineer", "Password1!", superiorEmployeeNumber: 601);

        // 603: Another Engineer whose superior is null/nobody
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 603, "Other Engineer", "other.req@hmt.test", "Engineer", "Password1!");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateRequest_ValidPayload_ReturnsCreatedWithPendingStatus()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var client = await AuthedClientAsync(602, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 4 } },
            requiredByDate = DateTime.UtcNow.AddDays(7)
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Pending");
        body.GetProperty("requestorEmployeeNumber").GetInt32().Should().Be(602);
        body.GetProperty("approverEmployeeNumber").GetInt32().Should().Be(601);
        body.GetProperty("totalEstimatedCost").GetDecimal().Should().Be(20.00m);
        body.GetProperty("items").GetArrayLength().Should().Be(1);
        body.GetProperty("statusHistory").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SubmitRequest_PendingRequest_TransitionsToSubmitted()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var client = await AuthedClientAsync(602, "Password1!");

        // 1. Create request
        var createRes = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 2 } }
        });
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();
        var rowVersion = created.GetProperty("rowVersion").GetString();

        // 2. Submit request
        var submitRes = await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/submit", new
        {
            requestId,
            rowVersion = Guid.Parse(rowVersion!)
        });

        submitRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        submitted.GetProperty("status").GetString().Should().Be("Pending");
        submitted.GetProperty("statusHistory").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task WithdrawRequest_PendingRequest_TransitionsToWithdrawn()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var client = await AuthedClientAsync(602, "Password1!");

        // 1. Create request
        var createRes = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 2 } }
        });
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();
        var rowVersion = created.GetProperty("rowVersion").GetString();

        // 2. Withdraw
        var withdrawRes = await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/withdraw", new
        {
            requestId,
            rowVersion = Guid.Parse(rowVersion!)
        });

        withdrawRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var withdrawn = await withdrawRes.Content.ReadFromJsonAsync<JsonElement>();
        withdrawn.GetProperty("status").GetString().Should().Be("Withdrawn");
    }

    [Fact]
    public async Task DeletePendingRequest_Unsubmitted_DeletesSuccessfully()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var client = await AuthedClientAsync(602, "Password1!");

        var createRes = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 2 } }
        });
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();

        var deleteRes = await client.DeleteAsync($"/api/v1/requests/{requestId}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify gone
        var getRes = await client.GetAsync($"/api/v1/requests/{requestId}");
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyCallersRequests()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var client602 = await AuthedClientAsync(602, "Password1!");
        var client603 = await AuthedClientAsync(603, "Password1!");

        // 602 creates request
        await client602.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 1 } }
        });

        // 603 creates request
        await client603.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 3 } }
        });

        // 602 gets mine
        var res602 = await client602.GetAsync("/api/v1/requests/mine");
        res602.StatusCode.Should().Be(HttpStatusCode.OK);
        var body602 = await res602.Content.ReadFromJsonAsync<JsonElement>();
        body602.GetProperty("totalCount").GetInt32().Should().Be(1);
        body602.GetProperty("items")[0].GetProperty("requestorEmployeeNumber").GetInt32().Should().Be(602);
    }

    [Fact]
    public async Task GetById_UnauthorizedUser_Returns404()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var client602 = await AuthedClientAsync(602, "Password1!");
        var client603 = await AuthedClientAsync(603, "Password1!");

        // 602 creates request
        var createRes = await client602.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 1 } }
        });
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();

        // 603 tries to read 602's request -> 404 (does not leak existence)
        var res603 = await client603.GetAsync($"/api/v1/requests/{requestId}");
        res603.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
