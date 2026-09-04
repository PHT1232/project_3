using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApi.IntegrationTests;

/// <summary>
/// Each test gets its own factory/in-memory SQLite database (created fresh in InitializeAsync,
/// not shared via IClassFixture) so tests don't see each other's seeded users.
/// </summary>
public class UsersTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 201, "Mia Manager", "mia.manager@hmt.test", "Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 202, "Eve Engineer", "eve.engineer@hmt.test", "Engineer", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 203, "Bea Business Manager", "bea.business.manager@hmt.test", "Business Manager", "Password1!");
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 1, "Mo MD", "mo.md@hmt.test", "Managing Director", "Password1!");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Engineer_CallingUsersList_Receives403()
    {
        var client = await AuthedClientAsync(202, "Password1!");

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Manager_CallingUsersList_Receives403()
    {
        var client = await AuthedClientAsync(201, "Password1!");

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BusinessManager_CanListAndCreateUsers()
    {
        var client = await AuthedClientAsync(203, "Password1!");

        var listResponse = await client.GetAsync("/api/v1/users");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createResponse = await client.PostAsJsonAsync("/api/v1/users", new
        {
            employeeNumber = 210,
            name = "New Hire",
            email = "new.hire@hmt.test",
            role = "Engineer",
            superiorEmployeeNumber = 201,
            initialPassword = "Password1!",
            grade = (string?)null,
            location = (string?)null,
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task BusinessManager_CreateUser_DuplicateEmployeeNumber_Returns409()
    {
        var client = await AuthedClientAsync(203, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/users", new
        {
            employeeNumber = 202,
            name = "Dup",
            email = "dup@hmt.test",
            role = "Engineer",
            superiorEmployeeNumber = 0,
            initialPassword = "Password1!",
            grade = (string?)null,
            location = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task BusinessManager_UpdateUser_CycleAssignment_Returns400()
    {
        var client = await AuthedClientAsync(203, "Password1!");

        var makeEveReportToMia = await client.PutAsJsonAsync("/api/v1/users/202", new
        {
            name = "Eve Engineer",
            email = "eve.engineer@hmt.test",
            role = "Engineer",
            superiorEmployeeNumber = 201,
            grade = (string?)null,
            location = (string?)null,
        });
        makeEveReportToMia.StatusCode.Should().Be(HttpStatusCode.OK);

        var createCycle = await client.PutAsJsonAsync("/api/v1/users/201", new
        {
            name = "Mia Manager",
            email = "mia.manager@hmt.test",
            role = "Manager",
            superiorEmployeeNumber = 202,
            grade = (string?)null,
            location = (string?)null,
        });

        createCycle.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BusinessManager_CannotCreateBusinessManager()
    {
        var client = await AuthedClientAsync(203, "Password1!");

        var response = await client.PostAsJsonAsync("/api/v1/users", new
        {
            employeeNumber = 204,
            name = "Another BM",
            email = "another.bm@hmt.test",
            role = "Business Manager",
            superiorEmployeeNumber = 1,
            initialPassword = "Password1!",
            grade = (string?)null,
            location = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BusinessManager_CannotUpdateAnotherBusinessManager()
    {
        var client = await AuthedClientAsync(203, "Password1!");

        var response = await client.PutAsJsonAsync("/api/v1/users/203", new
        {
            name = "Bea Manager",
            email = "bea.bm@hmt.test",
            role = "Business Manager",
            superiorEmployeeNumber = 1,
            grade = (string?)null,
            location = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BusinessManager_CannotChangeManagingDirectorStatus()
    {
        var client = await AuthedClientAsync(203, "Password1!");

        var response = await client.PatchAsJsonAsync("/api/v1/users/1/status", new
        {
            isActive = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ManagingDirector_WithNullSuperior_LoadsSafely()
    {
        var client = await AuthedClientAsync(201, "Password1!");

        var response = await client.GetAsync("/api/v1/users/1/subordinates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Engineer_CanViewOwnSubordinates_ButNotSomeoneElses()
    {
        var client = await AuthedClientAsync(202, "Password1!");

        var self = await client.GetAsync("/api/v1/users/202/subordinates");
        self.StatusCode.Should().Be(HttpStatusCode.OK);

        var someoneElse = await client.GetAsync("/api/v1/users/201/subordinates");
        someoneElse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
