using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Application.Extensibility;

public interface IDocumentTypeModule
{
    DocumentType Type { get; }

    string DisplayName { get; }

    string NewDocumentLabel { get; }

    string DefaultSubdirectoryName { get; }
}
