using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Application.Projects;

public sealed record TheoryProjectSaveOptions(
    DocumentTarget? TargetOverride = null,
    bool SaveAs = false,
    bool AllowCreateDirectory = false,
    bool AllowOverwrite = false);
