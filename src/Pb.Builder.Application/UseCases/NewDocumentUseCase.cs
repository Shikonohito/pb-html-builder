using Pb.Builder.Application.Extensibility;
using Pb.Builder.Application.Ports;
using Pb.Builder.Application.Session;
using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Application.UseCases;

public sealed class NewDocumentUseCase(
    DocumentStore documentStore,
    DocumentTypeRegistry documentTypeRegistry,
    IProjectPathService projectPathService,
    IIdGenerator idGenerator,
    IClock clock)
{
    public NewDocumentResult Create(NewDocumentRequest request)
    {
        var fileNameResult = projectPathService.NormalizeHtmlFileName(request.FileName);
        if (!fileNameResult.Succeeded || fileNameResult.Value is null)
        {
            return NewDocumentResult.FileNameFailure(fileNameResult.ErrorMessage ?? "File name is invalid.");
        }

        var requestedDirectory = string.IsNullOrWhiteSpace(request.DirectoryPath)
            ? projectPathService.GetDefaultProjectDirectory(request.DocumentType)
            : request.DirectoryPath;

        var directoryResult = projectPathService.ResolveDirectoryPath(requestedDirectory);
        if (!directoryResult.Succeeded || directoryResult.Value is null)
        {
            return NewDocumentResult.FolderFailure(directoryResult.ErrorMessage ?? "Folder path is invalid.");
        }

        var pathResult = projectPathService.ValidateProjectFilePath(directoryResult.Value, fileNameResult.Value);
        if (!pathResult.Succeeded)
        {
            return NewDocumentResult.FileNameFailure(pathResult.ErrorMessage ?? "File path is invalid.");
        }

        var createDirectoryResult = projectPathService.EnsureDirectory(directoryResult.Value);
        if (!createDirectoryResult.Succeeded)
        {
            return NewDocumentResult.FolderFailure(createDirectoryResult.ErrorMessage ?? "Cannot create folder.");
        }

        var browserTitle = string.IsNullOrWhiteSpace(request.BrowserTitle)
            ? fileNameResult.Value
            : request.BrowserTitle.Trim();
        var document = Document.Create(request.DocumentType, idGenerator.CreateDocumentId(), browserTitle, clock.UtcNow);
        var session = new DocumentSession(document, directoryResult.Value, fileNameResult.Value);

        documentStore.StartSession(session);

        var module = documentTypeRegistry.GetRequired(request.DocumentType);
        return new NewDocumentResult(
            true,
            directoryResult.Value,
            fileNameResult.Value,
            browserTitle,
            $"Create is a placeholder. Prepared folder and remembered {module.NewDocumentLabel}: {session.ProjectPath}");
    }
}
