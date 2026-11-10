namespace HamzetWasl.Application.Common;

public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Fields = null);

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiError? Error,
    string TraceId)
{
    public static ApiResponse<T> Ok(T data, string traceId) =>
        new(true, data, null, traceId);

    public static ApiResponse<T> Fail(ApiError error, string traceId) =>
        new(false, default, error, traceId);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public static PagedResult<T> Empty(int page, int pageSize) =>
        new(Array.Empty<T>(), page, pageSize, 0);
}
