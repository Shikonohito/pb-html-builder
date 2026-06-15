namespace Pb.Builder.Application.UseCases;

public sealed record NewDocumentResult(
    bool Succeeded,
    string? DirectoryPath = null,
    string? FileName = null,
    string? BrowserTitle = null,
    string? Feedback = null,
    string? FolderError = null,
    string? FileNameError = null)
{
    public static NewDocumentResult FileNameFailure(string errorMessage)
    {
        return new NewDocumentResult(false, FileNameError: errorMessage);
    }

    public static NewDocumentResult FolderFailure(string errorMessage)
    {
        return new NewDocumentResult(false, FolderError: errorMessage);
    }
}
