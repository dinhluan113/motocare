using System.Security.Claims;
using MotoCare.Api.Domain;

namespace MotoCare.Api.Infrastructure;

public sealed class AuditLoggingMiddleware(
    RequestDelegate next,
    ILogger<AuditLoggingMiddleware> logger)
{
    private static readonly HashSet<string> MutatingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async Task InvokeAsync(HttpContext httpContext, MongoDbContext mongo)
    {
        await next(httpContext);
        if (!MutatingMethods.Contains(httpContext.Request.Method)
            || httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest
            || httpContext.Request.Path.StartsWithSegments("/api/v1/auth/login"))
        {
            return;
        }

        try
        {
            var log = new AuditLog
            {
                UserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
                Action = httpContext.Request.Method,
                EntityType = httpContext.Request.Path.Value ?? string.Empty,
                EntityId = httpContext.Request.RouteValues.GetValueOrDefault("id")?.ToString() ?? string.Empty,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString()
            };
            await mongo.Collection<AuditLog>().InsertOneAsync(
                log,
                cancellationToken: httpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not write audit log.");
        }
    }
}
