using Pb.Builder.Application.Session;

namespace Pb.Builder.Application.Commands;

public interface IDocumentCommand
{
    CommandResult Execute(DocumentStore documentStore);
}
