using Pb.Builder.Application.Session;

namespace Pb.Builder.Application.Commands;

public sealed class CommandDispatcher(DocumentStore documentStore)
{
    public CommandResult Dispatch(IDocumentCommand command)
    {
        return command.Execute(documentStore);
    }
}
