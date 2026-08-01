using System.ComponentModel.DataAnnotations;
using MotoCare.Api.Domain;

namespace MotoCare.Api.Contracts;

public sealed record CreateRepairOrderRequest(
    [Required] string CustomerId,
    [Required] string VehicleId,
    DateTime? ExpectedDeliveryAt,
    [Range(0, int.MaxValue)] int? OdometerIn,
    [MaxLength(50)] string? FuelLevel,
    [Required, MaxLength(4_000)] string VehicleCondition,
    [Required, MaxLength(4_000)] string CustomerRequest,
    [MaxLength(4_000)] string? Diagnosis,
    [MaxLength(4_000)] string? InternalNotes,
    RepairPriority Priority = RepairPriority.Normal,
    string? ServiceAdvisorId = null,
    IReadOnlyList<string>? VehicleConditionImages = null);

public sealed record UpdateVehicleConditionImagesRequest(
    IReadOnlyList<string> Images);

public sealed record UpdateRepairOrderOdometerRequest(
    [Range(0, int.MaxValue)] int OdometerIn);

public sealed record AddRepairOrderItemRequest(
    RepairItemType ItemType,
    string? ServiceId,
    string? PartId,
    [Required, MaxLength(500)] string Description,
    [Range(typeof(decimal), "0.01", "999999")] decimal Quantity,
    [Range(typeof(decimal), "0", "999999999999")] decimal UnitPrice,
    [Range(typeof(decimal), "0", "999999999999")] decimal DiscountAmount,
    string? AssignedEmployeeId,
    [MaxLength(2_000)] string? TechnicianNotes,
    DiscountType DiscountType = DiscountType.Amount,
    [Range(typeof(decimal), "0", "999999999999")] decimal DiscountValue = 0);

public sealed record UpdateRepairOrderItemRequest(
    RepairItemType ItemType,
    string? ServiceId,
    string? PartId,
    [Required, MaxLength(500)] string Description,
    [Range(typeof(decimal), "0.01", "999999")] decimal Quantity,
    [Range(typeof(decimal), "0", "999999999999")] decimal UnitPrice,
    [Range(typeof(decimal), "0", "999999999999")] decimal DiscountAmount,
    string? AssignedEmployeeId,
    [MaxLength(2_000)] string? TechnicianNotes,
    DiscountType DiscountType = DiscountType.Amount,
    [Range(typeof(decimal), "0", "999999999999")] decimal DiscountValue = 0);

public sealed record ChangeRepairStatusRequest(
    RepairOrderStatus Status,
    [MaxLength(2_000)] string? Note);

public sealed record UpdateRepairWorkRequest(
    WorkStatus Status,
    [MaxLength(2_000)] string? TechnicianNotes);
