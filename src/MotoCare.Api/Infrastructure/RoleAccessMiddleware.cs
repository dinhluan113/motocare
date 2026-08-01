using System.Security.Claims;

namespace MotoCare.Api.Infrastructure;

public sealed class RoleAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !context.User.IsInRole(SecurityRoles.Employee)
            || context.User.IsInRole(SecurityRoles.Admin)
            || context.User.IsInRole(SecurityRoles.Manager)
            || IsEmployeeAllowed(context.Request))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiEnvelope.Fail(
            "FORBIDDEN",
            "Nhân viên không có quyền thực hiện thao tác này."));
    }

    private static bool IsEmployeeAllowed(HttpRequest request)
    {
        var path = request.Path.Value?.TrimEnd('/') ?? string.Empty;
        var method = request.Method;

        if (path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/notifications", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HttpMethods.IsGet(method))
        {
            return HasAnyPrefix(path,
                "/api/v1/customers",
                "/api/v1/vehicles",
                "/api/v1/vehicle-brands",
                "/api/v1/vehicle-models",
                "/api/v1/repair-orders",
                "/api/v1/parts",
                "/api/v1/part-brands",
                "/api/v1/part-categories",
                "/api/v1/suppliers",
                "/api/v1/inventory",
                "/api/v1/locations");
        }

        if ((HttpMethods.IsPost(method) || HttpMethods.IsPut(method))
            && (path.StartsWith("/api/v1/customers", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/v1/vehicles", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return HttpMethods.IsPost(method)
            && path.Equals("/api/v1/repair-orders", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnyPrefix(string path, params string[] prefixes) =>
        prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
