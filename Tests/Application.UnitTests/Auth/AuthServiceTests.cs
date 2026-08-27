using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Application.Services.Auth;
using Application.Validators.Auth;
using FluentAssertions;
using Moq;

namespace Application.UnitTests.Auth;

public class AuthServiceTests
{
    private static readonly AccountProjection SampleAccount = new(
        EmployeeNumber: 42,
        Name: "Ada Lovelace",
        Email: "ada@hmt.test",
        Role: "Manager",
        RankLevel: 2,
        SuperiorEmployeeNumber: 1,
        IsApprover: true,
        IsActive: true);

    private static AuthService CreateSut(
        Mock<IAccountStore> accountStore,
        Mock<ITokenService>? tokenService = null,
        Mock<IPasswordService>? passwordService = null)
    {
        tokenService ??= new Mock<ITokenService>();
        passwordService ??= new Mock<IPasswordService>();
        return new AuthService(
            accountStore.Object,
            tokenService.Object,
            passwordService.Object,
            new ChangePasswordRequestValidator());
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenAndProfile()
    {
        var accountStore = new Mock<IAccountStore>();
        accountStore
            .Setup(s => s.VerifyCredentialsAsync(42, "correct-password"))
            .ReturnsAsync(AccountVerificationResult.Success(SampleAccount));

        var tokenService = new Mock<ITokenService>();
        var expiresAtUtc = DateTime.UtcNow.AddHours(8);
        tokenService.Setup(s => s.CreateToken(SampleAccount)).Returns(("jwt-token", expiresAtUtc));

        var sut = CreateSut(accountStore, tokenService);

        var result = await sut.LoginAsync(new LoginRequest(42, "correct-password"));

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("jwt-token");
        result.ExpiresAtUtc.Should().Be(expiresAtUtc);
        result.User.EmployeeNumber.Should().Be(42);
        result.User.Role.Should().Be("Manager");
        result.User.RankLevel.Should().Be(2);
        result.User.IsApprover.Should().BeTrue();
    }

    [Theory]
    [InlineData("Unknown employee number")]
    [InlineData("Inactive account")]
    [InlineData("Locked out")]
    [InlineData("Wrong password")]
    public async Task LoginAsync_AnyFailureReason_ReturnsNullWithoutLeakingWhich(string scenario)
    {
        _ = scenario;
        var accountStore = new Mock<IAccountStore>();
        accountStore
            .Setup(s => s.VerifyCredentialsAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(AccountVerificationResult.Failed);

        var sut = CreateSut(accountStore);

        var result = await sut.LoginAsync(new LoginRequest(42, "irrelevant"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentUserAsync_UnknownEmployee_ReturnsNull()
    {
        var accountStore = new Mock<IAccountStore>();
        accountStore.Setup(s => s.GetByEmployeeNumberAsync(999)).ReturnsAsync((AccountProjection?)null);

        var sut = CreateSut(accountStore);

        var result = await sut.GetCurrentUserAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentUserAsync_KnownEmployee_MapsAllFields()
    {
        var accountStore = new Mock<IAccountStore>();
        accountStore.Setup(s => s.GetByEmployeeNumberAsync(42)).ReturnsAsync(SampleAccount);

        var sut = CreateSut(accountStore);

        var result = await sut.GetCurrentUserAsync(42);

        result.Should().BeEquivalentTo(new CurrentUserDto(42, "Ada Lovelace", "ada@hmt.test", "Manager", 2, 1, true));
    }

    [Fact]
    public async Task GetCurrentUserAsync_ManagingDirectorWithNullSuperior_LoadsSafely()
    {
        var mdAccount = SampleAccount with { SuperiorEmployeeNumber = null, RankLevel = 4, Role = "Managing Director" };
        var accountStore = new Mock<IAccountStore>();
        accountStore.Setup(s => s.GetByEmployeeNumberAsync(1)).ReturnsAsync(mdAccount);

        var sut = CreateSut(accountStore);

        var result = await sut.GetCurrentUserAsync(1);

        result!.SuperiorEmployeeNumber.Should().BeNull();
    }
}
