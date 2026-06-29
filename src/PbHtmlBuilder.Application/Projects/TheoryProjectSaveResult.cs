using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Application.Projects;

public sealed record TheoryProjectSaveResult(
    ProjectSaveStatus Status,
    TheoryDocument Document,
    string? RelativePath,
    DateTimeOffset? LastModifiedAt,
    string? BackupRelativePath,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Status == ProjectSaveStatus.Saved;

    public static TheoryProjectSaveResult ValidationFailed(
        TheoryDocument document,
        IReadOnlyList<string> errors)
    {
        return new TheoryProjectSaveResult(
            ProjectSaveStatus.ValidationFailed,
            document,
            null,
            null,
            null,
            errors);
    }
}
