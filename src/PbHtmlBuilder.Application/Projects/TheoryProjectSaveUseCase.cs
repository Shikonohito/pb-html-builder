using PbHtmlBuilder.Domain.Documents;
using PbHtmlBuilder.Domain.Ids;

namespace PbHtmlBuilder.Application.Projects;

public sealed class TheoryProjectSaveUseCase
{
    private readonly TimeProvider _clock;
    private readonly IProjectFileStorage _storage;
    private readonly ITheoryHtmlRenderer _renderer;

    public TheoryProjectSaveUseCase(
        TimeProvider clock,
        IProjectFileStorage storage,
        ITheoryHtmlRenderer renderer)
    {
        _clock = clock;
        _storage = storage;
        _renderer = renderer;
    }

    public async Task<TheoryProjectSaveResult> SaveAsync(
        TheoryDocument document,
        TheoryProjectSaveOptions options,
        CancellationToken cancellationToken = default)
    {
        var requestedTarget = options.TargetOverride ?? document.Target;
        var fileInfo = _storage.Inspect(requestedTarget);

        if (!fileInfo.IsValid)
        {
            return TheoryProjectSaveResult.ValidationFailed(document, fileInfo.Errors);
        }

        if (!fileInfo.DirectoryExists && !options.AllowCreateDirectory)
        {
            return new TheoryProjectSaveResult(
                ProjectSaveStatus.RequiresCreateDirectoryConfirmation,
                document,
                fileInfo.RelativePath,
                null,
                null,
                []);
        }

        if (fileInfo.FileExists && !options.AllowOverwrite)
        {
            return new TheoryProjectSaveResult(
                ProjectSaveStatus.RequiresOverwriteConfirmation,
                document,
                fileInfo.RelativePath,
                fileInfo.LastModifiedAt,
                null,
                []);
        }

        var savedAt = _clock.GetUtcNow();
        var documentToWrite = options.SaveAs
            ? document.WithSaveAs(StableIdGenerator.NewId("doc"), fileInfo.Target, savedAt)
            : document.WithSavedTarget(fileInfo.Target, savedAt);

        var html = _renderer.Render(documentToWrite);
        var writeResult = await _storage.WriteHtmlAsync(
            fileInfo.Target,
            html,
            new ProjectFileWriteOptions(
                CreateDirectory: options.AllowCreateDirectory,
                Overwrite: options.AllowOverwrite),
            cancellationToken);

        if (!writeResult.Succeeded)
        {
            return TheoryProjectSaveResult.ValidationFailed(document, writeResult.Errors);
        }

        return new TheoryProjectSaveResult(
            ProjectSaveStatus.Saved,
            documentToWrite with { Target = writeResult.Target },
            writeResult.RelativePath,
            null,
            writeResult.BackupRelativePath,
            []);
    }
}
