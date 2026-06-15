using System.Text.Json;
using Pb.Builder.Contracts.ProjectJson;

namespace Pb.Builder.Artifacts.Html;

public sealed class MetadataScriptBlockWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string WriteProjectMetadata(ProjectDocumentDto projectDocument)
    {
        var json = JsonSerializer.Serialize(projectDocument, JsonOptions);
        return $"""<script type="application/json" id="pb-project">{json}</script>""";
    }
}
