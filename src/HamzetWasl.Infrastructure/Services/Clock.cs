using HamzetWasl.Application.Interfaces;

namespace HamzetWasl.Infrastructure.Services;

public sealed class Clock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
