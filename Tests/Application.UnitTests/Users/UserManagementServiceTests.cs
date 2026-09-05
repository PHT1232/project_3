using Application.DTOs.Users;
using Application.Exceptions;
using Application.Interfaces.Auth;
using Application.Interfaces.Users;
using Application.Services.Users;
using Application.Validators.Users;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace Application.UnitTests.Users;

public class UserManagementServiceTests
{
    private static UserManagementService CreateSut(Mock<IUserStore> userStore, int rankLevel = 4)
    {
        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(s => s.RankLevel).Returns(rankLevel);

        return new UserManagementService(
            userStore.Object,
            currentUserService.Object,
            new CreateUserRequestValidator(),
            new UpdateUserRequestValidator());
    }

    private static CreateUserRequest ValidCreateRequest(int superior = 0) => new(
        EmployeeNumber: 10,
        Name: "Jane Doe",
        Email: "jane@hmt.test",
        Role: "Engineer",
        SuperiorEmployeeNumber: superior,
        InitialPassword: "Password1",
        Grade: null,
        Location: null);

    [Fact]
    public async Task CreateUserAsync_SelfSupervision_ThrowsValidationException()
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.RoleExistsAsync("Engineer")).ReturnsAsync(true);

        var sut = CreateSut(userStore);
        var request = ValidCreateRequest(superior: 10);

        var act = () => sut.CreateUserAsync(request);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.ErrorMessage.Contains("cannot supervise themselves")));
    }

    [Fact]
    public async Task CreateUserAsync_UnknownRole_ThrowsValidationException()
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.RoleExistsAsync("Engineer")).ReturnsAsync(false);

        var sut = CreateSut(userStore);

        var act = () => sut.CreateUserAsync(ValidCreateRequest());

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmployeeNumber_ThrowsConflictException()
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.RoleExistsAsync("Engineer")).ReturnsAsync(true);
        userStore.Setup(s => s.EmployeeExistsAsync(10)).ReturnsAsync(true);

        var sut = CreateSut(userStore);

        var act = () => sut.CreateUserAsync(ValidCreateRequest());

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ThrowsConflictException()
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.RoleExistsAsync("Engineer")).ReturnsAsync(true);
        userStore.Setup(s => s.EmployeeExistsAsync(10)).ReturnsAsync(false);
        userStore.Setup(s => s.EmailExistsAsync("jane@hmt.test", null)).ReturnsAsync(true);

        var sut = CreateSut(userStore);

        var act = () => sut.CreateUserAsync(ValidCreateRequest());

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateUserAsync_ApiSuperiorZero_MapsToNullSuperior()
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.RoleExistsAsync("Engineer")).ReturnsAsync(true);
        userStore.Setup(s => s.EmployeeExistsAsync(10)).ReturnsAsync(false);
        userStore.Setup(s => s.EmailExistsAsync("jane@hmt.test", null)).ReturnsAsync(false);

        CreateUserRequest? captured = null;
        userStore
            .Setup(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>()))
            .Callback<CreateUserRequest>(r => captured = r)
            .ReturnsAsync(new UserDto(10, "Jane Doe", "jane@hmt.test", "Engineer", 1, null, null, null, true));

        var sut = CreateSut(userStore);

        await sut.CreateUserAsync(ValidCreateRequest(superior: 0));

        captured!.SuperiorEmployeeNumber.Should().Be(0);
        userStore.Verify(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_UnknownEmployee_ThrowsNotFoundException()
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.EmployeeExistsAsync(99)).ReturnsAsync(false);

        var sut = CreateSut(userStore);
        var request = new UpdateUserRequest("Jane", "jane@hmt.test", "Engineer", 0, null, null);

        var act = () => sut.UpdateUserAsync(99, request);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateUserAsync_DirectCycle_ThrowsValidationException()
    {
        // 5's superior is 10; assigning 10's superior to 5 would create a 2-node cycle.
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.EmployeeExistsAsync(10)).ReturnsAsync(true);
        userStore.Setup(s => s.GetByEmployeeNumberAsync(10))
            .ReturnsAsync(new UserDto(10, "Jane", "jane@hmt.test", "Engineer", 1, null, null, null, true));
        userStore.Setup(s => s.RoleExistsAsync("Engineer")).ReturnsAsync(true);
        userStore.Setup(s => s.EmployeeExistsAsync(5)).ReturnsAsync(true);
        userStore.Setup(s => s.GetSuperiorEmployeeNumberAsync(5)).ReturnsAsync(10);

        var sut = CreateSut(userStore);
        var request = new UpdateUserRequest("Jane", "jane@hmt.test", "Engineer", 5, null, null);

        var act = () => sut.UpdateUserAsync(10, request);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.ErrorMessage.Contains("cycle")));
    }

    [Fact]
    public async Task UpdateUserAsync_LongerCycleBeyondTenHops_IsNotFlagged()
    {
        // Chain of 11 hops back to the target — walk caps at 10, so this must NOT throw.
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.EmployeeExistsAsync(1)).ReturnsAsync(true);
        userStore.Setup(s => s.GetByEmployeeNumberAsync(1))
            .ReturnsAsync(new UserDto(1, "Jane", "jane@hmt.test", "Engineer", 1, null, null, null, true));
        userStore.Setup(s => s.RoleExistsAsync("Engineer")).ReturnsAsync(true);
        userStore.Setup(s => s.EmployeeExistsAsync(2)).ReturnsAsync(true);
        userStore.Setup(s => s.EmailExistsAsync("jane@hmt.test", 1)).ReturnsAsync(false);

        // 2 -> 3 -> 4 -> ... -> 12 -> 1 (11 hops back to employee 1)
        for (var i = 2; i <= 12; i++)
        {
            userStore.Setup(s => s.GetSuperiorEmployeeNumberAsync(i)).ReturnsAsync(i + 1 <= 12 ? i + 1 : 1);
        }

        userStore
            .Setup(s => s.UpdateUserAsync(1, It.IsAny<UpdateUserRequest>()))
            .ReturnsAsync(new UserDto(1, "Jane", "jane@hmt.test", "Engineer", 1, 2, null, null, true));

        var sut = CreateSut(userStore);
        var request = new UpdateUserRequest("Jane", "jane@hmt.test", "Engineer", 2, null, null);

        var result = await sut.UpdateUserAsync(1, request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateUserAsync_BusinessManager_CannotManageBusinessManager()
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.GetByEmployeeNumberAsync(99))
            .ReturnsAsync(new UserDto(99, "Other BM", "other.bm@hmt.test", "Business Manager", 3, null, null, null, true));

        var sut = CreateSut(userStore, rankLevel: 3);
        var request = new UpdateUserRequest("Other BM", "other.bm@hmt.test", "Business Manager", 0, null, null);

        var act = () => sut.UpdateUserAsync(99, request);

        await act.Should().ThrowAsync<ForbiddenException>();
        userStore.Verify(s => s.UpdateUserAsync(It.IsAny<int>(), It.IsAny<UpdateUserRequest>()), Times.Never);
    }

    [Fact]
    public async Task GetSubordinatesAsync_UnknownEmployee_ThrowsNotFoundException()
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.EmployeeExistsAsync(99)).ReturnsAsync(false);

        var sut = CreateSut(userStore);

        var act = () => sut.GetSubordinatesAsync(99);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---- Rank guard: nobody creates an account at or above their own level ------------------
    //
    // Team ruling 2026-09-05: "a Manager should not be able to create a Managing Director."
    // UsersController's write endpoints are RequireBusinessManager today, so a Manager never
    // reaches this service — these tests pin the rule at the second layer CLAUDE.md #9 requires,
    // so relaxing that policy later cannot silently reopen the hole.

    [Theory]
    [InlineData(2, "Managing Director", 4)]   // the ruling, stated directly
    [InlineData(2, "Business Manager", 3)]
    [InlineData(2, "Manager", 2)]             // equal rank is also refused
    [InlineData(3, "Managing Director", 4)]
    [InlineData(3, "Business Manager", 3)]
    public async Task CreateUserAsync_RoleAtOrAboveActorsOwnRank_ThrowsForbidden(
        int actorRank, string requestedRole, int requestedRank)
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.RoleExistsAsync(requestedRole)).ReturnsAsync(true);
        userStore.Setup(s => s.GetRoleRankLevelAsync(requestedRole)).ReturnsAsync(requestedRank);

        var sut = CreateSut(userStore, rankLevel: actorRank);
        var request = ValidCreateRequest() with { Role = requestedRole };

        var act = () => sut.CreateUserAsync(request);

        await act.Should().ThrowAsync<ForbiddenException>();
        userStore.Verify(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>()), Times.Never,
            "nothing may be written when the rank guard refuses");
    }

    [Theory]
    [InlineData(2, "Engineer", 1)]            // a Manager may still create below themselves
    [InlineData(3, "Manager", 2)]             // unchanged Business Manager behaviour
    [InlineData(3, "Engineer", 1)]
    public async Task CreateUserAsync_RoleBelowActorsOwnRank_IsAllowed(
        int actorRank, string requestedRole, int requestedRank)
    {
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.RoleExistsAsync(requestedRole)).ReturnsAsync(true);
        userStore.Setup(s => s.GetRoleRankLevelAsync(requestedRole)).ReturnsAsync(requestedRank);
        userStore.Setup(s => s.EmployeeExistsAsync(It.IsAny<int>())).ReturnsAsync(false);
        userStore.Setup(s => s.EmailExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(false);
        userStore.Setup(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>()))
            .ReturnsAsync(new UserDto(10, "Jane Doe", "jane@hmt.test", requestedRole, requestedRank, null, null, null, true));

        var sut = CreateSut(userStore, rankLevel: actorRank);
        var request = ValidCreateRequest() with { Role = requestedRole };

        await sut.CreateUserAsync(request);

        userStore.Verify(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_ManagingDirectorCreatingAnotherManagingDirector_IsAllowed()
    {
        // The top rank is exempt: an "at or above your own level" rule applied literally would
        // stop the MD creating a successor, and there is nobody above them to do it instead.
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.RoleExistsAsync("Managing Director")).ReturnsAsync(true);
        userStore.Setup(s => s.GetRoleRankLevelAsync("Managing Director")).ReturnsAsync(4);
        userStore.Setup(s => s.EmployeeExistsAsync(It.IsAny<int>())).ReturnsAsync(false);
        userStore.Setup(s => s.EmailExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(false);
        userStore.Setup(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>()))
            .ReturnsAsync(new UserDto(10, "Jane Doe", "jane@hmt.test", "Managing Director", 4, null, null, null, true));

        var sut = CreateSut(userStore, rankLevel: 4);
        var request = ValidCreateRequest() with { Role = "Managing Director" };

        await sut.CreateUserAsync(request);

        userStore.Verify(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>()), Times.Once);
    }

    [Fact]
    public async Task SetStatusAsync_TargetAtOrAboveActorsRank_ThrowsForbidden()
    {
        // Deactivating is managing: a Manager must not be able to switch off an MD either.
        var userStore = new Mock<IUserStore>();
        userStore.Setup(s => s.GetByEmployeeNumberAsync(99))
            .ReturnsAsync(new UserDto(99, "The Boss", "boss@hmt.test", "Managing Director", 4, null, null, null, true));

        var sut = CreateSut(userStore, rankLevel: 2);

        var act = () => sut.SetStatusAsync(99, isActive: false);

        await act.Should().ThrowAsync<ForbiddenException>();
        userStore.Verify(s => s.SetStatusAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }
}
