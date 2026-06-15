namespace Pb.Builder.Domain.Documents;

public sealed record DocumentManifest(
    string AppId,
    DocumentType DocumentType,
    DocumentId DocumentId,
    SchemaVersion SchemaVersion,
    RuntimeVersion RuntimeVersion,
    DateTimeOffset CreatedAt)
{
    public const string PbAppId = "pb-html-builder";

    public static DocumentManifest Create(
        DocumentType documentType,
        DocumentId documentId,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(documentId.Value))
        {
            throw new ArgumentException("Document id cannot be empty.", nameof(documentId));
        }

        return new DocumentManifest(
            PbAppId,
            documentType,
            documentId,
            SchemaVersion.Current,
            RuntimeVersion.Current,
            createdAt);
    }
}
