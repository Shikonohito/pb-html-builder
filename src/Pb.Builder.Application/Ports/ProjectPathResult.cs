namespace Pb.Builder.Application.Ports;

public sealed record ProjectPathResult(bool Succeeded, string? Value, string? ErrorMessage)
{
    public static ProjectPathResult Success(string? value = null) => new(true, value, null);

    public static ProjectPathResult Failure(string errorMessage) => new(false, null, errorMessage);
}
