namespace PbHtmlBuilder.Host.Configuration;

public static class WorkingDirectoryRootResolver
{
    public static string Resolve(IWebHostEnvironment environment, WorkingDirectoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        return Resolve(environment.ContentRootPath, options);
    }

    public static string Resolve(string contentRootPath, WorkingDirectoryOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.RootPath))
        {
            return NormalizeFullPath(options.RootPath);
        }

        return NormalizeFullPath(Path.Combine(contentRootPath, "..", ".."));
    }

    private static string NormalizeFullPath(string path)
    {
        return TrimTrailingDirectorySeparators(Path.GetFullPath(path));
    }

    private static string TrimTrailingDirectorySeparators(string path)
    {
        var root = Path.GetPathRoot(path);
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.IsNullOrEmpty(trimmed))
        {
            return path;
        }

        return !string.IsNullOrEmpty(root) && trimmed.Length < root.Length
            ? root
            : trimmed;
    }
}
