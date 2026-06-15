using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Application.Extensibility;

public sealed record DocumentTypeModule(
    DocumentType Type,
    string DisplayName,
    string NewDocumentLabel,
    string DefaultSubdirectoryName) : IDocumentTypeModule;
