using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Application.Projects;

public sealed record ProjectFileInfo(
    DocumentTarget Target,
    string RelativePath,
    bool DirectoryExists,
    bool FileExists,
    DateTimeOffset? LastModifiedAt,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
