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

        // 604: A Manager who is neither the requestor nor the approver of anything below —
        // exists to prove Manager+ visibility comes from the ROLE, not ApplicationUser.RankLevel
        // (which TestUserFactory, like IdentityUserStore, never sets).
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 604, "Uninvolved Mgr", "unrelated.req@hmt.test", "Manager", "Password1!");
    }

    [Fact]
    public async Task GetById_UnrelatedManager_SeesRequest_RankComesFromRole()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var client602 = await AuthedClientAsync(602, "Password1!");
        var createRes = await client602.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 1 } }
        });
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();

        var client604 = await AuthedClientAsync(604, "Password1!");

        var byId = await client604.GetAsync($"/api/v1/requests/{requestId}");
        byId.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client604.GetAsync("/api/v1/requests");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
        listBody.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var summary = await client604.GetAsync("/api/v1/requests/dashboard-summary");
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        summaryBody.EnumerateObject().Sum(p => p.Value.GetInt32()).Should().BeGreaterThanOrEqualTo(1);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateRequest_ValidPayload_ReturnsCreatedWithDraftStatus()
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
        body.GetProperty("status").GetString().Should().Be("Draft");
        body.GetProperty("requestorEmployeeNumber").GetInt32().Should().Be(602);
        body.GetProperty("approverEmployeeNumber").GetInt32().Should().Be(601);
        body.GetProperty("totalEstimatedCost").GetDecimal().Should().Be(20.00m);
        body.GetProperty("items").GetArrayLength().Should().Be(1);
        body.GetProperty("statusHistory").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SubmitRequest_DraftRequest_TransitionsToPending()
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
        submitted.GetProperty("statusHistory")[1].GetProperty("fromStatus").GetString().Should().Be("Draft");
        submitted.GetProperty("statusHistory")[1].GetProperty("toStatus").GetString().Should().Be("Pending");

        // A Pending request cannot be submitted twice.
        var again = await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/submit", new
        {
            requestId,
            rowVersion = Guid.Parse(submitted.GetProperty("rowVersion").GetString()!)
        });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Draft_IsInvisibleToApprover_UntilSubmitted()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestor = await AuthedClientAsync(602, "Password1!");
        var approver = await AuthedClientAsync(601, "Password1!");

        var createRes = await requestor.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId = item.Id, quantity = 2 } }
        });
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();

        // Audit finding C1: "Save as Draft" used to land in the approver's queue immediately.
        var before = await (await approver.GetAsync("/api/v1/approvals/pending")).Content.ReadFromJsonAsync<JsonElement>();
        before.GetProperty("items").EnumerateArray()
            .Select(r => r.GetProperty("requestId").GetInt32())
            .Should().NotContain(requestId);

        await requestor.PostAsJsonAsync($"/api/v1/requests/{requestId}/submit", new
        {
            requestId,
            rowVersion = Guid.Parse(created.GetProperty("rowVersion").GetString()!)
        });

        var after = await (await approver.GetAsync("/api/v1/approvals/pending")).Content.ReadFromJsonAsync<JsonElement>();
        after.GetProperty("items").EnumerateArray()
            .Select(r => r.GetProperty("requestId").GetInt32())
            .Should().Contain(requestId);
    }

    [Fact]
    public async Task ApproveRequest_ModifiedLine_PersistsDecisionAndApprovedQuantity()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestor = await AuthedClientAsync(602, "Password1!");
        var approver = await AuthedClientAsync(601, "Password1!");

        var submitted = await CreateAndSubmitAsync(requestor, item.Id, quantity: 10);
        var requestId = submitted.GetProperty("requestId").GetInt32();
        var lineId = submitted.GetProperty("items")[0].GetProperty("requestItemId").GetInt32();

        var approveRes = await approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/approve", new
        {
            requestId,
            rowVersion = Guid.Parse(submitted.GetProperty("rowVersion").GetString()!),
            lineDecisions = new[] { new { requestItemId = lineId, decision = "modified", modifiedQuantity = (int?)4 } },
            comment = "Only 4 available this month"
        });

        approveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var decided = await approveRes.Content.ReadFromJsonAsync<JsonElement>();

        // Audit finding C2: the decision and reduced quantity used to be discarded.
        decided.GetProperty("status").GetString().Should().Be("PartiallyApproved");
        var line = decided.GetProperty("items")[0];
        line.GetProperty("decision").GetString().Should().Be("modified");
        line.GetProperty("approvedQuantity").GetInt32().Should().Be(4);
        line.GetProperty("quantity").GetInt32().Should().Be(10, "the requested quantity is history and must not be rewritten");
    }

    [Fact]
    public async Task ApproveRequest_RejectedLine_PersistsZeroApprovedQuantity()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestor = await AuthedClientAsync(602, "Password1!");
        var approver = await AuthedClientAsync(601, "Password1!");

        var submitted = await CreateAndSubmitAsync(requestor, item.Id, quantity: 3);
        var requestId = submitted.GetProperty("requestId").GetInt32();
        var lineId = submitted.GetProperty("items")[0].GetProperty("requestItemId").GetInt32();

        var approveRes = await approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/approve", new
        {
            requestId,
            rowVersion = Guid.Parse(submitted.GetProperty("rowVersion").GetString()!),
            lineDecisions = new[] { new { requestItemId = lineId, decision = "rejected", modifiedQuantity = (int?)null } },
            comment = "Not needed"
        });

        approveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var decided = await approveRes.Content.ReadFromJsonAsync<JsonElement>();
        decided.GetProperty("status").GetString().Should().Be("Rejected");
        decided.GetProperty("items")[0].GetProperty("decision").GetString().Should().Be("rejected");
        decided.GetProperty("items")[0].GetProperty("approvedQuantity").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ApproveRequest_DecisionForForeignLine_Returns409()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestor = await AuthedClientAsync(602, "Password1!");
        var approver = await AuthedClientAsync(601, "Password1!");

        var submitted = await CreateAndSubmitAsync(requestor, item.Id, quantity: 1);
        var requestId = submitted.GetProperty("requestId").GetInt32();

        var approveRes = await approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/approve", new
        {
            requestId,
            rowVersion = Guid.Parse(submitted.GetProperty("rowVersion").GetString()!),
            lineDecisions = new[] { new { requestItemId = 999_999, decision = "approved", modifiedQuantity = (int?)null } },
            comment = (string?)null
        });

        approveRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PendingApprovals_IncludesCancellationPendingRequests()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestor = await AuthedClientAsync(602, "Password1!");
        var approver = await AuthedClientAsync(601, "Password1!");

        var submitted = await CreateAndSubmitAsync(requestor, item.Id, quantity: 2);
        var requestId = submitted.GetProperty("requestId").GetInt32();
        var lineId = submitted.GetProperty("items")[0].GetProperty("requestItemId").GetInt32();

        var approved = await (await approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/approve", new
        {
            requestId,
            rowVersion = Guid.Parse(submitted.GetProperty("rowVersion").GetString()!),
            lineDecisions = new[] { new { requestItemId = lineId, decision = "approved", modifiedQuantity = (int?)null } },
            comment = (string?)null
        })).Content.ReadFromJsonAsync<JsonElement>();
        approved.GetProperty("status").GetString().Should().Be("Approved");

        var cancelRes = await requestor.PostAsJsonAsync($"/api/v1/requests/{requestId}/request-cancellation", new
        {
            requestId,
            rowVersion = Guid.Parse(approved.GetProperty("rowVersion").GetString()!),
            reason = "No longer needed"
        });
        cancelRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Audit finding C5: this list used to return Pending only, so the approver could never
        // find — and therefore never resolve — a cancellation request.
        var queue = await (await approver.GetAsync("/api/v1/approvals/pending")).Content.ReadFromJsonAsync<JsonElement>();
        var row = queue.GetProperty("items").EnumerateArray()
            .Single(r => r.GetProperty("requestId").GetInt32() == requestId);
        row.GetProperty("status").GetString().Should().Be("CancellationPending");
    }

    [Fact]
    public async Task RefuseCancellation_PartiallyApprovedRequest_RevertsToPartiallyApproved()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestor = await AuthedClientAsync(602, "Password1!");
        var approver = await AuthedClientAsync(601, "Password1!");

        var submitted = await CreateAndSubmitAsync(requestor, item.Id, quantity: 8);
        var requestId = submitted.GetProperty("requestId").GetInt32();
        var lineId = submitted.GetProperty("items")[0].GetProperty("requestItemId").GetInt32();

        var partial = await (await approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/approve", new
        {
            requestId,
            rowVersion = Guid.Parse(submitted.GetProperty("rowVersion").GetString()!),
            lineDecisions = new[] { new { requestItemId = lineId, decision = "modified", modifiedQuantity = (int?)3 } },
            comment = "Reduced"
        })).Content.ReadFromJsonAsync<JsonElement>();
        partial.GetProperty("status").GetString().Should().Be("PartiallyApproved");

        var pendingCancel = await (await requestor.PostAsJsonAsync($"/api/v1/requests/{requestId}/request-cancellation", new
        {
            requestId,
            rowVersion = Guid.Parse(partial.GetProperty("rowVersion").GetString()!),
            reason = "Changed my mind"
        })).Content.ReadFromJsonAsync<JsonElement>();
        pendingCancel.GetProperty("status").GetString().Should().Be("CancellationPending");

        // Audit finding C6: this used to come back "Approved" because StatusHistory wasn't loaded.
        var refuseRes = await approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/cancel-approval", new
        {
            requestId,
            rowVersion = Guid.Parse(pendingCancel.GetProperty("rowVersion").GetString()!),
            approved = false,
            reason = "Order already placed"
        });
        refuseRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var refused = await refuseRes.Content.ReadFromJsonAsync<JsonElement>();
        refused.GetProperty("status").GetString().Should().Be("PartiallyApproved");
        refused.GetProperty("items")[0].GetProperty("approvedQuantity").GetInt32().Should().Be(3, "the earlier decision is untouched");

        var last = refused.GetProperty("statusHistory").EnumerateArray().Last();
        last.GetProperty("fromStatus").GetString().Should().Be("CancellationPending");
        last.GetProperty("toStatus").GetString().Should().Be("PartiallyApproved");
    }

    [Fact]
    public async Task ApproveCancellation_ReasonOver500Chars_Returns400()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestor = await AuthedClientAsync(602, "Password1!");
        var approver = await AuthedClientAsync(601, "Password1!");

        var submitted = await CreateAndSubmitAsync(requestor, item.Id, quantity: 1);
        var requestId = submitted.GetProperty("requestId").GetInt32();
        var lineId = submitted.GetProperty("items")[0].GetProperty("requestItemId").GetInt32();

        var approved = await (await approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/approve", new
        {
            requestId,
            rowVersion = Guid.Parse(submitted.GetProperty("rowVersion").GetString()!),
            lineDecisions = new[] { new { requestItemId = lineId, decision = "approved", modifiedQuantity = (int?)null } },
            comment = (string?)null
        })).Content.ReadFromJsonAsync<JsonElement>();

        var pendingCancel = await (await requestor.PostAsJsonAsync($"/api/v1/requests/{requestId}/request-cancellation", new
        {
            requestId,
            rowVersion = Guid.Parse(approved.GetProperty("rowVersion").GetString()!),
            reason = (string?)null
        })).Content.ReadFromJsonAsync<JsonElement>();

        // Audit finding C6: ApproveCancellationCommandValidator was never injected, so this passed.
        var res = await approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/cancel-approval", new
        {
            requestId,
            rowVersion = Guid.Parse(pendingCancel.GetProperty("rowVersion").GetString()!),
            approved = true,
            reason = new string('x', 501)
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteRequest_AfterSubmit_Returns400AndKeepsRequest()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var requestor = await AuthedClientAsync(602, "Password1!");
        var submitted = await CreateAndSubmitAsync(requestor, item.Id, quantity: 1);
        var requestId = submitted.GetProperty("requestId").GetInt32();

        // Audit finding C4: a submitted request could be hard-deleted, taking its history with it.
        var deleteRes = await requestor.DeleteAsync($"/api/v1/requests/{requestId}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var stillThere = await requestor.GetAsync($"/api/v1/requests/{requestId}");
        stillThere.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Create then submit, returning the submitted (Pending) request body.</summary>
    private static async Task<JsonElement> CreateAndSubmitAsync(HttpClient requestor, int itemId, int quantity)
    {
        var createRes = await requestor.PostAsJsonAsync("/api/v1/requests", new
        {
            items = new[] { new { itemId, quantity } }
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("requestId").GetInt32();

        var submitRes = await requestor.PostAsJsonAsync($"/api/v1/requests/{requestId}/submit", new
        {
            requestId,
            rowVersion = Guid.Parse(created.GetProperty("rowVersion").GetString()!)
        });
        submitRes.StatusCode.Should().Be(HttpStatusCode.OK);
        return await submitRes.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task WithdrawRequest_PendingRequest_TransitionsToWithdrawn()
    {
        var (category, supplier) = await CatalogueTestData.SeedCategoryAndSupplierAsync(_factory.Services);
        var item = await CatalogueTestData.SeedItemAsync(_factory.Services, category.Id, supplier.Id, minRankLevelToRequest: 1);

        var client = await AuthedClientAsync(602, "Password1!");

        // 1. Create + submit (withdraw is a Pending-only transition; a Draft is deleted instead)
        var submitted = await CreateAndSubmitAsync(client, item.Id, quantity: 2);
        var requestId = submitted.GetProperty("requestId").GetInt32();
        var rowVersion = submitted.GetProperty("rowVersion").GetString();

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
    public async Task DeleteRequest_Draft_DeletesSuccessfully()
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
