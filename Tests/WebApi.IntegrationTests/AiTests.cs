using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Exceptions;
using Application.Interfaces.Ai;
using FluentAssertions;
using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WebApi.IntegrationTests;

/// <summary>
/// Plan §7 M5 integration checks: LLM stubbed with a canned response → correct draft;
/// LLM stub throws → fallback draft with WasFallback = true and a log row.
/// The stub replaces <see cref="ILlmClient"/> so no test ever touches the network.
/// </summary>
public class AiTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();
    private readonly StubLlmClient _llm = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 701, "Mia Manager", "mia.ai@hmt.test", "Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 702, "Eli Engineer", "eli.ai@hmt.test", "Engineer", "Password1!", superiorEmployeeNumber: 701);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task RequestAssistant_Anonymous_Returns401()
    {
        var response = await Client().PostAsJsonAsync("/api/v1/ai/request-assistant", new { text = "pens" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestAssistant_EmptyText_Returns400()
    {
        var client = await AuthedClientAsync(702);

        var response = await client.PostAsJsonAsync("/api/v1/ai/request-assistant", new { text = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestAssistant_CannedLlmResponse_ReturnsValidatedDraft()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var pen = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        _llm.Reply = $$"""{"items":[{"itemId":{{pen.Id}},"quantity":3},{"itemId":987654,"quantity":1}],"requiredByDate":null,"note":"Drafted."}""";
        var client = await AuthedClientAsync(702);

        var response = await client.PostAsJsonAsync("/api/v1/ai/request-assistant", new { text = "3 pens please" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("wasFallback").GetBoolean().Should().BeFalse();
        body.GetProperty("model").GetString().Should().Be("stub-model");
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("itemId").GetInt32().Should().Be(pen.Id);
        items[0].GetProperty("quantity").GetInt32().Should().Be(3);
        items[0].GetProperty("itemName").GetString().Should().Be(pen.ItemName);
        body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString())
            .Should().ContainSingle(w => w!.Contains("not in your catalogue"));

        _llm.LastUserText.Should().Be("3 pens please");
        _llm.LastSystemPrompt.Should().Contain(pen.ItemName).And.NotContain("3 pens please");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var log = await db.AiInteractionLogs.SingleAsync();
        log.EmployeeNumber.Should().Be(702);
        log.WasFallback.Should().BeFalse();
        log.Model.Should().Be("stub-model");
        log.DraftItemCount.Should().Be(1);
    }

    [Fact]
    public async Task RequestAssistant_LlmThrows_FallsBackAndLogsIt()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        _llm.Throw = new LlmUnavailableException("timeout");
        var client = await AuthedClientAsync(702);

        var response = await client.PostAsJsonAsync("/api/v1/ai/request-assistant", new { text = $"2 {item.ItemName}" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("wasFallback").GetBoolean().Should().BeTrue();
        body.GetProperty("items").GetArrayLength().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("quantity").GetInt32().Should().Be(2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var log = await db.AiInteractionLogs.SingleAsync();
        log.WasFallback.Should().BeTrue();
        log.FallbackReason.Should().Be("timeout");
    }

    [Fact]
    public async Task RequestAssistant_ItemAboveCallersRank_IsNotOfferedToModel()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var managerOnly = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 2);

        _llm.Reply = $$"""{"items":[{"itemId":{{managerOnly.Id}},"quantity":1}],"requiredByDate":null,"note":null}""";
        var client = await AuthedClientAsync(702); // Engineer, rank 1

        var response = await client.PostAsJsonAsync("/api/v1/ai/request-assistant", new { text = "anything" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        _llm.LastSystemPrompt.Should().NotContain(managerOnly.ItemName);
    }

    [Fact]
    public async Task UsageReport_Engineer_Returns403()
    {
        var client = await AuthedClientAsync(702);

        var response = await client.GetAsync("/api/v1/ai/usage-report");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UsageReport_Manager_ReturnsPagedLog()
    {
        _llm.Reply = """{"items":[],"requiredByDate":null,"note":null}""";
        var engineer = await AuthedClientAsync(702);
        await engineer.PostAsJsonAsync("/api/v1/ai/request-assistant", new { text = "hello" });

        var manager = await AuthedClientAsync(701);
        var response = await manager.GetAsync("/api/v1/ai/usage-report");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("employeeNumber").GetInt32().Should().Be(702);
        body.GetProperty("items")[0].GetProperty("userText").GetString().Should().Be("hello");
    }

    private HttpClient Client() =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILlmClient>();
                services.AddSingleton<ILlmClient>(_llm);
            })).CreateClient();

    private async Task<HttpClient> AuthedClientAsync(int employeeNumber)
    {
        var anonymous = Client();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { employeeNumber, password = "Password1!" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class StubLlmClient : ILlmClient
    {
        public string Reply { get; set; } = """{"items":[],"requiredByDate":null,"note":null}""";
        public Exception? Throw { get; set; }
        public string? LastSystemPrompt { get; private set; }
        public string? LastUserText { get; private set; }

        public bool IsConfigured => true;

        public Task<LlmCompletion> DraftRequestAsync(string systemPrompt, string userText, CancellationToken cancellationToken = default)
        {
            LastSystemPrompt = systemPrompt;
            LastUserText = userText;
            if (Throw is not null) throw Throw;
            return Task.FromResult(new LlmCompletion(Reply, "stub-model", 10, 5));
        }
    }
}
