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

        // A self-contained reporting line for the hierarchy-visibility tests:
        //   615 Managing Director
        //   └── 610 Business Manager
        //       ├── 611 Manager ──── 613 Engineer
        //       └── 612 Manager ──── 614 Engineer
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 615, "Dita Director", "dita.req@hmt.test", "Managing Director", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 610, "Bianca BizMgr", "bianca.req@hmt.test", "Business Manager", "Password1!", superiorEmployeeNumber: 615);
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 611, "Milo Manager", "milo.req@hmt.test", "Manager", "Password1!", superiorEmployeeNumber: 610);
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 612, "Petra Manager", "petra.req@hmt.test", "Manager", "Password1!", superiorEmployeeNumber: 610);
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 613, "Ravi Engineer", "ravi.req@hmt.test", "Engineer", "Password1!", superiorEmployeeNumber: 611);
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 614, "Sara Engineer", "sara.req@hmt.test", "Engineer", "Password1!", superiorEmployeeNumber: 612);
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

    [Fact]
    public async Task GetVisible_Manager_SeesOwnSubordinatesRequests_ButNotAPeersOrSuperiors()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var subordinate = await AuthedClientAsync(613, "Password1!");  // reports to Manager 611
        var peerEngineer = await AuthedClientAsync(614, "Password1!");  // reports to Manager 612
        var peerManager = await AuthedClientAsync(612, "Password1!");
        var superior = await AuthedClientAsync(610, "Password1!");      // Business Manager over 611

        await CreateRequestAsync(subordinate, item.Id, 1);
        await CreateRequestAsync(peerEngineer, item.Id, 1);
        await CreateRequestAsync(peerManager, item.Id, 1);
        await CreateRequestAsync(superior, item.Id, 1);

        var manager611 = await AuthedClientAsync(611, "Password1!");
        var visible = await manager611.GetFromJsonAsync<JsonElement>("/api/v1/requests?page=1&pageSize=50");

        var requestorIds = visible.GetProperty("items").EnumerateArray()
            .Select(r => r.GetProperty("requestorEmployeeNumber").GetInt32())
            .ToHashSet();

        requestorIds.Should().Contain(613, "an engineer who reports to 611");
        requestorIds.Should().NotContain(614, "an engineer who reports to a peer manager");
        requestorIds.Should().NotContain(612, "a peer manager");
        requestorIds.Should().NotContain(610, "the manager's own superior");
    }

    [Fact]
    public async Task GetVisible_BusinessManager_SeesRequestsTwoLevelsDown()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var deepEngineer = await AuthedClientAsync(614, "Password1!"); // 614 → 612 → 610
        await CreateRequestAsync(deepEngineer, item.Id, 1);

        var businessManager610 = await AuthedClientAsync(610, "Password1!");
        var visible = await businessManager610.GetFromJsonAsync<JsonElement>("/api/v1/requests?page=1&pageSize=50");

        var requestorIds = visible.GetProperty("items").EnumerateArray()
            .Select(r => r.GetProperty("requestorEmployeeNumber").GetInt32())
            .ToHashSet();

        requestorIds.Should().Contain(614);
    }

    [Fact]
    public async Task GetById_Manager_CannotSeeAPeerManagersRequest_Returns404()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var peerManager = await AuthedClientAsync(612, "Password1!");
        var requestId = await CreateRequestAsync(peerManager, item.Id, 1);

        var manager611 = await AuthedClientAsync(611, "Password1!");
        var res = await manager611.GetAsync($"/api/v1/requests/{requestId}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ManagingDirector_SeesAnyRequest()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var engineer = await AuthedClientAsync(602, "Password1!"); // reports to 601, outside MD 615's line
        var requestId = await CreateRequestAsync(engineer, item.Id, 1);

        var director615 = await AuthedClientAsync(615, "Password1!");
        var res = await director615.GetAsync($"/api/v1/requests/{requestId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<int> CreateRequestAsync(HttpClient client, int itemId, int quantity)
    {
        var res = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId, quantity } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created,
            "create failed: {0}", await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("requestId").GetInt32();
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
