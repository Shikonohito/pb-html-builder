namespace Pb.Builder.Domain.Documents;

public readonly record struct SchemaVersion(string Value)
{
    public static SchemaVersion Current { get; } = new("1.0");

    public override string ToString() => Value;
}
