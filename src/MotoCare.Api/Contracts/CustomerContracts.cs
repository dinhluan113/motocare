using System.ComponentModel.DataAnnotations;

namespace MotoCare.Api.Contracts;

public sealed record AddressDetailsRequest(
    [MaxLength(250)] string? AddressLine,
    [MaxLength(10)] string? CountryCode,
    [MaxLength(150)] string? CountryName,
    [MaxLength(30)] string? RegionCode,
    [MaxLength(150)] string? RegionName,
    [MaxLength(30)] string? AreaCode,
    [MaxLength(150)] string? AreaName);

public sealed record UpsertCustomerRequest(
    [Required, MaxLength(150)] string FullName,
    [Required, Phone, MaxLength(30)] string Phone,
    [EmailAddress, MaxLength(200)] string? Email,
    [MaxLength(500)] string? Address,
    AddressDetailsRequest? AddressDetails,
    DateTime? DateOfBirth,
    [MaxLength(30)] string? Gender,
    [MaxLength(30)] string? TaxCode,
    [MaxLength(2_000)] string? Notes,
    bool IsActive = true);

public sealed record UpsertVehicleRequest(
    [Required] string CustomerId,
    [Required] string VehicleModelId,
    [Required, MaxLength(30)] string LicensePlate,
    [MaxLength(100)] string? FrameNumber,
    [MaxLength(100)] string? EngineNumber,
    [Range(1900, 2200)] int? ManufactureYear,
    [MaxLength(100)] string? Color,
    [Range(0, int.MaxValue)] int? Odometer,
    DateTime? PurchaseDate,
    [MaxLength(2_000)] string? Notes,
    bool IsActive = true);
