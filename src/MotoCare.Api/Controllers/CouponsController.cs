using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Controllers;

[Route("api/v1/coupons")]
public sealed class CouponsController(IMongoRepository<Coupon> repository)
    : CrudController<Coupon>(repository)
{
    protected override FilterDefinition<Coupon> BuildSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return Builders<Coupon>.Filter.Empty;
        var regex = new BsonRegularExpression(
            System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        return Builders<Coupon>.Filter.Or(
            Builders<Coupon>.Filter.Regex(x => x.Code, regex),
            Builders<Coupon>.Filter.Regex(x => x.Name, regex));
    }

    [Authorize(Roles = SecurityRoles.Administrators)]
    public override Task<IActionResult> Create(Coupon entity, CancellationToken cancellationToken) =>
        base.Create(entity, cancellationToken);

    [Authorize(Roles = SecurityRoles.Administrators)]
    public override async Task<IActionResult> Update(
        string id,
        Coupon entity,
        CancellationToken cancellationToken)
    {
        var current = await Repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy coupon.");
        entity.UsedCount = current.UsedCount;
        return await base.Update(id, entity, cancellationToken);
    }

    [Authorize(Roles = SecurityRoles.Administrators)]
    public override async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var coupon = await Repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy coupon.");
        coupon.IsActive = false;
        coupon.EndAt = DateTime.UtcNow;
        await Repository.ReplaceAsync(coupon, cancellationToken);
        return Ok(ApiEnvelope.Ok(
            new { id, expired = true },
            "Coupon đã được đánh dấu hết hạn."));
    }

    protected override void Prepare(Coupon entity)
    {
        entity.Code = entity.Code.Trim().ToUpperInvariant();
        entity.Name = entity.Name.Trim();
        entity.Description = entity.Description?.Trim();
        entity.CustomerIds = entity.CustomerIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        entity.StartAt = entity.StartAt?.ToUniversalTime();
        entity.EndAt = entity.EndAt?.ToUniversalTime();
    }

    protected override void ValidateBusinessRules(Coupon entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Name))
            throw new InvalidOperationException("Tên coupon là bắt buộc.");
        if (entity.DiscountValue <= 0
            || entity.DiscountType == DiscountType.Percentage && entity.DiscountValue > 100)
            throw new InvalidOperationException("Giá trị giảm coupon không hợp lệ.");
        if (entity.Audience == CouponAudience.MinimumOrder && entity.MinimumOrderAmount <= 0)
            throw new InvalidOperationException("Cần nhập giá trị đơn hàng tối thiểu.");
        if (entity.Audience == CouponAudience.SpecificCustomers && entity.CustomerIds.Count == 0)
            throw new InvalidOperationException("Cần chọn ít nhất một khách hàng.");
        if (entity.UsageLimit.HasValue && entity.UsageLimit < entity.UsedCount)
            throw new InvalidOperationException("Giới hạn sử dụng không được nhỏ hơn số lượt đã dùng.");
        if (entity.StartAt.HasValue && entity.EndAt.HasValue && entity.EndAt <= entity.StartAt)
            throw new InvalidOperationException("Thời gian kết thúc phải sau thời gian bắt đầu.");
    }
}
