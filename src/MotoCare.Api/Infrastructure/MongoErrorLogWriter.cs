using System.Security.Claims;
using Microsoft.AspNetCore.WebUtilities;
using MongoDB.Driver;
using MotoCare.Api.Domain;

namespace MotoCare.Api.Infrastructure;

public sealed class MongoErrorLogWriter(
    MongoDbContext mongo,
    ILogger<MongoErrorLogWriter> logger)
{
    private static readonly string[] SensitiveQueryTerms =
        ["token", "password", "secret", "authorization", "api_key", "apikey", "key"];

    public async Task WriteAsync(
        HttpContext context,
        Exception exception,
        int statusCode,
        string errorCode,
        string level)
    {
        var log = new ApplicationErrorLog
        {
            TraceId = context.TraceIdentifier,
            Level = level,
            ErrorCode = errorCode,
            StatusCode = statusCode,
            Message = Limit(exception.Message, 4_000) ?? string.Empty,
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            StackTrace = Limit(exception.StackTrace, 32_000),
            InnerException = FormatInnerException(exception.InnerException),
            RequestMethod = context.Request.Method,
            RequestPath = Limit(context.Request.Path.Value, 2_000) ?? string.Empty,
            QueryString = SanitizeQueryString(context.Request.QueryString.Value),
            RouteValues = context.Request.RouteValues.ToDictionary(
                x => x.Key,
                x => Limit(x.Value?.ToString(), 1_000)),
            UserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            Username = context.User.FindFirstValue(ClaimTypes.Name),
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Limit(context.Request.Headers.UserAgent.ToString(), 1_000)
        };

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await mongo.Collection<ApplicationErrorLog>()
                .InsertOneAsync(log, cancellationToken: timeout.Token);
        }
        catch (Exception writeException)
        {
            logger.LogError(
                writeException,
                "Could not persist backend error log to MongoDB. Original TraceId: {TraceId}",
                context.TraceIdentifier);
        }
    }

    private static string? SanitizeQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString)) return null;
        var parsed = QueryHelpers.ParseQuery(queryString);
        var values = parsed.SelectMany(item => item.Value.Select(value =>
            new KeyValuePair<string, string?>(
                item.Key,
                IsSensitive(item.Key) ? "[REDACTED]" : Limit(value, 1_000))));
        var sanitized = QueryString.Create(values).Value;
        return Limit(sanitized, 4_000);
    }

    private static bool IsSensitive(string key) => SensitiveQueryTerms.Any(term =>
        key.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string? FormatInnerException(Exception? exception)
    {
        if (exception is null) return null;
        var messages = new List<string>();
        for (var current = exception; current is not null && messages.Count < 5; current = current.InnerException)
        {
            messages.Add($"{current.GetType().FullName}: {current.Message}");
        }
        return Limit(string.Join(Environment.NewLine, messages), 8_000);
    }

    private static string? Limit(string? value, int length) =>
        string.IsNullOrEmpty(value) || value.Length <= length ? value : value[..length];
}
