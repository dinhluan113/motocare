using System.Net;
using MongoDB.Driver;

namespace MotoCare.Api.Infrastructure;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (KeyNotFoundException ex)
        {
            await WriteError(context, HttpStatusCode.NotFound, "NOT_FOUND", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await WriteError(context, HttpStatusCode.Conflict, "BUSINESS_RULE_VIOLATION", ex.Message);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await WriteError(context, HttpStatusCode.Conflict, "DUPLICATE_KEY", "Dữ liệu đã tồn tại.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error. TraceId: {TraceId}", context.TraceIdentifier);
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
