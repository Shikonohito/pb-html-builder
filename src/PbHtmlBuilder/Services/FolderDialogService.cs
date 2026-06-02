using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PbHtmlBuilder.Services;

public sealed class FolderDialogService
{
    private const string DialogTitle = "Select project folder";

    public Task<string?> PickFolderAsync(string? initialDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<string?>(null);
        }

        var foregroundWindow = GetForegroundWindow();
        var completion = new TaskCompletionSource<string?>();
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();

                using var dialog = new FolderBrowserDialog
                {
                    Description = DialogTitle,
                    UseDescriptionForTitle = true,
                    SelectedPath = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty
                };
                using var owner = new WindowHandleOwner(foregroundWindow);

                var result = foregroundWindow == IntPtr.Zero
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(owner);
                completion.SetResult(result == DialogResult.OK ? dialog.SelectedPath : null);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task;
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
