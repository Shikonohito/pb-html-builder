namespace PbHtmlBuilder.Host.Configuration;

public sealed class BuilderOptions
{
    public const string SectionName = "PbHtmlBuilder";

    public WorkingDirectoryOptions WorkingDirectory { get; set; } = new();

    public RuntimeAssetOptions RuntimeAssets { get; set; } = new();

    public DependencyVersionOptions Dependencies { get; set; } = new();

    public BrandDefaultsOptions Defaults { get; set; } = new();
}
