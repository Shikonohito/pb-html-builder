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
