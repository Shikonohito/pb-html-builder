using Pb.Builder.Contracts.ProjectJson;
using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Contracts.Mapping;

public static class ProjectDtoMapper
{
    public static ProjectDocumentDto ToDto(Document document)
    {
        return new ProjectDocumentDto(
            new ProjectManifestDto(
                document.Manifest.AppId,
                document.Manifest.DocumentType.ToString(),
                document.Manifest.DocumentId.Value,
                document.Manifest.SchemaVersion.Value,
                document.Manifest.RuntimeVersion.Value,
                document.Manifest.CreatedAt),
            new ProjectSettingsDto(document.Settings.BrowserTitle));
    }
}
