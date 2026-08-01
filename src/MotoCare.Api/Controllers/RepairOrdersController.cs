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
[Route("api/v1/repair-orders")]
[Authorize]
public sealed class RepairOrdersController(
    MongoDbContext context,
    RepairOrderService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPage(
        [FromQuery] RepairOrderStatus? status,
        [FromQuery] string? customerId,
        [FromQuery] string? vehicleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<RepairOrder>>
        {
            Builders<RepairOrder>.Filter.Eq(x => x.IsDeleted, false)
        };
        if (status.HasValue)
        {
            filters.Add(Builders<RepairOrder>.Filter.Eq(x => x.Status, status.Value));
        }

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            filters.Add(Builders<RepairOrder>.Filter.Eq(x => x.CustomerId, customerId));
        }

        if (!string.IsNullOrWhiteSpace(vehicleId))
        {
            filters.Add(Builders<RepairOrder>.Filter.Eq(x => x.VehicleId, vehicleId));
        }

        var filter = Builders<RepairOrder>.Filter.And(filters);
        var collection = context.Collection<RepairOrder>();
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await collection.Find(filter)
            .SortByDescending(x => x.ReceivedAt)
            .Skip((safePage - 1) * safePageSize)
            .Limit(safePageSize)
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(new PagedResult<RepairOrder>(
            items,
            total,
            safePage,
            safePageSize)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var order = await context.Collection<RepairOrder>()
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        return order is null
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy phiếu sửa chữa."))
            : Ok(ApiEnvelope.Ok(order));
    }

    [HttpPost]
    [Authorize(Roles = SecurityRoles.Operations)]
    public async Task<IActionResult> Create(
        CreateRepairOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await service.CreateAsync(request, UserId(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, ApiEnvelope.Ok(order));
    }

    [HttpPost("{id}/items")]
    [Authorize(Roles = SecurityRoles.Operations)]
    public async Task<IActionResult> AddItem(
        string id,
        AddRepairOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.AddItemAsync(id, request, cancellationToken)));
    }

    [HttpPatch("{id}/condition-images")]
    [Authorize(Roles = SecurityRoles.Operations)]
    public async Task<IActionResult> UpdateConditionImages(
        string id,
        UpdateVehicleConditionImagesRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.UpdateConditionImagesAsync(
            id,
            request.Images,
            cancellationToken)));
    }

    [HttpPatch("{id}/odometer")]
    [Authorize(Roles = SecurityRoles.Operations)]
    public async Task<IActionResult> UpdateOdometer(
        string id,
        UpdateRepairOrderOdometerRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.UpdateOdometerAsync(
            id,
            request.OdometerIn,
            cancellationToken)));
    }

    [HttpPut("{id}/items/{itemId}")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> UpdateItem(
        string id,
        string itemId,
        UpdateRepairOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.UpdateItemAsync(id, itemId, request, cancellationToken)));
    }

    [HttpDelete("{id}/items/{itemId}")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> DeleteItem(
        string id,
        string itemId,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.DeleteItemAsync(id, itemId, cancellationToken)));
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = SecurityRoles.Operations)]
    public async Task<IActionResult> ChangeStatus(
        string id,
        ChangeRepairStatusRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.ChangeStatusAsync(
            id,
            request,
            UserId(),
            cancellationToken)));
    }

    [HttpPatch("{id}/items/{itemId}/work")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> UpdateWork(
        string id,
        string itemId,
        UpdateRepairWorkRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(ApiEnvelope.Ok(await service.UpdateWorkAsync(
            id,
            itemId,
            request,
            cancellationToken)));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var order = await context.Collection<RepairOrder>()
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy phiếu sửa chữa."));
        }

        var hasActiveInvoice = await context.Collection<Invoice>()
            .Find(x => x.RepairOrderId == id
                && !x.IsDeleted
                && x.PaymentStatus != InvoicePaymentStatus.Cancelled)
            .AnyAsync(cancellationToken);
        if (hasActiveInvoice)
        {
            throw new InvalidOperationException(
                "Không thể xóa phiếu sửa chữa đang có hóa đơn còn hiệu lực. Hãy hủy hóa đơn trước.");
        }

        var update = Builders<RepairOrder>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        await context.Collection<RepairOrder>().UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            update,
            cancellationToken: cancellationToken);
        return Ok(ApiEnvelope.Ok(new { id, deleted = true }, "Đã xóa phiếu sửa chữa."));
    }

    private string UserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
}
