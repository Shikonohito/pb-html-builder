namespace Pb.Builder.Application.Commands;

public sealed record CommandResult(bool Succeeded, string? ErrorMessage = null)
{
    public static CommandResult Success() => new(true);

    public static CommandResult Failure(string errorMessage) => new(false, errorMessage);
}
