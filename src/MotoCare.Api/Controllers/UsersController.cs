using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = SecurityRoles.Administrators)]
public sealed class UsersController(MongoDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var users = await context.Collection<AppUser>()
            .Find(x => !x.IsDeleted)
            .Project(x => new
            {
                x.Id,
                x.Username,
                x.FullName,
                x.EmployeeId,
                x.Roles,
                x.IsActive,
                x.LastLoginAt
            })
            .SortBy(x => x.Username)
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(users));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        string id,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Collection<AppUser>()
            .Find(x => x.Id == id && (includeDeleted || !x.IsDeleted))
            .Project(x => new
            {
                x.Id,
                x.Username,
                x.FullName,
                x.EmployeeId,
                x.Roles,
                x.IsActive,
                x.IsDeleted,
                x.LastLoginAt,
                x.CreatedAt,
                x.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
        return user is null
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy tài khoản."))
            : Ok(ApiEnvelope.Ok(user));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var allowedRoles = new[]
        {
            SecurityRoles.Admin,
            SecurityRoles.Manager,
            SecurityRoles.Employee
        };
        var roles = request.Roles?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        if (roles.Count != 1 || roles.Any(role => !allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return BadRequest(ApiEnvelope.Fail("INVALID_ROLES", "Vai trò không hợp lệ."));
        }

        var user = new AppUser
        {
            Username = request.Username.Trim(),
            NormalizedUsername = request.Username.Trim().ToUpperInvariant(),
            FullName = request.FullName.Trim(),
            EmployeeId = request.EmployeeId,
            Roles = roles
        };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, request.Password);
        await context.Collection<AppUser>().InsertOneAsync(user, cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(Get), ApiEnvelope.Ok(new
        {
            user.Id,
            user.Username,
            user.FullName,
            user.EmployeeId,
            user.Roles
        }));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var allowedRoles = new[] { SecurityRoles.Admin, SecurityRoles.Manager, SecurityRoles.Employee };
        if (!allowedRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(ApiEnvelope.Fail("INVALID_ROLE", "Vai trò không hợp lệ."));
        }
        var user = await context.Collection<AppUser>()
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");
        user.FullName = request.FullName.Trim();
        user.EmployeeId = request.EmployeeId;
        user.Roles = [request.Role];
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, request.NewPassword);
        }
        await context.Collection<AppUser>().ReplaceOneAsync(
            x => x.Id == user.Id,
            user,
            cancellationToken: cancellationToken);
        return Ok(ApiEnvelope.Ok(new
        {
            user.Id, user.Username, user.FullName, user.EmployeeId, user.Roles, user.IsActive
        }, "Đã cập nhật tài khoản."));
    }
}
