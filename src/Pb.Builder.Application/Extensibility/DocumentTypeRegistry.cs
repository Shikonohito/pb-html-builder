using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Application.Extensibility;

public sealed class DocumentTypeRegistry
{
    private readonly Dictionary<DocumentType, IDocumentTypeModule> modules;

    public DocumentTypeRegistry(IEnumerable<IDocumentTypeModule> modules)
    {
        this.modules = modules.ToDictionary(module => module.Type);
    }

    public static DocumentTypeRegistry CreateDefault()
    {
        return new DocumentTypeRegistry(
        [
            new DocumentTypeModule(DocumentType.Theory, "Theory", "New Theory", "theory"),
            new DocumentTypeModule(DocumentType.Practice, "Practice", "New Practice", "practice"),
            new DocumentTypeModule(DocumentType.Assessment, "Assessment", "New Assessment", "assessment"),
            new DocumentTypeModule(DocumentType.VerifiedAssessment, "Verified assessment", "New Verified Assessment", "verified")
        ]);
    }

    public IDocumentTypeModule GetRequired(DocumentType documentType)
    {
        return modules.TryGetValue(documentType, out var module)
            ? module
            : throw new InvalidOperationException($"Document type '{documentType}' is not registered.");
    }
}
