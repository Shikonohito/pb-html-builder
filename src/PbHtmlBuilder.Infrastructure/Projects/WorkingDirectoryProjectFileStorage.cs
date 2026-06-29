using System.Text;
using PbHtmlBuilder.Application.Projects;
using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Infrastructure.Projects;

public sealed class WorkingDirectoryProjectFileStorage : IProjectFileStorage
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9"
    };

    private readonly TimeProvider _clock;
    private readonly string _rootPath;

    public WorkingDirectoryProjectFileStorage(string rootPath, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _rootPath = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _clock = clock;
    }

    public ProjectFileInfo Inspect(DocumentTarget target)
    {
        var resolved = Resolve(target);
        if (resolved.Errors.Count > 0)
        {
            return new ProjectFileInfo(
                target,
                target.DisplayPath,
                DirectoryExists: false,
                FileExists: false,
                LastModifiedAt: null,
                resolved.Errors);
        }

        var fileExists = File.Exists(resolved.FullPath);
        return new ProjectFileInfo(
            resolved.Target,
            resolved.RelativePath,
            Directory.Exists(resolved.DirectoryPath),
            fileExists,
            fileExists ? File.GetLastWriteTimeUtc(resolved.FullPath) : null,
            []);
    }

    public async Task<ProjectFileWriteResult> WriteHtmlAsync(
        DocumentTarget target,
        string content,
        ProjectFileWriteOptions options,
        CancellationToken cancellationToken = default)
    {
        var resolved = Resolve(target);
        if (resolved.Errors.Count > 0)
        {
            return new ProjectFileWriteResult(target, target.DisplayPath, null, resolved.Errors);
        }

        if (!Directory.Exists(resolved.DirectoryPath))
        {
            if (!options.CreateDirectory)
            {
                return new ProjectFileWriteResult(
                    resolved.Target,
                    resolved.RelativePath,
                    null,
                    [$"Folder does not exist: {resolved.Target.FolderPath}"]);
            }

            Directory.CreateDirectory(resolved.DirectoryPath);
        }

        string? backupRelativePath = null;
        if (File.Exists(resolved.FullPath))
        {
            if (!options.Overwrite)
            {
                return new ProjectFileWriteResult(
                    resolved.Target,
                    resolved.RelativePath,
                    null,
                    [$"File already exists: {resolved.RelativePath}"]);
            }

            backupRelativePath = CreateBackup(resolved);
        }

        await File.WriteAllTextAsync(
            resolved.FullPath,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        return new ProjectFileWriteResult(
            resolved.Target,
            resolved.RelativePath,
            backupRelativePath,
            []);
    }

    private string CreateBackup(ResolvedProjectPath resolved)
    {
        var timestamp = _clock.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'");
        var fileName = Path.GetFileNameWithoutExtension(resolved.Target.FileName);
        var backupFileName = $"{fileName}.{timestamp}.bak.html";
        var backupFullPath = Path.Combine(resolved.DirectoryPath, backupFileName);

        File.Copy(resolved.FullPath, backupFullPath, overwrite: false);

        return string.IsNullOrWhiteSpace(resolved.Target.FolderPath)
            ? backupFileName
            : $"{resolved.Target.FolderPath}/{backupFileName}";
    }

    private ResolvedProjectPath Resolve(DocumentTarget target)
    {
        var errors = new List<string>();
        var folderPath = NormalizeFolderPath(target.FolderPath, errors);
        var fileName = NormalizeFileName(target.FileName, errors);

        if (errors.Count > 0)
        {
            return ResolvedProjectPath.Invalid(errors);
        }

        var directoryPath = string.IsNullOrWhiteSpace(folderPath)
            ? _rootPath
            : Path.Combine(_rootPath, Path.Combine(folderPath.Split('/')));
        var fullPath = Path.GetFullPath(Path.Combine(directoryPath, fileName));

        if (!IsWithinRoot(fullPath))
        {
            errors.Add("Path resolves outside the app working directory.");
            return ResolvedProjectPath.Invalid(errors);
        }

        var normalizedTarget = new DocumentTarget(folderPath, fileName);
        var relativePath = string.IsNullOrWhiteSpace(folderPath)
            ? fileName
            : $"{folderPath}/{fileName}";

        return new ResolvedProjectPath(
            normalizedTarget,
            relativePath,
            Path.GetDirectoryName(fullPath) ?? _rootPath,
            fullPath,
            []);
    }

    private bool IsWithinRoot(string fullPath)
    {
        var rootWithSeparator = _rootPath + Path.DirectorySeparatorChar;
        return fullPath.Equals(_rootPath, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFolderPath(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == ".")
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        if (Path.IsPathRooted(normalized) || Path.IsPathFullyQualified(normalized))
        {
            errors.Add("Folder path must be relative.");
            return normalized;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            ValidatePathSegment(segment, "Folder path", errors);
        }

        return string.Join('/', segments);
    }

    private static string NormalizeFileName(string? value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("File name is required.");
            return string.Empty;
        }

        var fileName = value.Trim();
        if (fileName.Contains('/') || fileName.Contains('\\') || Path.IsPathRooted(fileName))
        {
            errors.Add("File name must not contain a path.");
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            fileName += ".html";
        }
        else if (!extension.Equals(".html", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Theory files must use the .html extension.");
        }

        ValidatePathSegment(fileName, "File name", errors);
        return fileName;
    }

    private static void ValidatePathSegment(string segment, string label, List<string> errors)
    {
        if (segment is "." or "..")
        {
            errors.Add($"{label} must not contain parent traversal.");
            return;
        }

        if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            errors.Add($"{label} contains invalid Windows filename characters.");
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(segment);
        if (ReservedNames.Contains(nameWithoutExtension))
        {
            errors.Add($"{label} contains a reserved Windows name: {segment}.");
        }

        if (segment.EndsWith(' ') || segment.EndsWith('.'))
        {
            errors.Add($"{label} segments must not end with a space or period.");
        }
    }

    private sealed record ResolvedProjectPath(
        DocumentTarget Target,
        string RelativePath,
        string DirectoryPath,
        string FullPath,
        IReadOnlyList<string> Errors)
    {
        public static ResolvedProjectPath Invalid(IReadOnlyList<string> errors)
        {
            return new ResolvedProjectPath(
                DocumentTarget.Create(null, null),
                string.Empty,
                string.Empty,
                string.Empty,
                errors);
        }
    }
}
