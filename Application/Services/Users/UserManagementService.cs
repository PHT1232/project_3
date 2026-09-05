using Application.DTOs.Common;
using Application.DTOs.Users;
using Application.Exceptions;
using Application.Interfaces.Auth;
using Application.Interfaces.Users;
using FluentValidation;
using FluentValidation.Results;

namespace Application.Services.Users;

public class UserManagementService(
    IUserStore userStore,
    ICurrentUserService currentUserService,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator) : IUserManagementService
{
    private const int MaxHierarchyWalk = 10;

    public Task<PagedResult<UserDto>> GetUsersAsync(int page, int pageSize, string? role, string? location) =>
        userStore.GetUsersAsync(page, pageSize, role, location);

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        await createValidator.ValidateAndThrowAsync(request);

        var failures = new List<ValidationFailure>();
        var superior = request.SuperiorEmployeeNumber == 0 ? (int?)null : request.SuperiorEmployeeNumber;

        if (!await userStore.RoleExistsAsync(request.Role))
        {
            failures.Add(new ValidationFailure(nameof(request.Role), $"Role '{request.Role}' does not exist."));
        }

        if (superior is not null)
        {
            if (superior == request.EmployeeNumber)
            {
                failures.Add(new ValidationFailure(
                    nameof(request.SuperiorEmployeeNumber), "A user cannot supervise themselves."));
            }
            else if (!await userStore.EmployeeExistsAsync(superior.Value))
            {
                failures.Add(new ValidationFailure(
                    nameof(request.SuperiorEmployeeNumber), $"Superior {superior} does not exist."));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        await EnsureActorCanAssignRoleAsync(request.Role);

        if (await userStore.EmployeeExistsAsync(request.EmployeeNumber))
        {
            throw new ConflictException($"Employee number {request.EmployeeNumber} already exists.");
        }

        if (await userStore.EmailExistsAsync(request.Email, null))
        {
            throw new ConflictException($"Email '{request.Email}' is already in use.");
        }

        return await userStore.CreateUserAsync(request);
    }

    public async Task<UserDto> UpdateUserAsync(int employeeNumber, UpdateUserRequest request)
    {
        await updateValidator.ValidateAndThrowAsync(request);

        var target = await userStore.GetByEmployeeNumberAsync(employeeNumber)
            ?? throw new NotFoundException($"Employee {employeeNumber} not found.");
        EnsureActorCanManageTarget(target);

        var failures = new List<ValidationFailure>();
        var superior = request.SuperiorEmployeeNumber == 0 ? (int?)null : request.SuperiorEmployeeNumber;

        if (!await userStore.RoleExistsAsync(request.Role))
        {
            failures.Add(new ValidationFailure(nameof(request.Role), $"Role '{request.Role}' does not exist."));
        }

        if (superior is not null)
        {
            if (superior == employeeNumber)
            {
                failures.Add(new ValidationFailure(
                    nameof(request.SuperiorEmployeeNumber), "A user cannot supervise themselves."));
            }
            else if (!await userStore.EmployeeExistsAsync(superior.Value))
            {
                failures.Add(new ValidationFailure(
                    nameof(request.SuperiorEmployeeNumber), $"Superior {superior} does not exist."));
            }
            else if (await CreatesCycleAsync(employeeNumber, superior.Value))
            {
                failures.Add(new ValidationFailure(
                    nameof(request.SuperiorEmployeeNumber), "This assignment would create a reporting cycle."));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        await EnsureActorCanAssignRoleAsync(request.Role);

        if (await userStore.EmailExistsAsync(request.Email, employeeNumber))
        {
            throw new ConflictException($"Email '{request.Email}' is already in use.");
        }

        var updated = await userStore.UpdateUserAsync(employeeNumber, request);
        return updated ?? throw new NotFoundException($"Employee {employeeNumber} not found.");
    }

    public async Task<UserDto> SetStatusAsync(int employeeNumber, bool isActive)
    {
        var target = await userStore.GetByEmployeeNumberAsync(employeeNumber)
            ?? throw new NotFoundException($"Employee {employeeNumber} not found.");
        EnsureActorCanManageTarget(target);

        var updated = await userStore.SetStatusAsync(employeeNumber, isActive);
        return updated ?? throw new NotFoundException($"Employee {employeeNumber} not found.");
    }

    public async Task<IReadOnlyList<UserDto>> GetSubordinatesAsync(int employeeNumber)
    {
        if (!await userStore.EmployeeExistsAsync(employeeNumber))
        {
            throw new NotFoundException($"Employee {employeeNumber} not found.");
        }

        return await userStore.GetSubordinatesAsync(employeeNumber);
    }

    /// <summary>
    /// Nobody may create, edit or deactivate an account at or above their own rank; the Managing
    /// Director, being the top rank, is exempt and may manage anyone (including another MD).
    ///
    /// Team ruling 2026-09-05: "a Manager should not be able to create a Managing Director."
    /// Stated as a rank comparison rather than a list of role names so it holds for every tier at
    /// once — it reproduces the Business-Manager rule this replaces exactly (a BM is still refused
    /// BM and MD accounts and still allowed Manager and Engineer ones) and extends the same
    /// principle downward.
    ///
    /// <b>Why this exists when the controller already guards it.</b> Today every write endpoint on
    /// UsersController is <c>RequireBusinessManager</c>, so a Manager is refused before reaching
    /// this class at all. That makes this the second of the two layers CLAUDE.md principle #9
    /// requires ("a policy on the controller *and* a row-level check inside the service"): if the
    /// controller policy is ever relaxed to RequireManager, the rule still holds here instead of
    /// silently opening up. A controller/service rank mismatch is not hypothetical — one was found
    /// on the Support Inbox route during the same audit.
    /// </summary>
    private const int ManagingDirectorRankLevel = 4;

    private void EnsureActorCanManageTarget(UserDto target) =>
        EnsureActorOutranks(target.RankLevel, $"manage the account of a {target.Role}");

    private async Task EnsureActorCanAssignRoleAsync(string role)
    {
        // Null means the role does not exist. Treated as rank 0 so this guard stays silent and
        // the caller's own role-existence validation reports the real problem, which is what the
        // previous `>= 3` comparison on an int? did too.
        var requestedRankLevel = await userStore.GetRoleRankLevelAsync(role) ?? 0;
        EnsureActorOutranks(requestedRankLevel, $"assign the {role} role");
    }

    private void EnsureActorOutranks(int targetRankLevel, string attemptedAction)
    {
        var actorRankLevel = currentUserService.RankLevel ?? 0;

        if (actorRankLevel >= ManagingDirectorRankLevel)
        {
            return; // Top of the hierarchy — nobody outranks them, so nothing to refuse.
        }

        if (targetRankLevel >= actorRankLevel)
        {
            throw new ForbiddenException(
                $"You cannot {attemptedAction}: it is at or above your own level. "
                + "Ask someone more senior.");
        }
    }

    /// <summary>Walks at most 10 superior links (Plan §7) looking for a path back to employeeNumber.</summary>
    private async Task<bool> CreatesCycleAsync(int employeeNumber, int candidateSuperior)
    {
        var current = (int?)candidateSuperior;
        for (var i = 0; i < MaxHierarchyWalk && current is not null; i++)
        {
            if (current == employeeNumber)
            {
                return true;
            }

            current = await userStore.GetSuperiorEmployeeNumberAsync(current.Value);
        }

        return false;
    }
}
