using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApi.IntegrationTests;

/// <summary>
/// End-to-end coverage: real HTTP calls through the actual request-lifecycle and auth
/// endpoints, verifying notifications actually land for both parties and that the feed/
/// unread-count/mark-read endpoints behave correctly — complements NotificationServiceTests,
/// which tests NotificationService in isolation.
/// </summary>
public class NotificationsControllerTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        // 801: Manager / Approver
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 801, "Nadia Approver", "nadia.notif@hmt.test", "Manager", "Password1!");

        // 802: Engineer whose superior is 801
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 802, "Remy Requestor", "remy.notif@hmt.test", "Engineer", "Password1!",
            superiorEmployeeNumber: 801);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SubmitRequest_NotifiesBothRequestorAndApprover()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestorClient = await AuthedClientAsync(802, "Password1!");

        var createResponse = await requestorClient.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 2 } },
            requiredByDate = DateTime.UtcNow.AddDays(7)
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();
        var rowVersion = created.GetProperty("rowVersion").GetGuid();

        var submitResponse = await requestorClient.PostAsJsonAsync(
            $"/api/v1/requests/{requestId}/submit", new { requestId, rowVersion });
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var approverClient = await AuthedClientAsync(801, "Password1!");

        var requestorFeed = await GetFeedAsync(requestorClient);
        var approverFeed = await GetFeedAsync(approverClient);

        requestorFeed.Should().Contain(n => n.GetProperty("eventType").GetString() == "RequestSubmitted");
        approverFeed.Should().Contain(n => n.GetProperty("eventType").GetString() == "RequestSubmitted");

        var approverUnreadCount = await GetUnreadCountAsync(approverClient);
        approverUnreadCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task MarkRead_ClearsThatNotificationFromUnreadCount()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestorClient = await AuthedClientAsync(802, "Password1!");
        var createResponse = await requestorClient.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 1 } },
            requiredByDate = DateTime.UtcNow.AddDays(7)
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();
        var rowVersion = created.GetProperty("rowVersion").GetGuid();
        await requestorClient.PostAsJsonAsync($"/api/v1/requests/{requestId}/submit", new { requestId, rowVersion });

        var beforeCount = await GetUnreadCountAsync(requestorClient);
        beforeCount.Should().BeGreaterThanOrEqualTo(1);

        var feed = await GetFeedAsync(requestorClient);
        var notificationId = feed[0].GetProperty("id").GetInt64();

        var markReadResponse = await requestorClient.PostAsync($"/api/v1/notifications/{notificationId}/read", null);
        markReadResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterCount = await GetUnreadCountAsync(requestorClient);
        afterCount.Should().Be(beforeCount - 1);
    }

    [Fact]
    public async Task MarkAllRead_ClearsUnreadCountToZero()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestorClient = await AuthedClientAsync(802, "Password1!");
        var createResponse = await requestorClient.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 1 } },
            requiredByDate = DateTime.UtcNow.AddDays(7)
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();
        var rowVersion = created.GetProperty("rowVersion").GetGuid();
        await requestorClient.PostAsJsonAsync($"/api/v1/requests/{requestId}/submit", new { requestId, rowVersion });

        var readAllResponse = await requestorClient.PostAsync("/api/v1/notifications/read-all", null);
        readAllResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterCount = await GetUnreadCountAsync(requestorClient);
        afterCount.Should().Be(0);
    }

    [Fact]
    public async Task ChangePassword_NotifiesUserAndTheirSuperior()
    {
        var requestorClient = await AuthedClientAsync(802, "Password1!");

        var changeResponse = await requestorClient.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "Password1!",
            newPassword = "NewPassword2!"
        });
        changeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var approverClient = await AuthedClientAsync(801, "Password1!");

        var requestorFeed = await GetFeedAsync(await AuthedClientAsync(802, "NewPassword2!"));
        var approverFeed = await GetFeedAsync(approverClient);

        requestorFeed.Should().Contain(n => n.GetProperty("eventType").GetString() == "PasswordChanged");
        approverFeed.Should().Contain(n => n.GetProperty("eventType").GetString() == "PasswordChanged");
    }

    private static async Task<List<JsonElement>> GetFeedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/notifications");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    private static async Task<int> GetUnreadCountAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/notifications/unread-count");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("count").GetInt32();
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
