namespace Pb.Builder.Contracts.ProjectJson;

public sealed record ProjectManifestDto(
    string AppId,
    string DocumentType,
    string DocumentId,
    string SchemaVersion,
    string RuntimeVersion,
    DateTimeOffset CreatedAt);
