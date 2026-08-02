using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/loyalty")]
[Authorize]
public sealed class LoyaltyController(
    MongoDbContext context,
    LoyaltyService service,
    AutoCodeService autoCodes) : ControllerBase
{
    [HttpGet("tiers")]
    public async Task<IActionResult> Tiers(CancellationToken cancellationToken)
    {
        var tiers = await context.Collection<LoyaltyTier>()
            .Find(x => !x.IsDeleted)
            .SortBy(x => x.Rank)
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(tiers));
    }

    [HttpGet("tiers/{id}")]
    public async Task<IActionResult> TierById(
        string id,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var tier = await context.Collection<LoyaltyTier>()
            .Find(x => x.Id == id && (includeDeleted || !x.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken);
        return tier is null
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy hạng thành viên."))
            : Ok(ApiEnvelope.Ok(tier));
    }

    [HttpPost("tiers")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> CreateTier(
        UpsertLoyaltyTierRequest request,
        CancellationToken cancellationToken)
    {
        var tier = new LoyaltyTier
        {
            Code = request.Code?.Trim().ToUpperInvariant() ?? string.Empty,
            Name = request.Name.Trim(),
            Rank = request.Rank,
            MinEligibleSpend = request.MinEligibleSpend,
            MinEarnedPoints = request.MinEarnedPoints,
            EarnRate = request.EarnRate,
            RedemptionValue = request.RedemptionValue,
            Benefits = request.Benefits?.Select(x => x.Trim()).Where(x => x.Length > 0).ToList() ?? [],
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };
        await autoCodes.EnsureAsync(tier, cancellationToken);
        await context.Collection<LoyaltyTier>().InsertOneAsync(tier, cancellationToken: cancellationToken);
        return Ok(ApiEnvelope.Ok(tier));
    }

    [HttpDelete("tiers/{id}")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> DeleteTier(string id, CancellationToken cancellationToken)
    {
        var update = Builders<LoyaltyTier>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var result = await context.Collection<LoyaltyTier>().UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            update,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1
            ? Ok(ApiEnvelope.Ok(new { id, deleted = true }))
            : NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy hạng thành viên."));
    }

    [HttpGet("rules")]
    public async Task<IActionResult> Rules(CancellationToken cancellationToken)
    {
        var rules = await context.Collection<LoyaltyRule>()
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(rules));
    }

    [HttpGet("rules/{id}")]
    public async Task<IActionResult> RuleById(
        string id,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var rule = await context.Collection<LoyaltyRule>()
            .Find(x => x.Id == id && (includeDeleted || !x.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken);
        return rule is null
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy quy tắc tích điểm."))
            : Ok(ApiEnvelope.Ok(rule));
    }

    [HttpPost("rules")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> CreateRule(
        UpsertLoyaltyRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EffectiveTo.HasValue && request.EffectiveTo <= request.EffectiveFrom)
        {
            throw new InvalidOperationException("Ngày kết thúc phải sau ngày hiệu lực.");
        }

        var rule = new LoyaltyRule
        {
            Name = request.Name.Trim(),
            SpendPerPoint = request.SpendPerPoint,
            RedemptionValue = request.RedemptionValue,
            MinimumRedemptionPoints = request.MinimumRedemptionPoints,
            MaximumRedemptionRate = request.MaximumRedemptionRate,
            PointExpiryDays = request.PointExpiryDays,
            EffectiveFrom = request.EffectiveFrom.ToUniversalTime(),
            EffectiveTo = request.EffectiveTo?.ToUniversalTime(),
            IsActive = request.IsActive
        };
        await context.Collection<LoyaltyRule>().InsertOneAsync(rule, cancellationToken: cancellationToken);
        return Ok(ApiEnvelope.Ok(rule));
    }

    [HttpDelete("rules/{id}")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> DeleteRule(string id, CancellationToken cancellationToken)
    {
        var update = Builders<LoyaltyRule>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var result = await context.Collection<LoyaltyRule>().UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            update,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1
            ? Ok(ApiEnvelope.Ok(new { id, deleted = true }))
            : NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy quy tắc tích điểm."));
    }

    [HttpPost("redemptions/preview")]
    [Authorize(Roles = SecurityRoles.Finance)]
    public async Task<IActionResult> Preview(
        LoyaltyRedemptionPreviewRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.PreviewRedemptionAsync(request, cancellationToken)));
    }

    [HttpGet("accounts/{customerId}/transactions")]
    public async Task<IActionResult> Transactions(
        string customerId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var items = await context.Collection<LoyaltyTransaction>()
            .Find(x => x.CustomerId == customerId && !x.IsDeleted)
            .SortByDescending(x => x.EffectiveAt)
            .Limit(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(items));
    }

    [HttpPost("accounts/{customerId}/adjustments")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> Adjust(
        string customerId,
        LoyaltyAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        return Ok(ApiEnvelope.Ok(await service.AdjustAsync(
            customerId,
            request,
            userId,
            cancellationToken)));
    }
}
