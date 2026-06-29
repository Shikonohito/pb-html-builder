using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Application.Projects;

public sealed record ProjectFileWriteResult(
    DocumentTarget Target,
    string RelativePath,
    string? BackupRelativePath,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}
