using Microsoft.Extensions.Options;

namespace PbHtmlBuilder.Services;

public sealed class ProjectDraftStore(IOptions<LocalAppOptions> localAppOptions)
{
    public ProjectDraft? LastCreated { get; private set; }

    public string GetDefaultDirectory(ProjectKind kind)
    {
        var subdirectory = kind == ProjectKind.Theory ? "theory" : "practice";

        return Path.GetFullPath(Path.Combine(localAppOptions.Value.ProjectsRoot, subdirectory));
    }

    public void Remember(ProjectDraft draft)
    {
        LastCreated = draft;
    }
}

public sealed record ProjectDraft(ProjectKind Kind, string DirectoryPath, string FileName, string BrowserTitle);

public enum ProjectKind
{
    Theory,
    Practice
}
