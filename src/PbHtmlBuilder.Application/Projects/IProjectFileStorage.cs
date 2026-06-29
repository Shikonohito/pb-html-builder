using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Application.Projects;

public interface IProjectFileStorage
{
    ProjectFileInfo Inspect(DocumentTarget target);

    Task<ProjectFileWriteResult> WriteHtmlAsync(
        DocumentTarget target,
        string content,
        ProjectFileWriteOptions options,
        CancellationToken cancellationToken = default);
}
