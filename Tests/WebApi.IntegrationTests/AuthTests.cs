using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests;

/// <summary>
/// Each test gets its own factory/in-memory SQLite database (created fresh in InitializeAsync,
/// not shared via IClassFixture) so tests don't see each other's seeded users.
/// </summary>
public class AuthTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();

        await TestUserFactory.CreateUserAsync(
            _factory.Services, 101, "Ada Manager", "ada.manager@hmt.test", "Manager", "Password1!");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwtAndProfile()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { employeeNumber = 101, password = "Password1!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("user").GetProperty("employeeNumber").GetInt32().Should().Be(101);
        body.GetProperty("user").GetProperty("role").GetString().Should().Be("Manager");
        body.GetProperty("user").GetProperty("rankLevel").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsGeneric401ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { employeeNumber = 101, password = "wrong-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task Login_UnknownEmployeeNumber_ReturnsSameGeneric401AsWrongPassword()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { employeeNumber = 999999, password = "anything" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsProfile()
    {
        var token = await LoginAsync(101, "Password1!");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("employeeNumber").GetInt32().Should().Be(101);
        body.GetProperty("rankLevel").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task DeactivatedUser_CannotLogIn()
    {
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 102, "Bob Engineer", "bob.eng@hmt.test", "Engineer", "Password1!", isActive: false);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { employeeNumber = 102, password = "Password1!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PasswordIsHashed_NeverPlaintext()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByIdAsync("101");

        user!.PasswordHash.Should().NotBeNullOrEmpty();
        user.PasswordHash.Should().NotBe("Password1!");
    }

    private async Task<string> LoginAsync(int employeeNumber, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { employeeNumber, password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    // --- Sign in by email address -----------------------------------------------------------
    // Identity holds Email unique (RequireUniqueEmail) so it identifies exactly one account.
    // Every one of these must behave identically to the employee-number path.

    [Fact]
    public async Task Login_ValidEmail_ReturnsTheSameJwtAndProfileAsEmployeeNumber()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "ada.manager@hmt.test", password = "Password1!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("user").GetProperty("employeeNumber").GetInt32().Should().Be(101);
        body.GetProperty("user").GetProperty("rankLevel").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Login_EmailIsCaseAndWhitespaceInsensitive()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "  ADA.Manager@HMT.test  ", password = "Password1!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_EmailWithWrongPassword_ReturnsGeneric401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "ada.manager@hmt.test", password = "wrong-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().Should().NotContain("password");
    }

    [Fact]
    public async Task Login_UnregisteredEmail_ReturnsSameGeneric401AsWrongPassword()
    {
        var unknown = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "nobody@hmt.test", password = "Password1!" });
        var wrongPassword = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "ada.manager@hmt.test", password = "wrong-password" });

        // Same status, title and detail: an attacker cannot use this endpoint to discover which
        // email addresses are registered (Plan §9.2). traceId differs per request by design.
        unknown.StatusCode.Should().Be(wrongPassword.StatusCode);

        static async Task<(string? Title, string? Detail)> ProblemAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (body.GetProperty("title").GetString(), body.GetProperty("detail").GetString());
        }

        (await ProblemAsync(unknown)).Should().Be(await ProblemAsync(wrongPassword));
    }

    [Fact]
    public async Task Login_DeactivatedUserByEmail_CannotLogIn()
    {
        await TestUserFactory.CreateUserAsync(
            _factory.Services, 103, "Cara Engineer", "cara.eng@hmt.test", "Engineer", "Password1!", isActive: false);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "cara.eng@hmt.test", password = "Password1!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_TokenFromEmailSignIn_WorksOnProtectedEndpoints()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "ada.manager@hmt.test", password = "Password1!" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetAsync("/api/v1/auth/me");

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await me.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("employeeNumber").GetInt32().Should().Be(101);
    }

    [Theory]
    [InlineData("""{"password":"Password1!"}""")]                                              // no identifier
    [InlineData("""{"employeeNumber":101,"email":"ada.manager@hmt.test","password":"Password1!"}""")] // both
    public async Task Login_AmbiguousOrMalformedIdentifier_Returns400(string payload)
    {
        var response = await _client.PostAsync(
            "/api/v1/auth/login", new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
