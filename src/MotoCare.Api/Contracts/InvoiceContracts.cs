using System.ComponentModel.DataAnnotations;

namespace MotoCare.Api.Contracts;

public sealed record CreateInvoiceRequest(
    [Required] string RepairOrderId,
    [Range(typeof(decimal), "0", "999999999999")] decimal DiscountAmount,
    [Range(typeof(decimal), "0", "100")] decimal TaxRate,
    [MaxLength(2_000)] string? Notes);

public sealed record AddPaymentRequest(
    [Range(typeof(decimal), "0", "999999999999")] decimal Amount,
    [Required, MaxLength(50)] string Method,
    [Required, MaxLength(200)] string IdempotencyKey,
    [Range(0, long.MaxValue)] long LoyaltyPoints = 0,
    [MaxLength(200)] string? ReferenceCode = null,
    [MaxLength(2_000)] string? Notes = null);

public sealed record RefundInvoiceRequest(
    [Required, MinLength(5), MaxLength(500)] string Reason);
