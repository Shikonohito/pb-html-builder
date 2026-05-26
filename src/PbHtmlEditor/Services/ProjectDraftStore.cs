namespace PbHtmlEditor.Services;

public sealed class ProjectDraftStore(IHostEnvironment environment)
{
    public ProjectDraft? LastCreated { get; private set; }

    public string GetDefaultDirectory(ProjectKind kind)
    {
        var projectRoot = FindProjectRoot(environment.ContentRootPath);
        var subdirectory = kind == ProjectKind.Theory ? "theory" : "practice";

        return Path.GetFullPath(Path.Combine(projectRoot, "projects", subdirectory));
    }

    public void Remember(ProjectDraft draft)
    {
        LastCreated = draft;
    }

    private static string FindProjectRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);

        while (directory is not null)
        {
            var requirementsPath = Path.Combine(directory.FullName, "docs", "tool-requirements.txt");
            if (File.Exists(requirementsPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return startPath;
    }
}

public sealed record ProjectDraft(ProjectKind Kind, string DirectoryPath, string FileName, string BrowserTitle);

public enum ProjectKind
{
    Theory,
    Practice
}
