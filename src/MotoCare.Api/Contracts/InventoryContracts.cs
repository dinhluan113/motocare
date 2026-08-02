using System.ComponentModel.DataAnnotations;
using MotoCare.Api.Domain;

namespace MotoCare.Api.Contracts;

public sealed record StockMovementRequest(
    [Required] string PartId,
    InventoryTransactionType Type,
    [Range(typeof(decimal), "0.0001", "999999999")] decimal Quantity,
    [Range(typeof(decimal), "0", "999999999999")] decimal UnitCost,
    string? ReferenceType,
    string? ReferenceId,
    [MaxLength(2_000)] string? Notes,
    string? SupplierId = null,
    DateTime? TransactionDate = null,
    string? WarehouseLocationId = null);

public sealed record StockTransferRequest(
    [Required] string PartId,
    [Required] string FromWarehouseLocationId,
    [Required] string ToWarehouseLocationId,
    [Range(typeof(decimal), "0.0001", "999999999")] decimal Quantity,
    [Required, MaxLength(2_000)] string Notes);
