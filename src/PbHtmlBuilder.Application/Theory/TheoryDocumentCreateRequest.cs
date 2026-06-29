namespace PbHtmlBuilder.Application.Theory;

public sealed record TheoryDocumentCreateRequest(
    string? FolderPath,
    string? FileName,
    string? DocumentTitle);
