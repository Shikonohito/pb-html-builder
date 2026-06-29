namespace PbHtmlBuilder.Application.Projects;

public sealed record ProjectFileWriteOptions(
    bool CreateDirectory,
    bool Overwrite);
