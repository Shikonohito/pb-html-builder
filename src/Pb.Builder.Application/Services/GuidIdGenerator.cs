using Pb.Builder.Application.Ports;
using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Application.Services;

public sealed class GuidIdGenerator : IIdGenerator
{
    public DocumentId CreateDocumentId() => new(Guid.NewGuid().ToString("N"));
}
