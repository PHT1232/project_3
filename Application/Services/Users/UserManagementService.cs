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
    private const int BusinessManagerRankLevel = 3;
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

    private void EnsureActorCanManageTarget(UserDto target)
    {
        if (currentUserService.RankLevel == BusinessManagerRankLevel
            && target.RankLevel >= BusinessManagerRankLevel)
        {
            throw new ForbiddenException("Business Managers cannot manage Business Manager or Managing Director accounts.");
        }
    }

    private async Task EnsureActorCanAssignRoleAsync(string role)
    {
        var requestedRankLevel = await userStore.GetRoleRankLevelAsync(role);
        if (currentUserService.RankLevel == BusinessManagerRankLevel
            && requestedRankLevel >= BusinessManagerRankLevel)
        {
            throw new ForbiddenException("Business Managers cannot assign Business Manager or Managing Director roles.");
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
