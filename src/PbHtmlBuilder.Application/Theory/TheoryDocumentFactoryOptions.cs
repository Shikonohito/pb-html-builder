using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Application.Theory;

public sealed class TheoryDocumentFactoryOptions
{
    public string BuilderVersion { get; init; } = "dev";

    public BrandMetadata Brand { get; init; } = new(
        "Python Bootcamp",
        "© Ivan Ivanov, 2026",
        "PbHtmlBuilder");

    public RuntimeMetadata Runtime { get; init; } = new(
        "local",
        "/runtime/student/",
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);
}
