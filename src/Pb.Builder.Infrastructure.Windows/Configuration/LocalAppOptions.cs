using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Pb.Builder.Infrastructure.Windows.Configuration;

public sealed class LocalAppOptions
{
    public const string SectionName = "LocalApp";

    public string ProjectsRoot { get; set; } = string.Empty;

    public string TemplatesRoot { get; set; } = string.Empty;

    public bool LaunchBrowser { get; set; } = true;

    public string? PreferredUrl { get; set; }

    public string DataRoot { get; set; } = string.Empty;

    public static void ApplyLegacyConfiguration(LocalAppOptions options, IConfiguration configuration)
    {
        if (bool.TryParse(configuration["LaunchBrowser"], out var launchBrowser))
        {
            options.LaunchBrowser = launchBrowser;
        }

        var preferredUrl = configuration["PreferredUrl"];
        if (!string.IsNullOrWhiteSpace(preferredUrl))
        {
            options.PreferredUrl = preferredUrl;
        }
    }

    public static void ApplyDefaults(LocalAppOptions options, IHostEnvironment environment)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = environment.ContentRootPath;
        }

        var defaultDataRoot = Path.Combine(localAppData, "PbHtmlBuilder");

        options.DataRoot = ResolveRoot(options.DataRoot, environment.ContentRootPath, defaultDataRoot);
        options.ProjectsRoot = ResolveRoot(options.ProjectsRoot, environment.ContentRootPath, Path.Combine(options.DataRoot, "projects"));
        options.TemplatesRoot = ResolveRoot(options.TemplatesRoot, environment.ContentRootPath, Path.Combine(options.DataRoot, "templates"));
        options.PreferredUrl = string.IsNullOrWhiteSpace(options.PreferredUrl)
            ? null
            : options.PreferredUrl.Trim();
    }

    private static string ResolveRoot(string configuredPath, string contentRootPath, string defaultPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultPath
            : configuredPath.Trim();

        path = Environment.ExpandEnvironmentVariables(path);
        if (!Path.IsPathFullyQualified(path))
        {
            path = Path.Combine(contentRootPath, path);
        }

        return Path.GetFullPath(path);
    }
}
