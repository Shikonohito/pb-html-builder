using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Pb.Builder.Application.Ports;

namespace Pb.Builder.Infrastructure.Windows.Dialogs;

public sealed class WindowsFileDialogService : IFileDialogService
{
    private const string DialogTitle = "Select project folder";
    private static readonly object VisualStylesLock = new();
    private static bool visualStylesEnabled;

    public Task<string?> PickFolderAsync(string? initialDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<string?>(null);
        }

        var foregroundWindow = GetForegroundWindow();
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                EnsureVisualStylesEnabled();

                using var dialog = new FolderBrowserDialog
                {
                    Description = DialogTitle,
                    UseDescriptionForTitle = true,
                    SelectedPath = GetExistingInitialDirectory(initialDirectory)
                };
                using var owner = new WindowHandleOwner(foregroundWindow);

                var result = foregroundWindow == IntPtr.Zero
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(owner);
                completion.TrySetResult(result == DialogResult.OK ? dialog.SelectedPath : null);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task;
    }

    private static void EnsureVisualStylesEnabled()
    {
        lock (VisualStylesLock)
        {
            if (visualStylesEnabled)
            {
                return;
            }

            System.Windows.Forms.Application.EnableVisualStyles();
            visualStylesEnabled = true;
        }
    }

    private static string GetExistingInitialDirectory(string? initialDirectory)
    {
        if (string.IsNullOrWhiteSpace(initialDirectory))
        {
            return string.Empty;
        }

        try
        {
            var directory = new DirectoryInfo(initialDirectory.Trim());
            while (directory is not null)
            {
                if (directory.Exists)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return string.Empty;
    }

    private sealed class WindowHandleOwner(IntPtr handle) : IWin32Window, IDisposable
    {
        public IntPtr Handle { get; } = handle;

        public void Dispose()
        {
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
