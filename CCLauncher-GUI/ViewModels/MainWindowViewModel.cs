using CCLauncher_GUI.ViewModels;
using ReactiveUI;
using Launcher.Utils;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System;

namespace CCLauncher_GUI.ViewModels;
public class MainWindowViewModel : ReactiveObject
{
    private string _windowTitle;
    public string WindowTitle
    {
        get => _windowTitle;
        private set => this.RaiseAndSetIfChanged(ref _windowTitle, value);
    }

    private string _statusMessage = "Checking for updates...";
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public MainWindowViewModel()
    {
        // set title with current version
        WindowTitle = $"ClassicCounter Launcher ({LauncherVersion.Current})";
    }

    public async Task Initialize()
    {
        StatusMessage = "Checking for updates...";
        string latestVersion = await LauncherVersion.GetLatestVersion();

        if (LauncherVersion.Current != latestVersion)
        {
            StatusMessage = "Updating launcher...";
            await UpdateLauncher(latestVersion);
        }

        StatusMessage = "Ready!";
        IsLoading = false;
    }

    private async Task UpdateLauncher(string version)
    {
        string updaterPath = $"{Directory.GetCurrentDirectory()}/updater.exe";
        //await DownloadManager.DownloadUpdater(updaterPath);
        Process updaterProcess = new Process();
        updaterProcess.StartInfo.FileName = updaterPath;
        updaterProcess.StartInfo.Arguments = $"--version={version}";
        updaterProcess.Start();
        Environment.Exit(1);
    }
}