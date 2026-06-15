using Pb.Builder.Application.Extensibility;
using Pb.Builder.Application.Ports;
using Pb.Builder.Domain.Documents;
using Pb.Builder.Infrastructure.Windows.Configuration;

namespace Pb.Builder.Infrastructure.Windows.FileSystem;

public sealed class WindowsProjectPathService(
    LocalAppOptions localAppOptions,
    DocumentTypeRegistry documentTypeRegistry) : IProjectPathService
{
    private const int MaximumWindowsPathLength = 260;
    private const string DefaultFileExtension = ".html";
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private static readonly HashSet<string> ReservedWindowsFileNames = new(StringComparer.OrdinalIgnoreCase)
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

    public string GetDefaultProjectDirectory(DocumentType documentType)
    {
        var module = documentTypeRegistry.GetRequired(documentType);

        return Path.GetFullPath(Path.Combine(localAppOptions.ProjectsRoot, module.DefaultSubdirectoryName));
    }

    public ProjectPathResult NormalizeHtmlFileName(string fileName)
    {
        var normalizedFileName = fileName.TrimStart();
        if (string.IsNullOrWhiteSpace(normalizedFileName))
        {
            return ProjectPathResult.Failure("File name cannot be empty.");
        }

        if (normalizedFileName.EndsWith(' '))
        {
            return ProjectPathResult.Failure("File name cannot end with a space.");
        }

        if (normalizedFileName.EndsWith('.'))
        {
            return ProjectPathResult.Failure("File name cannot end with a dot.");
        }

        if (normalizedFileName.Contains('/') || normalizedFileName.Contains('\\'))
        {
            return ProjectPathResult.Failure("File name cannot contain slash or backslash.");
        }

        if (normalizedFileName.Contains(':'))
        {
            return ProjectPathResult.Failure("File name cannot contain a colon.");
        }

        if (normalizedFileName.IndexOfAny(InvalidFileNameChars) >= 0)
        {
            return ProjectPathResult.Failure("File name contains invalid Windows characters.");
        }

        var reservedNameCandidate = normalizedFileName.Split('.', 2)[0];
        if (ReservedWindowsFileNames.Contains(reservedNameCandidate))
        {
            return ProjectPathResult.Failure("File name cannot use a reserved Windows device name.");
        }

        var extension = Path.GetExtension(normalizedFileName);
        if (string.IsNullOrEmpty(extension))
        {
            return ProjectPathResult.Success(normalizedFileName + DefaultFileExtension);
        }

        if (!string.Equals(extension, DefaultFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return ProjectPathResult.Failure("Use .html as the file extension.");
        }

        return ProjectPathResult.Success(Path.ChangeExtension(normalizedFileName, DefaultFileExtension));
    }

    public ProjectPathResult ResolveDirectoryPath(string directoryPath)
    {
        var normalizedDirectoryPath = directoryPath.Trim();

        try
        {
            if (!Path.IsPathFullyQualified(normalizedDirectoryPath))
            {
                return ProjectPathResult.Failure("Folder must be an absolute path.");
            }

            return ProjectPathResult.Success(Path.GetFullPath(normalizedDirectoryPath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ProjectPathResult.Failure($"Folder path is invalid: {exception.Message}");
        }
    }

    public ProjectPathResult ValidateProjectFilePath(string directoryPath, string fileName)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(directoryPath, fileName));
            if (fullPath.Length >= MaximumWindowsPathLength)
            {
                return ProjectPathResult.Failure("Full file path is too long. Use a shorter folder or file name.");
            }

            return ProjectPathResult.Success(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ProjectPathResult.Failure($"File path is invalid: {exception.Message}");
        }
    }

    public ProjectPathResult EnsureDirectory(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            return ProjectPathResult.Success(directoryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return ProjectPathResult.Failure($"Cannot create folder: {exception.Message}");
        }
    }
}
