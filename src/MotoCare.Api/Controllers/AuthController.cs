using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    MongoDbContext context,
    JwtTokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = request.Username.Trim().ToUpperInvariant();
        var users = context.Collection<AppUser>();
        var user = await users
            .Find(x => x.NormalizedUsername == normalized && x.IsActive && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized(ApiEnvelope.Fail("INVALID_CREDENTIALS", "Tên đăng nhập hoặc mật khẩu không đúng."));
        }

        var hasher = new PasswordHasher<AppUser>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(ApiEnvelope.Fail("INVALID_CREDENTIALS", "Tên đăng nhập hoặc mật khẩu không đúng."));
        }

        var update = Builders<AppUser>.Update.Set(x => x.LastLoginAt, DateTime.UtcNow);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            update = update.Set(x => x.PasswordHash, hasher.HashPassword(user, request.Password));
        }

        await users.UpdateOneAsync(x => x.Id == user.Id, update, cancellationToken: cancellationToken);
        return Ok(ApiEnvelope.Ok(tokenService.Create(user)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await context.Collection<AppUser>()
            .Find(x => x.Id == id && x.IsActive && !x.IsDeleted)
            .Project(x => new
            {
                x.Id,
                x.Username,
                x.FullName,
                x.EmployeeId,
                x.Roles,
                x.LastLoginAt
            })
            .FirstOrDefaultAsync(cancellationToken);
        return user is null
            ? Unauthorized(ApiEnvelope.Fail("USER_NOT_FOUND", "Tài khoản không còn hoạt động."))
            : Ok(ApiEnvelope.Ok(user));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var users = context.Collection<AppUser>();
        var user = await users.Find(x => x.Id == id && x.IsActive && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        var hasher = new PasswordHasher<AppUser>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
            == PasswordVerificationResult.Failed)
        {
            return BadRequest(ApiEnvelope.Fail("INVALID_PASSWORD", "Mật khẩu hiện tại không đúng."));
        }

        await users.UpdateOneAsync(
            x => x.Id == user.Id,
            Builders<AppUser>.Update
                .Set(x => x.PasswordHash, hasher.HashPassword(user, request.NewPassword))
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        return Ok(ApiEnvelope.Ok(new { changed = true }, "Đã đổi mật khẩu."));
    }
}
