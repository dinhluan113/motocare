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
[Authorize(Roles = SecurityRoles.Administrator)]
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

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var allowedRoles = new[]
        {
            SecurityRoles.Administrator,
            SecurityRoles.Manager,
            SecurityRoles.Receptionist,
            SecurityRoles.Technician,
            SecurityRoles.Cashier
        };
        var roles = request.Roles?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        if (roles.Count == 0 || roles.Any(role => !allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
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
}
