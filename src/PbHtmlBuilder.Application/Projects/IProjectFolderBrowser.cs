namespace PbHtmlBuilder.Application.Projects;

public interface IProjectFolderBrowser
{
    FolderBrowseResponse Browse(string? path);
}
