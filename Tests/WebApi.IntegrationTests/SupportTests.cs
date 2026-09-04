using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApi.IntegrationTests;

/// <summary>
/// The in-app support inbox (Option B of the "contact the team" decision — no SMTP).
/// Any authenticated user can send; only Manager+ can list or resolve.
/// </summary>
public class SupportTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 801, "Meg Manager", "meg.sup@hmt.test", "Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 802, "Ed Engineer", "ed.sup@hmt.test", "Engineer", "Password1!", superiorEmployeeNumber: 801);
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 804, "Dana Director", "dana.sup@hmt.test", "Managing Director", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 805, "Vic Director", "vic.sup@hmt.test", "Managing Director", "Password1!");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static object ValidMessage => new
    {
        area = "Approvals",
        subject = "Approve button does nothing",
        body = "Clicking Approve on request #5 spins forever.",
        diagnostics = "App version: abc123\nBrowser: test",
    };

    [Fact]
    public async Task Send_Anonymous_Returns401()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/v1/support/messages", ValidMessage);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Send_AsEngineer_StoresTheMessageAsNew()
    {
        var engineer = await AuthedClientAsync(802);

        var res = await engineer.PostAsJsonAsync("/api/v1/support/messages", ValidMessage);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("New");
        body.GetProperty("senderEmployeeNumber").GetInt32().Should().Be(802);
        body.GetProperty("senderName").GetString().Should().Be("Ed Engineer");
        body.GetProperty("subject").GetString().Should().Be("Approve button does nothing");
    }

    [Fact]
    public async Task Send_BlankBody_Returns400()
    {
        var engineer = await AuthedClientAsync(802);

        var res = await engineer.PostAsJsonAsync("/api/v1/support/messages", new
        {
            area = "Approvals",
            subject = "x",
            body = "   ",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_AsEngineer_Returns403()
    {
        var engineer = await AuthedClientAsync(802);
        var res = await engineer.GetAsync("/api/v1/support/messages");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsManager_ReturnsMessagesNewestFirst_AndFiltersByStatus()
    {
        var engineer = await AuthedClientAsync(802);
        await engineer.PostAsJsonAsync("/api/v1/support/messages", ValidMessage);
        await engineer.PostAsJsonAsync("/api/v1/support/messages", new
        {
            area = "Reports",
            subject = "Second message",
            body = "Totals look wrong.",
        });

        var manager = await AuthedClientAsync(801);

        var all = await manager.GetFromJsonAsync<JsonElement>("/api/v1/support/messages");
        all.GetProperty("totalCount").GetInt32().Should().Be(2);
        all.GetProperty("items")[0].GetProperty("subject").GetString().Should().Be("Second message");

        var newOnly = await manager.GetFromJsonAsync<JsonElement>("/api/v1/support/messages?status=New");
        newOnly.GetProperty("totalCount").GetInt32().Should().Be(2);

        var resolvedOnly = await manager.GetFromJsonAsync<JsonElement>("/api/v1/support/messages?status=Resolved");
        resolvedOnly.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Resolve_AsManagingDirector_FlipsStatusAndRecordsResolver()
    {
        var engineer = await AuthedClientAsync(802);
        var created = await (await engineer.PostAsJsonAsync("/api/v1/support/messages", ValidMessage))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt32();

        var director = await AuthedClientAsync(804);

        var resolved = await director.PatchAsJsonAsync($"/api/v1/support/messages/{id}/status", new { resolved = true });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resolved.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Resolved");
        body.GetProperty("resolvedByName").GetString().Should().Be("Dana Director");
        body.GetProperty("resolvedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);

        var openCount = await director.GetFromJsonAsync<int>("/api/v1/support/messages/open-count");
        openCount.Should().Be(0);
    }

    [Fact]
    public async Task Resolve_AsManager_Returns403()
    {
        var engineer = await AuthedClientAsync(802);
        var created = await (await engineer.PostAsJsonAsync("/api/v1/support/messages", ValidMessage))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt32();

        var manager = await AuthedClientAsync(801);
        var res = await manager.PatchAsJsonAsync($"/api/v1/support/messages/{id}/status", new { resolved = true });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Resolve_OwnMessage_Returns400_EvenForTheManagingDirector()
    {
        var director = await AuthedClientAsync(804);
        var created = await (await director.PostAsJsonAsync("/api/v1/support/messages", ValidMessage))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt32();

        var own = await director.PatchAsJsonAsync($"/api/v1/support/messages/{id}/status", new { resolved = true });
        own.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A different Managing Director can.
        var otherDirector = await AuthedClientAsync(805);
        var ok = await otherDirector.PatchAsJsonAsync($"/api/v1/support/messages/{id}/status", new { resolved = true });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resolve_AsEngineer_Returns403()
    {
        var engineer = await AuthedClientAsync(802);
        var created = await (await engineer.PostAsJsonAsync("/api/v1/support/messages", ValidMessage))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt32();

        var res = await engineer.PatchAsJsonAsync($"/api/v1/support/messages/{id}/status", new { resolved = true });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> AuthedClientAsync(int employeeNumber)
    {
        var anon = _factory.CreateClient();
        var login = await anon.PostAsJsonAsync("/api/v1/auth/login", new { employeeNumber, password = "Password1!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
