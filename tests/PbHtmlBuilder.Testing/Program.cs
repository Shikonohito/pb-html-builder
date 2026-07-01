using PbHtmlBuilder.Application.Theory;
using PbHtmlBuilder.Artifacts.Renderers;
using PbHtmlBuilder.Application.Projects;
using PbHtmlBuilder.Domain.Documents;
using PbHtmlBuilder.Host.Configuration;
using PbHtmlBuilder.Host.Endpoints;
using PbHtmlBuilder.Infrastructure.Projects;

var tests = new (string Name, Func<Task> Run)[]
{
    ("storage writes relative save targets inside root", StorageWritesRelativeTargetAsync),
    ("storage writes absolute save targets outside root", StorageWritesAbsoluteTargetAsync),
    ("storage rejects relative parent traversal", StorageRejectsRelativeParentTraversal),
    ("storage rejects file names that contain paths", StorageRejectsFileNamePaths),
    ("save use case requires create-directory confirmation", SaveUseCaseRequiresCreateDirectoryConfirmationAsync),
    ("save use case requires overwrite confirmation and creates backup", SaveUseCaseRequiresOverwriteAndCreatesBackupAsync),
    ("section inserter creates a default section in empty documents", SectionInserterCreatesDefaultSection),
    ("section inserter preserves section order around inserted sections", SectionInserterPreservesOrder),
    ("section inserter clamps out-of-range indexes", SectionInserterClampsIndexes),
    ("theory renderer excludes builder insertion tools", TheoryRendererExcludesBuilderInsertionTools),
    ("theory renderer saves section outline items", TheoryRendererSavesSectionOutlineItems),
    ("theory renderer saves builder shell shadows", TheoryRendererSavesBuilderShellShadows),
    ("save use case writes section outline items", SaveUseCaseWritesSectionOutlineItemsAsync),
    ("folder browser resolves default root from Host content root", FolderBrowserResolvesDefaultRoot),
    ("folder browser lists files and directories", FolderBrowserListsFilesAndDirectories),
    ("folder browser navigates above root", FolderBrowserNavigatesAboveRoot),
    ("folder browser returns absolute selection paths", FolderBrowserReturnsAbsoluteSelectionPaths),
    ("folder browser reports invalid path errors", FolderBrowserReportsInvalidPathErrors)
};

var failed = 0;
foreach (var (name, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}");
        Console.Error.WriteLine(exception);
    }
}

if (failed > 0)
{
    return 1;
}

Console.WriteLine($"All {tests.Length} tests passed.");
return 0;

static async Task StorageWritesRelativeTargetAsync()
{
    await using var scope = TestWorkspace.Create();
    var storage = scope.CreateStorage();

    var result = await storage.WriteHtmlAsync(
        DocumentTarget.Create("projects/theory", "lesson"),
        "<html></html>",
        new ProjectFileWriteOptions(CreateDirectory: true, Overwrite: false));

    AssertTrue(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
    AssertEqual("projects/theory/lesson.html", result.RelativePath);
    AssertTrue(File.Exists(Path.Combine(scope.RootPath, "projects", "theory", "lesson.html")));
}

static async Task StorageWritesAbsoluteTargetAsync()
{
    await using var scope = TestWorkspace.Create();
    var storage = scope.CreateStorage();

    var result = await storage.WriteHtmlAsync(
        DocumentTarget.Create(scope.OutsidePath, "absolute"),
        "<html></html>",
        new ProjectFileWriteOptions(CreateDirectory: true, Overwrite: false));

    AssertTrue(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
    AssertEqual(NormalizeFullPath(scope.OutsidePath), result.Target.FolderPath);
    AssertTrue(File.Exists(Path.Combine(scope.OutsidePath, "absolute.html")));
}

static Task StorageRejectsRelativeParentTraversal()
{
    using var scope = TestWorkspace.Create();
    var storage = scope.CreateStorage();

    var info = storage.Inspect(DocumentTarget.Create("../outside", "lesson.html"));

    AssertTrue(!info.IsValid);
    AssertContains(info.Errors, "parent traversal");
    return Task.CompletedTask;
}

static Task StorageRejectsFileNamePaths()
{
    using var scope = TestWorkspace.Create();
    var storage = scope.CreateStorage();

    var info = storage.Inspect(DocumentTarget.Create("projects/theory", "nested/lesson.html"));

    AssertTrue(!info.IsValid);
    AssertContains(info.Errors, "File name must not contain a path.");
    return Task.CompletedTask;
}

static async Task SaveUseCaseRequiresCreateDirectoryConfirmationAsync()
{
    await using var scope = TestWorkspace.Create();
    var useCase = scope.CreateSaveUseCase();
    var document = CreateDocument(DocumentTarget.Create("missing/theory", "lesson.html"));

    var result = await useCase.SaveAsync(document, new TheoryProjectSaveOptions());

    AssertEqual(ProjectSaveStatus.RequiresCreateDirectoryConfirmation, result.Status);
    AssertEqual("missing/theory/lesson.html", result.RelativePath);
}

static async Task SaveUseCaseRequiresOverwriteAndCreatesBackupAsync()
{
    await using var scope = TestWorkspace.Create();
    var folderPath = Path.Combine(scope.RootPath, "projects", "theory");
    Directory.CreateDirectory(folderPath);
    await File.WriteAllTextAsync(Path.Combine(folderPath, "lesson.html"), "old");

    var useCase = scope.CreateSaveUseCase();
    var document = CreateDocument(DocumentTarget.Create("projects/theory", "lesson.html"));

    var confirmation = await useCase.SaveAsync(document, new TheoryProjectSaveOptions());
    AssertEqual(ProjectSaveStatus.RequiresOverwriteConfirmation, confirmation.Status);

    var saved = await useCase.SaveAsync(
        document,
        new TheoryProjectSaveOptions(AllowOverwrite: true));

    AssertEqual(ProjectSaveStatus.Saved, saved.Status);
    AssertTrue(!string.IsNullOrWhiteSpace(saved.BackupRelativePath));
    AssertTrue(File.Exists(Path.Combine(
        scope.RootPath,
        saved.BackupRelativePath!.Replace('/', Path.DirectorySeparatorChar))));
}

static Task SectionInserterCreatesDefaultSection()
{
    var document = CreateDocument(DocumentTarget.Create("projects/theory", "lesson.html"));

    var updated = TheoryDocumentSectionInserter.Insert(document, 0);

    AssertEqual(1, updated.Sections.Count);
    AssertEqual(TheoryDocumentSectionInserter.DefaultSectionTitle, updated.Sections[0].Title);
    AssertTrue(updated.Sections[0].Id.StartsWith("sec_", StringComparison.Ordinal));
    AssertEqual(0, updated.SectionMapCells.Count);
    return Task.CompletedTask;
}

static Task SectionInserterPreservesOrder()
{
    var first = new TheorySection("sec_first", "First");
    var second = new TheorySection("sec_second", "Second");
    var document = CreateDocument(DocumentTarget.Create("projects/theory", "lesson.html")) with
    {
        Sections = [first, second]
    };

    var before = TheoryDocumentSectionInserter.Insert(document, 0);
    AssertEqual(3, before.Sections.Count);
    AssertEqual(TheoryDocumentSectionInserter.DefaultSectionTitle, before.Sections[0].Title);
    AssertEqual("sec_first", before.Sections[1].Id);
    AssertEqual("sec_second", before.Sections[2].Id);

    var middle = TheoryDocumentSectionInserter.Insert(document, 1);
    AssertEqual("sec_first", middle.Sections[0].Id);
    AssertEqual(TheoryDocumentSectionInserter.DefaultSectionTitle, middle.Sections[1].Title);
    AssertEqual("sec_second", middle.Sections[2].Id);

    var after = TheoryDocumentSectionInserter.Insert(document, 2);
    AssertEqual("sec_first", after.Sections[0].Id);
    AssertEqual("sec_second", after.Sections[1].Id);
    AssertEqual(TheoryDocumentSectionInserter.DefaultSectionTitle, after.Sections[2].Title);
    return Task.CompletedTask;
}

static Task SectionInserterClampsIndexes()
{
    var existing = new TheorySection("sec_existing", "Existing");
    var document = CreateDocument(DocumentTarget.Create("projects/theory", "lesson.html")) with
    {
        Sections = [existing]
    };

    var negative = TheoryDocumentSectionInserter.Insert(document, -10);
    AssertEqual(TheoryDocumentSectionInserter.DefaultSectionTitle, negative.Sections[0].Title);
    AssertEqual("sec_existing", negative.Sections[1].Id);

    var tooLarge = TheoryDocumentSectionInserter.Insert(document, 10);
    AssertEqual("sec_existing", tooLarge.Sections[0].Id);
    AssertEqual(TheoryDocumentSectionInserter.DefaultSectionTitle, tooLarge.Sections[1].Title);
    return Task.CompletedTask;
}

static Task TheoryRendererExcludesBuilderInsertionTools()
{
    var document = CreateDocument(DocumentTarget.Create("projects/theory", "lesson.html")) with
    {
        Sections = [new TheorySection("sec_rendered", "Rendered section")]
    };
    var renderer = new TheoryHtmlRenderer();

    var html = renderer.Render(document);

    AssertStringContains(html, "builder-section-heading section-heading");
    AssertDoesNotContain(html, "builder-insertion-rail");
    AssertDoesNotContain(html, "builder-insertion-button");
    AssertDoesNotContain(html, "lucide-plus-icon");
    return Task.CompletedTask;
}

static Task TheoryRendererSavesSectionOutlineItems()
{
    var document = CreateDocument(DocumentTarget.Create("projects/theory", "lesson.html")) with
    {
        Sections =
        [
            new TheorySection("sec_first", "First"),
            new TheorySection("sec_second", "Second")
        ]
    };
    var renderer = new TheoryHtmlRenderer();

    var html = renderer.Render(document);

    AssertStringContains(html, "<a class=\"builder-outline-item is-current\" href=\"#sec_first\" aria-current=\"true\">");
    AssertStringContains(html, "<span>01</span>");
    AssertStringContains(html, "<strong>First</strong>");
    AssertStringContains(html, "<a class=\"builder-outline-item\" href=\"#sec_second\">");
    AssertStringContains(html, "<span>02</span>");
    AssertStringContains(html, "<strong>Second</strong>");
    AssertDoesNotContain(html, "<strong>Section map</strong>");
    return Task.CompletedTask;
}

static Task TheoryRendererSavesBuilderShellShadows()
{
    var document = CreateDocument(DocumentTarget.Create("projects/theory", "lesson.html"));
    var renderer = new TheoryHtmlRenderer();

    var html = renderer.Render(document);

    AssertStringContains(html, "--builder-shadow-default: 20px 20px 10px rgba(0, 0, 0, 0.24);");
    AssertStringContains(html, "box-shadow: 20px 10px 10px rgba(0, 0, 0, 0.24);");
    AssertStringContains(html, "box-shadow: var(--builder-shadow-default);");
    AssertDoesNotContain(html, "box-shadow: 25px 25px 10px rgba(0, 0, 0, 0.24);");
    return Task.CompletedTask;
}

static async Task SaveUseCaseWritesSectionOutlineItemsAsync()
{
    await using var scope = TestWorkspace.Create();
    var document = CreateDocument(DocumentTarget.Create("projects/theory", "lesson.html")) with
    {
        Sections =
        [
            new TheorySection("sec_first", "First"),
            new TheorySection("sec_second", "Second")
        ]
    };
    var useCase = new TheoryProjectSaveUseCase(
        new FixedTimeProvider(),
        scope.CreateStorage(),
        new TheoryHtmlRenderer());

    var result = await useCase.SaveAsync(
        document,
        new TheoryProjectSaveOptions(AllowCreateDirectory: true));
    var savedHtml = await File.ReadAllTextAsync(Path.Combine(
        scope.RootPath,
        "projects",
        "theory",
        "lesson.html"));

    AssertEqual(ProjectSaveStatus.Saved, result.Status);
    AssertStringContains(savedHtml, "<a class=\"builder-outline-item is-current\" href=\"#sec_first\" aria-current=\"true\">");
    AssertStringContains(savedHtml, "<a class=\"builder-outline-item\" href=\"#sec_second\">");
    return;
}

static Task FolderBrowserResolvesDefaultRoot()
{
    using var scope = TestWorkspace.Create();
    var hostContentRoot = Path.Combine(scope.WorkspacePath, "repo", "src", "PbHtmlBuilder.Host");
    var expectedRoot = NormalizeFullPath(Path.Combine(hostContentRoot, "..", ".."));

    var resolvedRoot = WorkingDirectoryRootResolver.Resolve(hostContentRoot, new WorkingDirectoryOptions());
    var customRoot = WorkingDirectoryRootResolver.Resolve(
        hostContentRoot,
        new WorkingDirectoryOptions { RootPath = scope.OutsidePath });

    AssertEqual(expectedRoot, resolvedRoot);
    AssertEqual(NormalizeFullPath(scope.OutsidePath), customRoot);
    return Task.CompletedTask;
}

static Task FolderBrowserListsFilesAndDirectories()
{
    using var scope = TestWorkspace.Create();
    var theoryPath = Path.Combine(scope.RootPath, "projects", "theory");
    Directory.CreateDirectory(Path.Combine(theoryPath, "subfolder"));
    File.WriteAllText(Path.Combine(theoryPath, "lesson.html"), "html");

    var response = ProjectFolderBrowser.Browse(scope.RootPath, "projects/theory");

    AssertTrue(response.Errors.Count == 0, string.Join(Environment.NewLine, response.Errors));
    AssertEqual(NormalizeFullPath(theoryPath), response.CurrentSelectionPath);
    AssertTrue(response.Entries.Any(entry => entry.Name == "subfolder" && entry.Kind == FolderBrowseEntryKind.Directory));
    AssertTrue(response.Entries.Any(entry => entry.Name == "lesson.html" && entry.Kind == FolderBrowseEntryKind.File));

    var directoryIndex = Array.FindIndex(response.Entries.ToArray(), entry => entry.Name == "subfolder");
    var fileIndex = Array.FindIndex(response.Entries.ToArray(), entry => entry.Name == "lesson.html");
    AssertTrue(directoryIndex >= 0 && fileIndex >= 0 && directoryIndex < fileIndex);
    return Task.CompletedTask;
}

static Task FolderBrowserNavigatesAboveRoot()
{
    using var scope = TestWorkspace.Create();
    var rootResponse = ProjectFolderBrowser.Browse(scope.RootPath, ".");

    AssertTrue(rootResponse.ParentPath is not null);

    var parentResponse = ProjectFolderBrowser.Browse(scope.RootPath, rootResponse.ParentPath);

    AssertEqual(NormalizeFullPath(rootResponse.ParentPath!), parentResponse.CurrentPath);
    AssertTrue(Path.IsPathFullyQualified(parentResponse.CurrentSelectionPath));
    return Task.CompletedTask;
}

static Task FolderBrowserReturnsAbsoluteSelectionPaths()
{
    using var scope = TestWorkspace.Create();
    var theoryPath = Path.Combine(scope.RootPath, "projects", "theory");
    Directory.CreateDirectory(theoryPath);
    Directory.CreateDirectory(scope.OutsidePath);

    var relativeResponse = ProjectFolderBrowser.Browse(scope.RootPath, "projects/theory");
    var absoluteResponse = ProjectFolderBrowser.Browse(scope.RootPath, scope.OutsidePath);

    AssertEqual(NormalizeFullPath(theoryPath), relativeResponse.CurrentSelectionPath);
    AssertEqual(NormalizeFullPath(scope.OutsidePath), absoluteResponse.CurrentSelectionPath);
    return Task.CompletedTask;
}

static Task FolderBrowserReportsInvalidPathErrors()
{
    using var scope = TestWorkspace.Create();

    var response = ProjectFolderBrowser.Browse(scope.RootPath, "\0");

    AssertTrue(response.Errors.Count > 0);
    AssertContains(response.Errors, "invalid");
    return Task.CompletedTask;
}

static TheoryDocument CreateDocument(DocumentTarget target)
{
    var timestamp = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero);

    return new TheoryDocument(
        "doc_test",
        SchemaVersion: 1,
        BuilderVersion: "test",
        DocumentTitle: "Test lesson",
        target,
        new BrandMetadata("Topic", "Copyright", "Brand"),
        new RuntimeMetadata("local", "/runtime", "monaco", "/monaco", "pyodide", "/pyodide"),
        timestamp,
        timestamp,
        [],
        []);
}

static string NormalizeFullPath(string path)
{
    var fullPath = Path.GetFullPath(path);
    var root = Path.GetPathRoot(fullPath);
    var trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    return !string.IsNullOrEmpty(root) && trimmed.Length < root.Length
        ? root
        : trimmed;
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
    }
}

static void AssertTrue(bool condition, string? message = null)
{
    if (!condition)
    {
        throw new InvalidOperationException(message ?? "Expected condition to be true.");
    }
}

static void AssertContains(IEnumerable<string> values, string expected)
{
    if (!values.Any(value => value.Contains(expected, StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException($"Expected one value to contain '{expected}'. Values: {string.Join(" | ", values)}");
    }
}

static void AssertStringContains(string value, string expected)
{
    if (!value.Contains(expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected value to contain '{expected}'.");
    }
}

static void AssertDoesNotContain(string value, string expected)
{
    if (value.Contains(expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected value not to contain '{expected}'.");
    }
}

sealed class FakeTheoryHtmlRenderer : ITheoryHtmlRenderer
{
    public string Render(TheoryDocument document)
    {
        return $"<html><body>{document.Target.DisplayPath}</body></html>";
    }
}

sealed class FixedTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero);
    }
}

sealed class TestWorkspace : IDisposable, IAsyncDisposable
{
    private TestWorkspace(string workspacePath)
    {
        WorkspacePath = workspacePath;
        RootPath = Path.Combine(workspacePath, "root");
        OutsidePath = Path.Combine(workspacePath, "outside");
        Directory.CreateDirectory(RootPath);
    }

    public string WorkspacePath { get; }

    public string RootPath { get; }

    public string OutsidePath { get; }

    public static TestWorkspace Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "pb-html-builder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TestWorkspace(path);
    }

    public WorkingDirectoryProjectFileStorage CreateStorage()
    {
        return new WorkingDirectoryProjectFileStorage(RootPath, new FixedTimeProvider());
    }

    public TheoryProjectSaveUseCase CreateSaveUseCase()
    {
        return new TheoryProjectSaveUseCase(
            new FixedTimeProvider(),
            CreateStorage(),
            new FakeTheoryHtmlRenderer());
    }

    public void Dispose()
    {
        DeleteWorkspace();
    }

    public ValueTask DisposeAsync()
    {
        DeleteWorkspace();
        return ValueTask.CompletedTask;
    }

    private void DeleteWorkspace()
    {
        if (Directory.Exists(WorkspacePath))
        {
            Directory.Delete(WorkspacePath, recursive: true);
        }
    }
}
