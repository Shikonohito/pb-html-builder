namespace PbHtmlBuilder.Host.Configuration;

public sealed class RuntimeAssetOptions
{
    public string DevelopmentBaseUrl { get; set; } = "/runtime/student/";

    public string ExportBaseUrl { get; set; } = string.Empty;

    public string RuntimeChannel { get; set; } = "local";
}
