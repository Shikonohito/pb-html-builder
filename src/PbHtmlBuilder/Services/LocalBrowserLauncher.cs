using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Pb.Builder.Infrastructure.Windows.Configuration;

namespace PbHtmlBuilder.Services;

public sealed class LocalBrowserLauncher(
    LocalAppOptions localAppOptions,
    IHostApplicationLifetime lifetime,
    IServer server,
    ILogger<LocalBrowserLauncher> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!localAppOptions.LaunchBrowser)
        {
            return Task.CompletedTask;
        }

        lifetime.ApplicationStarted.Register(OpenBrowser);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OpenBrowser()
    {
        var url = localAppOptions.PreferredUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
            var address = addresses?.FirstOrDefault(static item => item.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                ?? addresses?.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(address))
            {
                return;
            }

            url = address
                .Replace("0.0.0.0", "localhost", StringComparison.OrdinalIgnoreCase)
                .Replace("[::]", "localhost", StringComparison.OrdinalIgnoreCase)
                .Replace("+", "localhost", StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to open the browser at {Url}", url);
        }
    }
}
