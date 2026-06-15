using Pb.Builder.Application.Ports;

namespace Pb.Builder.Application.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
