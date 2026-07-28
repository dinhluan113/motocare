namespace MotoCare.Api.Infrastructure;

public sealed record ApiEnvelope<T>(
    bool Success,
    T? Data,
    string? Message = null,
    string? Code = null,
    object? Errors = null)
{
    public static ApiEnvelope<T> Ok(T data, string? message = null) =>
        new(true, data, message);
}

public static class ApiEnvelope
{
    public static ApiEnvelope<object> Ok(object data, string? message = null) =>
        new(true, data, message);

    public static ApiEnvelope<object> Fail(
        string code,
        string message,
        object? errors = null) =>
        new(false, null, message, code, errors);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    long Total,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
