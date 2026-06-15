namespace Pb.Builder.Domain.Documents;

public sealed record Document(DocumentManifest Manifest, DocumentSettings Settings)
{
    public static Document Create(
        DocumentType documentType,
        DocumentId documentId,
        string browserTitle,
        DateTimeOffset createdAt)
    {
        return new Document(
            DocumentManifest.Create(documentType, documentId, createdAt),
            DocumentSettings.Create(browserTitle));
    }
}
