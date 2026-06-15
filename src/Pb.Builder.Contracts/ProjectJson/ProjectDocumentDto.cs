namespace Pb.Builder.Contracts.ProjectJson;

public sealed record ProjectDocumentDto(
    ProjectManifestDto Manifest,
    ProjectSettingsDto Settings);
