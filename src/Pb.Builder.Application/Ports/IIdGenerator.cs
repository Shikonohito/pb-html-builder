using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Application.Ports;

public interface IIdGenerator
{
    DocumentId CreateDocumentId();
}
