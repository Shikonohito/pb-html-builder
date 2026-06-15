namespace Pb.Builder.Application.Session;

public sealed class DocumentStore
{
    private DocumentSession? currentSession;

    public event EventHandler? Changed;

    public DocumentSnapshot? CurrentSnapshot => currentSession is null
        ? null
        : new DocumentSnapshot(currentSession.Document, currentSession.DirectoryPath, currentSession.FileName);

    public void StartSession(DocumentSession session)
    {
        currentSession = session;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
