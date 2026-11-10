namespace HamzetWasl.Application.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    string TraceId { get; }
}

public interface IApplicationMarker;
