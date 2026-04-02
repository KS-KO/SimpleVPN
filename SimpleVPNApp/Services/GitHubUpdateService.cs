using System;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace SimpleVPNApp.Services;

public sealed class GitHubUpdateService
{
    private const string RepositoryUrl = "https://github.com/KS-KO/SimpleVPN";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);

    public Task CheckForUpdatesOnStartupAsync()
    {
        return Task.Run(async () =>
        {
            await Task.Delay(StartupDelay).ConfigureAwait(false);
            await CheckForUpdatesAsync(isManualCheck: false).ConfigureAwait(false);
        });
    }

    public async Task CheckForUpdatesAsync(bool isManualCheck)
    {
        try
        {
            var manager = new UpdateManager(
                new GithubSource(RepositoryUrl, accessToken: string.Empty, prerelease: false));

            if (!manager.IsInstalled)
            {
                if (isManualCheck)
                {
                    await ShowMessageAsync(
                        "GitHub updates are only available from an installed release build.",
                        "Update");
                }

                return;
            }

            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update == null)
            {
                if (isManualCheck)
                {
                    await ShowMessageAsync("You already have the latest version.", "Update");
                }

                return;
            }

            var version = update.TargetFullRelease.Version?.ToString() ?? "new version";
            var shouldInstall = await ShowConfirmationAsync(
                $"Version {version} is available on GitHub Releases.{Environment.NewLine}{Environment.NewLine}Download and restart now?",
                "Update");

            if (!shouldInstall)
            {
                return;
            }

            await ShowMessageAsync("Downloading update. The app will close when it is ready to restart.", "Update");
            await manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            await manager.WaitExitThenApplyUpdatesAsync(update.TargetFullRelease, silent: true, restart: true).ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Application.Current.Shutdown();
            });
        }
        catch (Exception ex)
        {
            if (isManualCheck)
            {
                await ShowMessageAsync($"Failed to check for updates.{Environment.NewLine}{ex.Message}", "Update");
            }
        }
    }

    private static Task<bool> ShowConfirmationAsync(string message, string title)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
            MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes).Task;
    }

    private static Task ShowMessageAsync(string message, string title)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information)).Task;
    }
}
