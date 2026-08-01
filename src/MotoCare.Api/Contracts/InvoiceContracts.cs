using System.ComponentModel.DataAnnotations;
using MotoCare.Api.Domain;

namespace MotoCare.Api.Contracts;

public sealed record CreateInvoiceRequest(
    [Required] string RepairOrderId,
    [Range(typeof(decimal), "0", "999999999999")] decimal DiscountAmount,
    [Range(typeof(decimal), "0", "100")] decimal TaxRate,
    [MaxLength(2_000)] string? Notes,
    DiscountType DiscountType = DiscountType.Amount,
    [Range(typeof(decimal), "0", "999999999999")] decimal DiscountValue = 0,
    [MaxLength(50)] string? CouponCode = null);

public sealed record AddPaymentRequest(
    [Range(typeof(decimal), "0", "999999999999")] decimal Amount,
    [Required, MaxLength(50)] string Method,
    [Required, MaxLength(200)] string IdempotencyKey,
    [Range(0, long.MaxValue)] long LoyaltyPoints = 0,
    [MaxLength(200)] string? ReferenceCode = null,
    [MaxLength(2_000)] string? Notes = null);

public sealed record RefundInvoiceRequest(
    [Required, MinLength(5), MaxLength(500)] string Reason);

public sealed record CancelInvoiceRequest(
    [Required, MinLength(5), MaxLength(500)] string Reason);
