namespace PbHtmlBuilder.Host.Configuration;

public sealed class WorkingDirectoryOptions
{
    public string RootMode { get; set; } = "AppDirectory";

    public string? RootPath { get; set; }
}
