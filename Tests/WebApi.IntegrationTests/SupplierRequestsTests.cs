using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests;

public class SupplierRequestsTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 501, "Mona Manager", "mona.sr@hmt.test", "Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 502, "Eddie Engineer", "eddie.sr@hmt.test", "Engineer", "Password1!");
        // Only this role may confirm that goods physically arrived.
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 503, "Bruno BizMgr", "bruno.sr@hmt.test", "Business Manager", "Password1!");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Submit_SingleItem_CreatesOneRequest()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1, quantityAvailable: 50);

        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 10, supplierId = (int?)null } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(1);

        var created = body[0];
        created.GetProperty("supplierId").GetInt32().Should().Be(supplier.Id);
        created.GetProperty("supplierName").GetString().Should().Be(supplier.Name);
        created.GetProperty("items").GetArrayLength().Should().Be(1);
        created.GetProperty("items")[0].GetProperty("quantity").GetInt32().Should().Be(10);
        // 10 x the 5.00 unit cost seeded by CatalogueTestData.
        created.GetProperty("totalCost").GetDecimal().Should().Be(50.00m);
    }

    [Fact]
    public async Task Submit_ItemsFromTwoSuppliers_CreatesOneRequestPerSupplier()
    {
        var (category, supplierA) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var supplierB = await SeedSupplierAsync("Supplier B");

        var itemA1 = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplierA.Id, 1);
        var itemA2 = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplierA.Id, 1);
        var itemB1 = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplierB, 1);

        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[]
            {
                new { itemId = itemA1.Id, quantity = 10, supplierId = (int?)null },
                new { itemId = itemB1.Id, quantity = 20, supplierId = (int?)null },
                new { itemId = itemA2.Id, quantity = 5, supplierId = (int?)null },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(2);

        var forA = body.EnumerateArray().Single(r => r.GetProperty("supplierId").GetInt32() == supplierA.Id);
        var forB = body.EnumerateArray().Single(r => r.GetProperty("supplierId").GetInt32() == supplierB);

        forA.GetProperty("items").GetArrayLength().Should().Be(2);
        forB.GetProperty("items").GetArrayLength().Should().Be(1);
        forB.GetProperty("items")[0].GetProperty("quantity").GetInt32().Should().Be(20);
    }

    /// <summary>The rule the whole feature hangs on: ordering is not receiving.</summary>
    [Fact]
    public async Task Submit_DoesNotChangeStockOrWriteLedgerRows()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1, quantityAvailable: 50);

        var client = await AuthedClientAsync(501, "Password1!");

        await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 999, supplierId = (int?)null } },
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var reloaded = await db.StationeryItems.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        reloaded.QuantityAvailable.Should().Be(50, "creating an order must never move stock");

        var ledgerRows = await db.StockTransactions.CountAsync(t => t.ItemId == item.Id);
        ledgerRows.Should().Be(0, "no goods have been received yet");
    }

    [Fact]
    public async Task Submit_ClientSuppliedSupplierIsIgnoredWhenItemHasOne()
    {
        var (category, realSupplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var otherSupplier = await SeedSupplierAsync("Attacker's Supplier");
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, realSupplier.Id, 1);

        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 3, supplierId = (int?)otherSupplier } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body[0].GetProperty("supplierId").GetInt32().Should()
            .Be(realSupplier.Id, "the database owns the supplier relationship, not the client");
    }

    [Fact]
    public async Task Submit_ItemWithoutSupplier_UsesClientSuppliedSupplier()
    {
        var (category, _) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var chosen = await SeedSupplierAsync("Chosen In Modal");
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplierId: null, minRankLevelToRequest: 1);

        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 4, supplierId = (int?)chosen } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body[0].GetProperty("supplierId").GetInt32().Should().Be(chosen);
    }

    [Fact]
    public async Task Submit_ItemWithoutSupplierAndNoChoice_Returns400()
    {
        var (category, _) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplierId: null, minRankLevelToRequest: 1);

        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 4, supplierId = (int?)null } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>One bad line must leave nothing behind — not even the valid lines' order.</summary>
    [Fact]
    public async Task Submit_OneInvalidItem_RollsBackEntireSubmission()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var good1 = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1);
        var good2 = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1);

        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[]
            {
                new { itemId = good1.Id, quantity = 5, supplierId = (int?)null },
                new { itemId = good2.Id, quantity = 5, supplierId = (int?)null },
                new { itemId = 999_999, quantity = 5, supplierId = (int?)null },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        (await db.SupplierRequests.CountAsync()).Should().Be(0);
        (await db.SupplierRequestItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Submit_EmptyCart_Returns400()
    {
        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Submit_NonPositiveQuantity_Returns400(int quantity)
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1);

        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = item.Id, quantity, supplierId = (int?)null } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submit_DuplicateItemLines_Returns400()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1);

        var client = await AuthedClientAsync(501, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[]
            {
                new { itemId = item.Id, quantity = 5, supplierId = (int?)null },
                new { itemId = item.Id, quantity = 7, supplierId = (int?)null },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submit_AsEngineer_Returns403()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1);

        var client = await AuthedClientAsync(502, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 5, supplierId = (int?)null } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Submit_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = 1, quantity = 5, supplierId = (int?)null } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_ReturnsPreviouslyCreatedRequests()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1);

        var client = await AuthedClientAsync(501, "Password1!");

        await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 8, supplierId = (int?)null } },
        });

        var response = await client.GetAsync("/api/v1/supplier-requests");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("items")[0].GetProperty("quantity").GetInt32().Should().Be(8);
    }

    // ---------------------------------------------------------------------------------------
    // Goods-arrival confirmation. Ordering never moves stock; only a Business Manager confirming
    // a physical delivery does, and only once.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task NewOrder_IsPendingArrival_AndStockIsUnchanged()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1, quantityAvailable: 50);

        var client = await AuthedClientAsync(501, "Password1!");

        var created = await (await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 30, supplierId = (int?)null } },
        })).Content.ReadFromJsonAsync<JsonElement>();

        created[0].GetProperty("status").GetString().Should().Be("PendingArrival");
        created[0].GetProperty("receivedAtUtc").ValueKind.Should().Be(JsonValueKind.Null);

        (await QuantityAsync(item.Id)).Should().Be(50, "the goods have not arrived yet");
    }

    [Fact]
    public async Task ConfirmArrival_AsBusinessManager_MarksReceivedAndRaisesStockOnce()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var itemA = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1, quantityAvailable: 50);
        var itemB = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1, quantityAvailable: 5);

        var manager = await AuthedClientAsync(501, "Password1!");
        var orderId = await CreateOrderAsync(manager, (itemA.Id, 30), (itemB.Id, 7));

        var businessManager = await AuthedClientAsync(503, "Password1!");
        var response = await businessManager.PostAsync($"/api/v1/supplier-requests/{orderId}/confirm-arrival", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Received");
        body.GetProperty("receivedByEmployeeNumber").GetInt32().Should().Be(503);
        body.GetProperty("receivedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);

        // Every line moves, and only by its own quantity.
        (await QuantityAsync(itemA.Id)).Should().Be(80);
        (await QuantityAsync(itemB.Id)).Should().Be(12);

        // One Receipt ledger row per line — the balance is never changed without one.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var rows = await db.StockTransactions.AsNoTracking()
            .Where(t => t.ItemId == itemA.Id || t.ItemId == itemB.Id)
            .ToListAsync();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(t => t.TxType == Core.Entities.StockTransactionType.Receipt);
        rows.Should().OnlyContain(t => t.CreatedByEmployeeNumber == 503);
        rows.Should().OnlyContain(t => t.Reference == $"Supplier order #{orderId}");
    }

    [Fact]
    public async Task ConfirmArrival_Twice_Returns409_AndDoesNotRaiseStockAgain()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1, quantityAvailable: 50);

        var manager = await AuthedClientAsync(501, "Password1!");
        var orderId = await CreateOrderAsync(manager, (item.Id, 30));

        var businessManager = await AuthedClientAsync(503, "Password1!");

        var first = await businessManager.PostAsync($"/api/v1/supplier-requests/{orderId}/confirm-arrival", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await QuantityAsync(item.Id)).Should().Be(80);

        var second = await businessManager.PostAsync($"/api/v1/supplier-requests/{orderId}/confirm-arrival", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await QuantityAsync(item.Id)).Should().Be(80, "a repeated confirmation must not add the stock twice");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        (await db.StockTransactions.CountAsync(t => t.ItemId == item.Id)).Should().Be(1);
    }

    [Theory]
    [InlineData(501)] // Manager — may raise an order, may not certify it arrived
    [InlineData(502)] // Engineer
    public async Task ConfirmArrival_AsUnauthorisedRole_Returns403_AndStockIsUnchanged(int employeeNumber)
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, 1, quantityAvailable: 50);

        var manager = await AuthedClientAsync(501, "Password1!");
        var orderId = await CreateOrderAsync(manager, (item.Id, 30));

        var client = await AuthedClientAsync(employeeNumber, "Password1!");
        var response = await client.PostAsync($"/api/v1/supplier-requests/{orderId}/confirm-arrival", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await QuantityAsync(item.Id)).Should().Be(50);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var order = await db.SupplierRequests.AsNoTracking().FirstAsync(r => r.Id == orderId);
        order.Status.Should().Be("PendingArrival");
    }

    [Fact]
    public async Task ConfirmArrival_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .PostAsync("/api/v1/supplier-requests/1/confirm-arrival", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConfirmArrival_UnknownOrder_Returns404()
    {
        var businessManager = await AuthedClientAsync(503, "Password1!");

        var response = await businessManager.PostAsync("/api/v1/supplier-requests/999999/confirm-arrival", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Creates an order as the given client and returns its id.</summary>
    private static async Task<int> CreateOrderAsync(HttpClient client, params (int ItemId, int Quantity)[] lines)
    {
        var response = await client.PostAsJsonAsync("/api/v1/supplier-requests", new
        {
            items = lines.Select(l => new { itemId = l.ItemId, quantity = l.Quantity, supplierId = (int?)null }),
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(1, "these tests order from a single supplier");
        return body[0].GetProperty("supplierRequestId").GetInt32();
    }

    private async Task<int> QuantityAsync(int itemId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        return (await db.StationeryItems.AsNoTracking().FirstAsync(i => i.Id == itemId)).QuantityAvailable;
    }

    private async Task<int> SeedSupplierAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var supplier = new Core.Entities.Supplier { Name = name, LeadTimeDays = 3, IsActive = true };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return supplier.Id;
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
