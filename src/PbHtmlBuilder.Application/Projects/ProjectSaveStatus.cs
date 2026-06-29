namespace PbHtmlBuilder.Application.Projects;

public enum ProjectSaveStatus
{
    Saved,
    RequiresCreateDirectoryConfirmation,
    RequiresOverwriteConfirmation,
    ValidationFailed
}
