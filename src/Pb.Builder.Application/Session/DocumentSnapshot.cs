using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Application.Session;

public sealed record DocumentSnapshot(Document Document, string DirectoryPath, string FileName)
{
    public string ProjectPath => Path.Combine(DirectoryPath, FileName);
}
