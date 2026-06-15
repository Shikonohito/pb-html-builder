namespace Pb.Builder.Application.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
