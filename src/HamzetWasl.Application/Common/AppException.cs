using HamzetWasl.Application.Common;

namespace HamzetWasl.Application.Common;

/// <summary>Application-level failure that maps to the standard error envelope.</summary>
public class AppException : Exception
{
    public AppException(
        int statusCode,
        string code,
        string? message = null,
        IReadOnlyDictionary<string, string[]>? fields = null)
        : base(message ?? ArabicMessages.For(code))
    {
        StatusCode = statusCode;
        Code = code;
        Fields = fields;
    }

    public int StatusCode { get; }
    public string Code { get; }
    public IReadOnlyDictionary<string, string[]>? Fields { get; }

    public static AppException Validation(IReadOnlyDictionary<string, string[]> fields) =>
        new(400, ErrorCodes.ValidationError, ArabicMessages.For(ErrorCodes.ValidationError), fields);

    public static AppException Unauthorized(string? message = null) =>
        new(401, ErrorCodes.Unauthorized, message);

    public static AppException Forbidden(string code = ErrorCodes.Forbidden, string? message = null) =>
        new(403, code, message);

    public static AppException NotFound(string? message = null) =>
        new(404, ErrorCodes.NotFound, message);

    public static AppException Conflict(string code = ErrorCodes.Conflict, string? message = null) =>
        new(409, code, message);

    public static AppException Business(string code, string? message = null) =>
        new(422, code, message);
}
