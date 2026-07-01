using PbHtmlBuilder.Domain.Documents;
using PbHtmlBuilder.Domain.Ids;

namespace PbHtmlBuilder.Application.Theory;

public static class TheoryDocumentSectionInserter
{
    public const string DefaultSectionTitle = "Заголовок";

    public static TheoryDocument Insert(TheoryDocument document, int index)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sections = document.Sections.ToList();
        var insertionIndex = Math.Clamp(index, 0, sections.Count);
        sections.Insert(
            insertionIndex,
            new TheorySection(StableIdGenerator.NewId("sec"), DefaultSectionTitle));

        return document with
        {
            Sections = sections
        };
    }
}
