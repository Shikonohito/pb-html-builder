namespace PbHtmlBuilder.Domain.Documents;

public sealed record DocumentTarget(string FolderPath, string FileName)
{
    public string DisplayPath => string.IsNullOrWhiteSpace(FolderPath)
        ? FileName
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

        return value.Trim().Replace('\\', '/').Trim('/');
    }
}
