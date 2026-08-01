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
[Route("api/v1/inventory")]
[Authorize]
public sealed class InventoryController(
    InventoryService inventory,
    MongoDbContext context) : ControllerBase
{
    [HttpPost("movements")]
    [Authorize(Roles = SecurityRoles.Management)]
    public async Task<IActionResult> Move(
        StockMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Type == InventoryTransactionType.Receipt)
        {
            throw new InvalidOperationException("Nhập phụ tùng phải được lập bằng phiếu chi nhập hàng.");
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var transaction = await inventory.MoveAsync(request, userId, cancellationToken);
        return Ok(ApiEnvelope.Ok(transaction, "Đã cập nhật tồn kho."));
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions(
        [FromQuery] string? partId,
        [FromQuery] string? supplierId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<InventoryTransaction>>
        {
            Builders<InventoryTransaction>.Filter.Eq(x => x.IsDeleted, false)
        };
        if (!string.IsNullOrWhiteSpace(partId))
        {
            filters.Add(Builders<InventoryTransaction>.Filter.Eq(x => x.PartId, partId));
        }
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            filters.Add(Builders<InventoryTransaction>.Filter.Eq(x => x.SupplierId, supplierId));
        }

        if (from.HasValue)
        {
            filters.Add(Builders<InventoryTransaction>.Filter.Gte(x => x.TransactionDate, from.Value.ToUniversalTime()));
        }

        if (to.HasValue)
        {
            filters.Add(Builders<InventoryTransaction>.Filter.Lte(x => x.TransactionDate, to.Value.ToUniversalTime()));
        }

        var filter = Builders<InventoryTransaction>.Filter.And(filters);
        var collection = context.Collection<InventoryTransaction>();
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await collection.Find(filter)
            .SortByDescending(x => x.TransactionDate)
            .Skip((safePage - 1) * safePageSize)
            .Limit(safePageSize)
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(new PagedResult<InventoryTransaction>(
            items,
            total,
            safePage,
            safePageSize)));
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock(CancellationToken cancellationToken)
    {
        var filter = new MongoDB.Bson.BsonDocument("$expr", new MongoDB.Bson.BsonDocument(
            "$lt",
            new MongoDB.Bson.BsonArray { "$quantityOnHand", "$minQuantity" }));
        var parts = await context.Collection<Part>()
            .Find(filter)
            .SortBy(x => x.QuantityOnHand)
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(parts));
    }
}
