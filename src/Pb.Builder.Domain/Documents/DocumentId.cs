namespace Pb.Builder.Domain.Documents;

public readonly record struct DocumentId(string Value)
{
    public override string ToString() => Value;
}
