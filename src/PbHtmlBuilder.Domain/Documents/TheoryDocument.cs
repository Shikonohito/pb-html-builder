namespace PbHtmlBuilder.Domain.Documents;

public sealed record TheoryDocument(
    string DocumentId,
    int SchemaVersion,
    string BuilderVersion,
    string DocumentTitle,
    DocumentTarget Target,
    BrandMetadata Brand,
    RuntimeMetadata Runtime,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TheorySectionMapCell> SectionMapCells,
    IReadOnlyList<TheorySection> Sections)
{
    public const string DocumentType = "Theory";

    public TheoryDocument WithSavedTarget(DocumentTarget target, DateTimeOffset savedAt)
    {
        return this with
        {
            Target = target,
            UpdatedAt = savedAt
        };
    }

    public TheoryDocument WithSaveAs(string documentId, DocumentTarget target, DateTimeOffset savedAt)
    {
        return this with
        {
            DocumentId = documentId,
            Target = target,
            CreatedAt = savedAt,
            UpdatedAt = savedAt
        };
    }
}
