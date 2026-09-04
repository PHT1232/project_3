using Application.DTOs.Common;
using Application.DTOs.Users;
using Application.Interfaces.Users;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

public class IdentityUserStore(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager) : IUserStore
{
    public async Task<PagedResult<UserDto>> GetUsersAsync(int page, int pageSize, string? role, string? location)
    {
        var query = userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(u => u.Location == location);
        }

        var users = await query.OrderBy(u => u.Id).ToListAsync();

        if (!string.IsNullOrWhiteSpace(role))
        {
            var idsInRole = (await userManager.GetUsersInRoleAsync(role)).Select(u => u.Id).ToHashSet();
            users = users.Where(u => idsInRole.Contains(u.Id)).ToList();
        }

        var totalCount = users.Count;
        var page1 = users.Skip((page - 1) * pageSize).Take(pageSize);

        var dtos = new List<UserDto>();
        foreach (var user in page1)
        {
            dtos.Add(await ToDtoAsync(user));
        }

        return new PagedResult<UserDto>(dtos, page, pageSize, totalCount);
    }

    public async Task<UserDto?> GetByEmployeeNumberAsync(int employeeNumber)
    {
        var user = await userManager.FindByIdAsync(employeeNumber.ToString());
        return user is null ? null : await ToDtoAsync(user);
    }

    public Task<bool> RoleExistsAsync(string role) => roleManager.RoleExistsAsync(role);

    public async Task<int?> GetRoleRankLevelAsync(string role) =>
        (await roleManager.FindByNameAsync(role))?.RankLevel;

    public async Task<bool> EmployeeExistsAsync(int employeeNumber) =>
        await userManager.FindByIdAsync(employeeNumber.ToString()) is not null;

    public async Task<bool> EmailExistsAsync(string email, int? excludeEmployeeNumber)
    {
        var existing = await userManager.FindByEmailAsync(email);
        return existing is not null && existing.Id != excludeEmployeeNumber;
    }

    public async Task<int?> GetSuperiorEmployeeNumberAsync(int employeeNumber)
    {
        var user = await userManager.FindByIdAsync(employeeNumber.ToString());
        return user?.SuperiorEmployeeNumber;
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        var superior = request.SuperiorEmployeeNumber == 0 ? (int?)null : request.SuperiorEmployeeNumber;

        var user = new ApplicationUser
        {
            Id = request.EmployeeNumber,
            UserName = request.EmployeeNumber.ToString(),
            Email = request.Email,
            Name = request.Name,
            Grade = request.Grade,
            Location = request.Location,
            SuperiorEmployeeNumber = superior,
            IsActive = true,
        };

        var createResult = await userManager.CreateAsync(user, request.InitialPassword);
        ThrowIfFailed(createResult);

        var roleResult = await userManager.AddToRoleAsync(user, request.Role);
        ThrowIfFailed(roleResult);

        return await ToDtoAsync(user);
    }

    public async Task<UserDto?> UpdateUserAsync(int employeeNumber, UpdateUserRequest request)
    {
        var user = await userManager.FindByIdAsync(employeeNumber.ToString());
        if (user is null)
        {
            return null;
        }

        user.Name = request.Name;
        user.Email = request.Email;
        user.Grade = request.Grade;
        user.Location = request.Location;
        user.SuperiorEmployeeNumber = request.SuperiorEmployeeNumber == 0 ? null : request.SuperiorEmployeeNumber;

        ThrowIfFailed(await userManager.UpdateAsync(user));

        var currentRoles = await userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(request.Role))
        {
            if (currentRoles.Count > 0)
            {
                ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, currentRoles));
            }

            ThrowIfFailed(await userManager.AddToRoleAsync(user, request.Role));
        }

        return await ToDtoAsync(user);
    }

    public async Task<UserDto?> SetStatusAsync(int employeeNumber, bool isActive)
    {
        var user = await userManager.FindByIdAsync(employeeNumber.ToString());
        if (user is null)
        {
            return null;
        }

        user.IsActive = isActive;
        ThrowIfFailed(await userManager.UpdateAsync(user));

        if (!isActive)
        {
            // Belt-and-suspenders alongside the OnTokenValidated IsActive check (Program.cs):
            // rotate the security stamp so any Identity-level session state is invalidated too.
            await userManager.UpdateSecurityStampAsync(user);
        }

        return await ToDtoAsync(user);
    }

    public async Task<IReadOnlyList<UserDto>> GetSubordinatesAsync(int employeeNumber)
    {
        var subordinates = await userManager.Users
            .Where(u => u.SuperiorEmployeeNumber == employeeNumber)
            .OrderBy(u => u.Id)
            .ToListAsync();

        var dtos = new List<UserDto>();
        foreach (var user in subordinates)
        {
            dtos.Add(await ToDtoAsync(user));
        }

        return dtos;
    }

    private async Task<UserDto> ToDtoAsync(ApplicationUser user)
    {
        var roleName = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        var role = roleName.Length > 0 ? await roleManager.FindByNameAsync(roleName) : null;

        return new UserDto(
            user.Id,
            user.Name,
            user.Email!,
            roleName,
            role?.RankLevel ?? 0,
            user.SuperiorEmployeeNumber,
            user.Grade,
            user.Location,
            user.IsActive);
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(
                result.Errors.Select(e => new ValidationFailure(string.Empty, e.Description)));
        }
    }
}
