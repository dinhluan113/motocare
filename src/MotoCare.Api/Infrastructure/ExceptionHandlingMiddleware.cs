using System.Net;
using MongoDB.Driver;

namespace MotoCare.Api.Infrastructure;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, MongoErrorLogWriter errorLogs)
    {
        try
        {
            await next(context);
        }
        catch (KeyNotFoundException ex)
        {
            await errorLogs.WriteAsync(
                context, ex, StatusCodes.Status404NotFound, "NOT_FOUND", "Warning");
            await WriteError(context, HttpStatusCode.NotFound, "NOT_FOUND", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await errorLogs.WriteAsync(
                context, ex, StatusCodes.Status409Conflict, "BUSINESS_RULE_VIOLATION", "Warning");
            await WriteError(context, HttpStatusCode.Conflict, "BUSINESS_RULE_VIOLATION", ex.Message);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await errorLogs.WriteAsync(
                context, ex, StatusCodes.Status409Conflict, "DUPLICATE_KEY", "Warning");
            await WriteError(context, HttpStatusCode.Conflict, "DUPLICATE_KEY", "Dữ liệu đã tồn tại.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error. TraceId: {TraceId}", context.TraceIdentifier);
            await errorLogs.WriteAsync(
                context, ex, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Error");
            await WriteError(
                context,
                HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Có lỗi hệ thống xảy ra.",
                context.TraceIdentifier);
        }
    }

    private static async Task WriteError(
        HttpContext context,
        HttpStatusCode status,
        string code,
        string message,
        string? traceId = null)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            ApiEnvelope.Fail(code, message, traceId is null ? null : new { traceId }));
    }
}
