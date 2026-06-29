namespace PbHtmlBuilder.Application.Projects;

public sealed record FolderBrowseEntry(
    string Name,
    string FullPath,
    string SelectionPath,
    string? RelativePath,
    FolderBrowseEntryKind Kind,
    bool IsAccessible,
    string? Error);
