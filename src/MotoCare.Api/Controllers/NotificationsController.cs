using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(MongoDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        var audience = Builders<Notification>.Filter.Or(
            Builders<Notification>.Filter.Eq(x => x.UserId, userId),
            Builders<Notification>.Filter.In(x => x.Role, roles));
        var filter = Builders<Notification>.Filter.And(
            Builders<Notification>.Filter.Eq(x => x.IsDeleted, false),
            audience);
        if (unreadOnly)
        {
            filter &= Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Ne(x => x.IsRead, true),
                Builders<Notification>.Filter.Not(
                    Builders<Notification>.Filter.AnyEq(x => x.ReadByUserIds, userId)));
        }

        var notifications = await context.Collection<Notification>()
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Limit(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        return Ok(ApiEnvelope.Ok(notifications));
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> Read(string id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        var filter = Builders<Notification>.Filter.And(
            Builders<Notification>.Filter.Eq(x => x.Id, id),
            Builders<Notification>.Filter.Or(
                Builders<Notification>.Filter.Eq(x => x.UserId, userId),
                Builders<Notification>.Filter.In(x => x.Role, roles)));
        var update = Builders<Notification>.Update
            .AddToSet(x => x.ReadByUserIds, userId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var result = await context.Collection<Notification>()
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.MatchedCount == 0
            ? NotFound(ApiEnvelope.Fail("NOT_FOUND", "Không tìm thấy thông báo."))
            : Ok(ApiEnvelope.Ok(new { id, isRead = true }));
    }
}
