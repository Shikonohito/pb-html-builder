using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Application.Ports;

public interface IProjectPathService
{
    string GetDefaultProjectDirectory(DocumentType documentType);

    ProjectPathResult NormalizeHtmlFileName(string fileName);

    ProjectPathResult ResolveDirectoryPath(string directoryPath);

    ProjectPathResult ValidateProjectFilePath(string directoryPath, string fileName);

    ProjectPathResult EnsureDirectory(string directoryPath);
}
