namespace PbHtmlBuilder.Application.Projects;

public sealed record FolderBrowseResponse(
    string RootPath,
    string RootSelectionPath,
    string CurrentPath,
    string CurrentSelectionPath,
    string CurrentDisplayPath,
    string RequestedPath,
    string RequestedSelectionPath,
    string RequestedDisplayPath,
    bool RequestedPathExists,
    string? ParentPath,
    string? ParentSelectionPath,
    IReadOnlyList<FolderBrowseEntry> Entries,
    IReadOnlyList<string> Errors);
