namespace PbHtmlBuilder.UI.Features.StartPage;

public sealed record StartDocumentAction(
    string Key,
    string Label,
    string Accent,
    string FolderPathPlaceholder,
    string FileNamePlaceholder);
