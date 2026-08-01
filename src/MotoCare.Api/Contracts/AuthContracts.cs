using System.ComponentModel.DataAnnotations;

namespace MotoCare.Api.Contracts;

public sealed record LoginRequest(
    [Required, MinLength(3)] string Username,
    [Required, MinLength(6)] string Password);

public sealed record CreateUserRequest(
    [Required, MinLength(3)] string Username,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(150)] string FullName,
    string? EmployeeId,
    IReadOnlyList<string>? Roles);

public sealed record UpdateUserRequest(
    [Required, MaxLength(150)] string FullName,
    string? EmployeeId,
    [Required] string Role,
    bool IsActive,
    [MinLength(8)] string? NewPassword);

public sealed record ChangePasswordRequest(
    [Required, MinLength(6)] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);
