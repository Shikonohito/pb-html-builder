using System.Globalization;
using System.Text;
using PbHtmlBuilder.Application.Projects;
using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Infrastructure.Projects;

public sealed class WorkingDirectoryProjectFileStorage : IProjectFileStorage
{
    private const int MaxBackupSlots = 10;
    private const int BackupTimestampLength = 16;
    private const string BackupSuffix = ".bak.html";

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

        _rootPath = TrimTrailingDirectorySeparators(Path.GetFullPath(rootPath));
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
        var baseFileName = Path.GetFileNameWithoutExtension(resolved.Target.FileName);
        PruneOldBackups(resolved.DirectoryPath, baseFileName);

        var backupFileName = GetAvailableBackupFileName(
            resolved.DirectoryPath,
            baseFileName,
            timestamp);
        var backupFullPath = Path.Combine(resolved.DirectoryPath, backupFileName);

        File.Copy(resolved.FullPath, backupFullPath, overwrite: false);

        return BuildDisplayPath(resolved.Target.FolderPath, backupFileName);
    }

    private static void PruneOldBackups(string directoryPath, string baseFileName)
    {
        var backups = EnumerateBackupFiles(directoryPath, baseFileName)
            .OrderBy(backup => backup.Timestamp, StringComparer.Ordinal)
            .ThenBy(backup => backup.Suffix)
            .ThenBy(backup => backup.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var backupDeleteCount = backups.Count - MaxBackupSlots + 1;
        if (backupDeleteCount <= 0)
        {
            return;
        }

        foreach (var backup in backups.Take(backupDeleteCount))
        {
            File.Delete(backup.FullPath);
        }
    }

    private static IEnumerable<BackupFile> EnumerateBackupFiles(string directoryPath, string baseFileName)
    {
        foreach (var fullPath in Directory.EnumerateFiles(directoryPath, $"*{BackupSuffix}", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(fullPath);
            if (TryParseBackupFileName(fileName, baseFileName, out var timestamp, out var suffix))
            {
                yield return new BackupFile(fullPath, fileName, timestamp, suffix);
            }
        }
    }

    private static bool TryParseBackupFileName(
        string fileName,
        string baseFileName,
        out string timestamp,
        out int suffix)
    {
        timestamp = string.Empty;
        suffix = 0;

        var prefix = $"{baseFileName}.";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var backupMarkerStart = fileName.Length - BackupSuffix.Length;
        var version = fileName[prefix.Length..backupMarkerStart];
        if (version.Length == BackupTimestampLength)
        {
            timestamp = version;
            return IsBackupTimestamp(timestamp);
        }

        if (version.Length <= BackupTimestampLength
            || version[BackupTimestampLength] != '.')
        {
            return false;
        }

        timestamp = version[..BackupTimestampLength];
        var suffixText = version[(BackupTimestampLength + 1)..];
        return IsBackupTimestamp(timestamp)
            && TryParsePositiveInt(suffixText, out suffix);
    }

    private static string GetAvailableBackupFileName(
        string directoryPath,
        string baseFileName,
        string timestamp)
    {
        var sameTimestampBackups = EnumerateBackupFiles(directoryPath, baseFileName)
            .Where(backup => backup.Timestamp.Equals(timestamp, StringComparison.Ordinal))
            .ToList();
        var suffix = 0;
        if (sameTimestampBackups.Count > 0)
        {
            var maxSuffix = sameTimestampBackups.Max(backup => backup.Suffix);
            if (maxSuffix == int.MaxValue)
            {
                throw new IOException($"No backup slot is available for {baseFileName} at {timestamp}.");
            }

            suffix = maxSuffix + 1;
        }

        for (; suffix < int.MaxValue; suffix++)
        {
            var backupFileName = BuildBackupFileName(baseFileName, timestamp, suffix);
            if (!File.Exists(Path.Combine(directoryPath, backupFileName)))
            {
                return backupFileName;
            }
        }

        throw new IOException($"No backup slot is available for {baseFileName} at {timestamp}.");
    }

    private static string BuildBackupFileName(string baseFileName, string timestamp, int suffix)
    {
        return suffix == 0
            ? $"{baseFileName}.{timestamp}{BackupSuffix}"
            : $"{baseFileName}.{timestamp}.{suffix}{BackupSuffix}";
    }

    private static bool IsBackupTimestamp(string value)
    {
        return DateTimeOffset.TryParseExact(
            value,
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out _);
    }

    private static bool TryParsePositiveInt(string value, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return int.TryParse(value, CultureInfo.InvariantCulture, out result)
            && result > 0;
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

        var folderIsAbsolute = IsAbsoluteFolderPath(folderPath);
        var directoryPath = string.IsNullOrWhiteSpace(folderPath)
            ? _rootPath
            : folderIsAbsolute
                ? folderPath
                : Path.Combine(_rootPath, Path.Combine(folderPath.Split('/')));
        var fullPath = Path.GetFullPath(Path.Combine(directoryPath, fileName));

        if (!folderIsAbsolute && !IsWithinRoot(fullPath))
        {
            errors.Add("Path resolves outside the app working directory.");
            return ResolvedProjectPath.Invalid(errors);
        }

        var normalizedTarget = new DocumentTarget(folderPath, fileName);
        var relativePath = BuildDisplayPath(folderPath, fileName);

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

        var trimmed = value.Trim();
        if (IsAbsoluteFolderPath(trimmed))
        {
            return NormalizeAbsoluteFolderPath(trimmed, errors);
        }

        var normalized = trimmed.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            ValidatePathSegment(segment, "Folder path", errors);
        }

        return string.Join('/', segments);
    }

    private static string NormalizeAbsoluteFolderPath(string value, List<string> errors)
    {
        try
        {
            return TrimTrailingDirectorySeparators(Path.GetFullPath(value));
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            errors.Add($"Folder path is invalid: {exception.Message}");
            return value;
        }
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

    private static string BuildDisplayPath(string folderPath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return fileName;
        }

        return IsAbsoluteFolderPath(folderPath)
            ? Path.Combine(folderPath, fileName)
            : $"{folderPath}/{fileName}";
    }

    private static bool IsAbsoluteFolderPath(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (Path.IsPathFullyQualified(value) || Path.IsPathRooted(value));
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

    private sealed record BackupFile(string FullPath, string FileName, string Timestamp, int Suffix);

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
