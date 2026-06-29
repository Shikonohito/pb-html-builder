namespace PbHtmlBuilder.Domain.Documents;

public sealed record RuntimeMetadata(
    string RuntimeChannel,
    string RuntimeBaseUrl,
    string MonacoVersion,
    string MonacoBaseUrl,
    string PyodideVersion,
    string PyodideBaseUrl);
