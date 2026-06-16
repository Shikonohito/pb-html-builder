namespace PbHtmlBuilder.Host.Configuration;

public sealed class DependencyVersionOptions
{
    public string MonacoVersion { get; set; } = string.Empty;

    public string MonacoBaseUrl { get; set; } = string.Empty;

    public string PyodideVersion { get; set; } = string.Empty;

    public string PyodideBaseUrl { get; set; } = string.Empty;
}
