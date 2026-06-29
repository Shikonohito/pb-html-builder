using PbHtmlBuilder.Domain.Documents;
using PbHtmlBuilder.Domain.Ids;

namespace PbHtmlBuilder.Application.Theory;

public sealed class TheoryDocumentFactory
{
    private static readonly string[] DefaultMapCellTitles =
    [
        "Заголовок",
        "Заголовок",
        "Заголовок",
        "Заголовок",
        "Заголовок",
        "Заголовок"
    ];

    private readonly TimeProvider _clock;
    private readonly TheoryDocumentFactoryOptions _options;

    public TheoryDocumentFactory(TheoryDocumentFactoryOptions options, TimeProvider clock)
    {
        _options = options;
        _clock = clock;
    }

    public TheoryDocument CreateNew(TheoryDocumentCreateRequest request)
    {
        var now = _clock.GetUtcNow();
        var section = new TheorySection(StableIdGenerator.NewId("sec"), "Introduction");

        return new TheoryDocument(
            StableIdGenerator.NewId("doc"),
            1,
            _options.BuilderVersion,
            ResolveTitle(request.DocumentTitle, request.FileName),
            ResolveTarget(request.FolderPath, request.FileName),
            _options.Brand,
            _options.Runtime,
            now,
            now,
            DefaultMapCellTitles
                .Select(title => new TheorySectionMapCell(StableIdGenerator.NewId("map"), title, section.Id))
                .ToArray(),
            [section]);
    }

    private static DocumentTarget ResolveTarget(string? folderPath, string? fileName)
    {
        return DocumentTarget.Create(
            string.IsNullOrWhiteSpace(folderPath) ? "projects/theory" : folderPath,
            string.IsNullOrWhiteSpace(fileName) ? "lesson-theory.html" : fileName);
    }

    private static string ResolveTitle(string? title, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var trimmedFileName = fileName.Trim();
            var extensionIndex = trimmedFileName.LastIndexOf('.');
            return extensionIndex > 0 ? trimmedFileName[..extensionIndex] : trimmedFileName;
        }

        return "Название урока";
    }
}
