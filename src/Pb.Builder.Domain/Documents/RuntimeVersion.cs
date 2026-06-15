namespace Pb.Builder.Domain.Documents;

public readonly record struct RuntimeVersion(string Value)
{
    public static RuntimeVersion Current { get; } = new("1.0.0");

    public override string ToString() => Value;
}
