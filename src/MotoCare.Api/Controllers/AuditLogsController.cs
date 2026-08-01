using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = SecurityRoles.Management)]
public sealed class AuditLogsController(MongoDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] string? userId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AuditLog>>
        {
            Builders<AuditLog>.Filter.Eq(x => x.IsDeleted, false)
        };
        if (!string.IsNullOrWhiteSpace(userId)) filters.Add(Builders<AuditLog>.Filter.Eq(x => x.UserId, userId));
        if (!string.IsNullOrWhiteSpace(action)) filters.Add(Builders<AuditLog>.Filter.Eq(x => x.Action, action));
        if (!string.IsNullOrWhiteSpace(entityType)) filters.Add(Builders<AuditLog>.Filter.Eq(x => x.EntityType, entityType));
        if (from.HasValue) filters.Add(Builders<AuditLog>.Filter.Gte(x => x.CreatedAt, from.Value.ToUniversalTime()));
        if (to.HasValue) filters.Add(Builders<AuditLog>.Filter.Lte(x => x.CreatedAt, to.Value.ToUniversalTime()));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
            filters.Add(Builders<AuditLog>.Filter.Or(
                Builders<AuditLog>.Filter.Regex(x => x.Username, regex),
                Builders<AuditLog>.Filter.Regex(x => x.UserDisplayName, regex),
                Builders<AuditLog>.Filter.Regex(x => x.EntityId, regex),
                Builders<AuditLog>.Filter.Regex(x => x.RequestPath, regex)));
        }

        var filter = Builders<AuditLog>.Filter.And(filters);
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var collection = context.Collection<AuditLog>();
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await collection.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Limit(safePageSize)
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(new PagedResult<AuditLog>(items, total, safePage, safePageSize)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var item = await context.Collection<AuditLog>()
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        return item is null
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy lịch sử thao tác."))
            : Ok(ApiEnvelope.Ok(item));
    }
}
