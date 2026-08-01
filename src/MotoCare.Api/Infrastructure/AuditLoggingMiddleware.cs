using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;
using MotoCare.Api.Domain;

namespace MotoCare.Api.Infrastructure;

public sealed class AuditLoggingMiddleware(
    RequestDelegate next,
    ILogger<AuditLoggingMiddleware> logger)
{
    private static readonly HashSet<string> MutatingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    private static readonly IReadOnlyDictionary<string, string> Collections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["users"] = "users", ["customers"] = "customers", ["employees"] = "employees",
            ["vehicles"] = "vehicles", ["vehicle-brands"] = "vehicle_brands",
            ["vehicle-models"] = "vehicle_models", ["part-brands"] = "part_brands",
            ["part-categories"] = "part_categories", ["parts"] = "parts",
            ["service-categories"] = "service_categories",
            ["suppliers"] = "suppliers", ["inventory"] = "inventory_transactions",
            ["repair-orders"] = "repair_orders", ["invoices"] = "invoices",
            ["coupons"] = "coupons",
            ["cash-transactions"] = "cash_transactions", ["cash-categories"] = "cash_categories",
            ["loyalty"] = "loyalty_accounts", ["notifications"] = "notifications"
        };

    public async Task InvokeAsync(HttpContext httpContext, MongoDbContext mongo)
    {
        if (!MutatingMethods.Contains(httpContext.Request.Method)
            || httpContext.Request.Path.StartsWithSegments("/api/v1/auth/login"))
        {
            await next(httpContext);
            return;
        }

        httpContext.Request.EnableBuffering();
        var requestBody = await ReadRequestBodyAsync(httpContext.Request);
        var entityType = GetEntityType(httpContext.Request.Path);
        var entityId = httpContext.Request.RouteValues.GetValueOrDefault("id")?.ToString() ?? string.Empty;
        var beforeData = await ReadBeforeDataAsync(mongo, entityType, entityId, httpContext.RequestAborted);

        var originalBody = httpContext.Response.Body;
        await using var responseBuffer = new MemoryStream();
        httpContext.Response.Body = responseBuffer;
        try
        {
            await next(httpContext);
            responseBuffer.Position = 0;
            var responseBody = await new StreamReader(responseBuffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(originalBody, httpContext.RequestAborted);

            if (httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest) return;
            if (string.IsNullOrWhiteSpace(entityId)) entityId = ExtractEntityId(responseBody);
            var log = new AuditLog
            {
                UserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
                Username = httpContext.User.FindFirstValue(ClaimTypes.Name),
                UserDisplayName = httpContext.User.FindFirstValue("full_name")
                    ?? httpContext.User.FindFirstValue(ClaimTypes.GivenName),
                Action = GetAction(httpContext.Request.Method, httpContext.Request.Path),
                EntityType = entityType,
                EntityId = entityId,
                RequestPath = httpContext.Request.Path.Value ?? string.Empty,
                StatusCode = httpContext.Response.StatusCode,
                BeforeData = Sanitize(beforeData),
                AfterData = Sanitize(string.IsNullOrWhiteSpace(responseBody) ? requestBody : responseBody),
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString()
            };
            await mongo.Collection<AuditLog>().InsertOneAsync(log, cancellationToken: httpContext.RequestAborted);
        }
        catch (Exception ex) when (httpContext.Response.HasStarted)
        {
            logger.LogWarning(ex, "Could not complete audit logging.");
        }
        finally
        {
            httpContext.Response.Body = originalBody;
        }
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0) return string.Empty;
        if (request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
            return "[MULTIPART FILE UPLOAD]";
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static async Task<string?> ReadBeforeDataAsync(
        MongoDbContext mongo,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entityId)
            || !ObjectId.TryParse(entityId, out var objectId)
            || !Collections.TryGetValue(entityType, out var collectionName)) return null;
        var document = await mongo.Database.GetCollection<BsonDocument>(collectionName)
            .Find(new BsonDocument("_id", objectId))
            .FirstOrDefaultAsync(cancellationToken);
        return document?.ToJson(new MongoDB.Bson.IO.JsonWriterSettings
        {
            OutputMode = MongoDB.Bson.IO.JsonOutputMode.RelaxedExtendedJson
        });
    }

    private static string GetEntityType(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return segments.Length >= 3 ? segments[2] : path.Value ?? string.Empty;
    }

    private static string GetAction(string method, PathString path)
    {
        if (path.Value?.EndsWith("/confirm", StringComparison.OrdinalIgnoreCase) == true) return "CONFIRM";
        return method.ToUpperInvariant() switch
        {
            "POST" => "CREATE",
            "PUT" or "PATCH" => "UPDATE",
            "DELETE" => "DELETE",
            _ => method.ToUpperInvariant()
        };
    }

    private static string ExtractEntityId(string responseBody)
    {
        try { return JsonNode.Parse(responseBody)?["data"]?["id"]?.GetValue<string>() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string? Sanitize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var node = JsonNode.Parse(json);
            SanitizeNode(node);
            var result = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? string.Empty;
            return result.Length <= 100_000 ? result : result[..100_000] + "…[TRUNCATED]";
        }
        catch
        {
            return json.Length <= 100_000 ? json : json[..100_000] + "…[TRUNCATED]";
        }
    }

    private static void SanitizeNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Key.Contains("password", StringComparison.OrdinalIgnoreCase)
                    || property.Key.Contains("token", StringComparison.OrdinalIgnoreCase))
                {
                    obj[property.Key] = "[REDACTED]";
                }
                else if (property.Key.Equals("attachmentUrl", StringComparison.OrdinalIgnoreCase)
                         && property.Value?.GetValue<string>() is { } image)
                {
                    obj[property.Key] = $"[IMAGE {image.Length} chars]";
                }
                else if (property.Key.Equals("vehicleConditionImages", StringComparison.OrdinalIgnoreCase)
                         && property.Value is JsonArray images)
                {
                    obj[property.Key] = $"[{images.Count} IMAGES]";
                }
                else SanitizeNode(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array) SanitizeNode(item);
        }
    }
}
