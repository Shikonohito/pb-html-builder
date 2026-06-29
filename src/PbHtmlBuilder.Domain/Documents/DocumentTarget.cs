namespace PbHtmlBuilder.Domain.Documents;

public sealed record DocumentTarget(string FolderPath, string FileName)
{
    public string DisplayPath => string.IsNullOrWhiteSpace(FolderPath)
        ? FileName
        : IsAbsoluteFolderPath(FolderPath)
            ? Path.Combine(FolderPath.Trim(), FileName)
            : $"{FolderPath.Trim().Replace('\\', '/')}/{FileName}";

    public static DocumentTarget Create(string? folderPath, string? fileName)
    {
        return new DocumentTarget(
            NormalizeFolderPath(folderPath),
            string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName.Trim());
    }

    private static string NormalizeFolderPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == ".")
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (IsAbsoluteFolderPath(trimmed))
        {
            try
            {
                return TrimTrailingDirectorySeparators(Path.GetFullPath(trimmed));
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                return trimmed;
            }
        }

        return trimmed.Replace('\\', '/').Trim('/');
    }

    private static bool IsAbsoluteFolderPath(string value)
    {
        return Path.IsPathFullyQualified(value) || Path.IsPathRooted(value);
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
