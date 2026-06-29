using PbHtmlBuilder.Application.Projects;
using PbHtmlBuilder.Host.Configuration;

namespace PbHtmlBuilder.Host.Endpoints;

public sealed class ProjectFolderBrowser : IProjectFolderBrowser
{
    public const string DefaultTheoryFolder = "projects/theory";

    private readonly string _rootPath;

    public ProjectFolderBrowser(
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        var workingDirectory = configuration
            .GetSection($"{BuilderOptions.SectionName}:WorkingDirectory")
            .Get<WorkingDirectoryOptions>() ?? new WorkingDirectoryOptions();

        _rootPath = WorkingDirectoryRootResolver.Resolve(environment, workingDirectory);
    }

    public FolderBrowseResponse Browse(string? path)
    {
        return Browse(_rootPath, path, DefaultTheoryFolder);
    }

    public static FolderBrowseResponse Browse(
        string rootPath,
        string? path,
        string defaultPath = DefaultTheoryFolder)
    {
        var normalizedRoot = NormalizeFullPath(rootPath);
        var errors = new List<string>();
        var requestedPath = normalizedRoot;

        try
        {
            requestedPath = ResolveRequestedPath(normalizedRoot, path, defaultPath);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            errors.Add($"Folder path is invalid: {exception.Message}");
        }

        var requestedExists = Directory.Exists(requestedPath);
        var currentPath = FindNearestExistingDirectory(requestedPath) ?? normalizedRoot;

        if (!requestedExists && !IsDescendantMissingDirectory(requestedPath, currentPath))
        {
            errors.Add($"Folder is not available: {requestedPath}");
        }

        var entries = ReadEntries(currentPath, normalizedRoot, errors);
        var parent = Directory.GetParent(currentPath)?.FullName;

        return new FolderBrowseResponse(
            normalizedRoot,
            normalizedRoot,
            currentPath,
            currentPath,
            currentPath,
            requestedPath,
            requestedPath,
            ToDisplayPath(requestedPath),
            requestedExists,
            parent is null ? null : NormalizeFullPath(parent),
            parent is null ? null : NormalizeFullPath(parent),
            entries,
            errors);
    }

    private static string ResolveRequestedPath(string rootPath, string? path, string defaultPath)
    {
        var requested = string.IsNullOrWhiteSpace(path)
            ? defaultPath
            : path.Trim();

        if (requested == ".")
        {
            return rootPath;
        }

        if (Path.IsPathFullyQualified(requested) || Path.IsPathRooted(requested))
        {
            return NormalizeFullPath(requested);
        }

        return NormalizeFullPath(Path.Combine(rootPath, Path.Combine(requested.Split('/', '\\'))));
    }

    private static string? FindNearestExistingDirectory(string path)
    {
        var current = File.Exists(path)
            ? Path.GetDirectoryName(path)
            : path;

        while (!string.IsNullOrWhiteSpace(current))
        {
            var normalized = NormalizeFullPath(current);
            if (Directory.Exists(normalized))
            {
                return normalized;
            }

            current = Directory.GetParent(normalized)?.FullName;
        }

        return null;
    }

    private static bool IsDescendantMissingDirectory(string requestedPath, string currentPath)
    {
        return IsWithinRoot(requestedPath, currentPath)
            && !requestedPath.Equals(currentPath, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FolderBrowseEntry> ReadEntries(
        string currentPath,
        string rootPath,
        List<string> errors)
    {
        var entries = new List<FolderBrowseEntry>();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(currentPath))
            {
                entries.Add(BuildEntry(directory, rootPath, FolderBrowseEntryKind.Directory));
            }

            foreach (var file in Directory.EnumerateFiles(currentPath))
            {
                entries.Add(BuildEntry(file, rootPath, FolderBrowseEntryKind.File));
            }
        }
        catch (UnauthorizedAccessException exception)
        {
            errors.Add($"Access denied: {exception.Message}");
        }
        catch (IOException exception)
        {
            errors.Add($"Could not read folder: {exception.Message}");
        }

        return entries
            .OrderBy(entry => entry.Kind == FolderBrowseEntryKind.File)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static FolderBrowseEntry BuildEntry(
        string path,
        string rootPath,
        FolderBrowseEntryKind kind)
    {
        var fullPath = NormalizeFullPath(path);
        var folderPath = kind == FolderBrowseEntryKind.Directory
            ? fullPath
            : Path.GetDirectoryName(fullPath) ?? fullPath;

        return new FolderBrowseEntry(
            Path.GetFileName(fullPath),
            fullPath,
            folderPath,
            ToRelativePath(fullPath, rootPath),
            kind,
            IsAccessible: true,
            Error: null);
    }

    private static string ToDisplayPath(string path)
    {
        return NormalizeFullPath(path);
    }

    private static string? ToRelativePath(string path, string rootPath)
    {
        var normalizedPath = NormalizeFullPath(path);
        var normalizedRoot = NormalizeFullPath(rootPath);

        if (!IsWithinRoot(normalizedPath, normalizedRoot))
        {
            return null;
        }

        var relativePath = Path.GetRelativePath(normalizedRoot, normalizedPath).Replace('\\', '/');
        return relativePath == "." ? string.Empty : relativePath;
    }

    private static bool IsWithinRoot(string fullPath, string rootPath)
    {
        var normalizedPath = NormalizeFullPath(fullPath);
        var normalizedRoot = NormalizeFullPath(rootPath);
        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;

        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
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
