using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Application.Projects;

public interface ITheoryHtmlRenderer
{
    string Render(TheoryDocument document);
}
