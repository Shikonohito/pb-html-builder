namespace Pb.Builder.Application.Ports;

public interface IFileDialogService
{
    Task<string?> PickFolderAsync(string? initialDirectory);
}
