using System.ComponentModel.DataAnnotations;

namespace MotoCare.Api.Contracts;

public sealed record LoyaltyRedemptionPreviewRequest(
    [Required] string CustomerId,
    [Range(typeof(decimal), "0.01", "999999999999")] decimal InvoiceAmount,
    [Range(0, long.MaxValue)] long RequestedPoints);

public sealed record LoyaltyAdjustmentRequest(
    [Range(typeof(long), "-999999999", "999999999")] long Points,
    [Required, MinLength(5), MaxLength(500)] string Reason,
    [Required, MaxLength(200)] string IdempotencyKey);

public sealed record UpsertLoyaltyTierRequest(
    [MaxLength(30)] string? Code,
    [Required, MaxLength(100)] string Name,
    [Range(1, 100)] int Rank,
    [Range(typeof(decimal), "0", "999999999999")] decimal MinEligibleSpend,
    [Range(0, long.MaxValue)] long MinEarnedPoints,
    [Range(typeof(decimal), "0", "100")] decimal EarnRate,
    [Range(typeof(decimal), "0.01", "999999999")] decimal RedemptionValue,
    IReadOnlyList<string>? Benefits,
    string? Description,
    bool IsActive = true);

public sealed record UpsertLoyaltyRuleRequest(
    [Required, MaxLength(100)] string Name,
    [Range(typeof(decimal), "1", "999999999")] decimal SpendPerPoint,
    [Range(typeof(decimal), "0.01", "999999999")] decimal RedemptionValue,
    [Range(1, long.MaxValue)] long MinimumRedemptionPoints,
    [Range(typeof(decimal), "0.01", "1")] decimal MaximumRedemptionRate,
    [Range(1, 3650)] int? PointExpiryDays,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive = true);

public sealed record LoyaltyRedemptionPreview(
    long AvailablePoints,
    long RequestedPoints,
    long AllowedPoints,
    decimal RedemptionValuePerPoint,
    decimal DiscountAmount,
    decimal MaximumDiscountAmount);
