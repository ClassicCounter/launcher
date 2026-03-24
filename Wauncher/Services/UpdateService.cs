using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Wauncher.Utils;

namespace Wauncher.Services
{
    public partial class UpdateService : ObservableObject, IUpdateService
    {
        private static string WauncherDirectory =>
            Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? Directory.GetCurrentDirectory();

        [ObservableProperty]
        private bool _isUpdateAvailable;

        [ObservableProperty]
        private bool _isNeedingInstall;

        [ObservableProperty]
        private bool _isCheckingUpdates;

        [ObservableProperty]
        private bool _isUpdating;

        [ObservableProperty]
        private bool _isInstalling;

        [ObservableProperty]
        private double _updateProgress;

        [ObservableProperty]
        private string _updateStatus = "";

        [ObservableProperty]
        private string _updateStatusFile = "";

        [ObservableProperty]
        private string _updateStatusSpeed = "";

        [ObservableProperty]
        private bool _updateIndeterminate;

        private CancellationTokenSource? _updateCts;
        private Patches? _cachedPatches;
        private bool _forceValidateAllOnce;

        public async Task<bool> CheckForUpdatesAsync()
        {
            if (IsCheckingUpdates || IsUpdating || IsInstalling)
            {
                string errorMsg = "Update check already in progress";
                Terminal.Warning(errorMsg);
                ErrorLogger.LogError("UpdateService.CheckForUpdatesAsync", errorMsg, "Multiple update check attempts");
                return false;
            }

            IsCheckingUpdates = true;
            
            try
            {
                string csgoExe = Path.Combine(WauncherDirectory, "csgo.exe");
                
                if (!File.Exists(csgoExe))
                {
                    string errorMsg = "Game executable not found - installation needed";
                    Terminal.Print(errorMsg);
                    ErrorLogger.LogError("UpdateService.CheckForUpdatesAsync", errorMsg, $"Expected path: {csgoExe}");
                    IsNeedingInstall = true;
                    return true;
                }

                Terminal.Print("Checking for updates...");
                var patches = await GetPatchesAsync();
                if (patches == null)
                {
                    string errorMsg = "Failed to retrieve patch information";
                    Terminal.Warning(errorMsg);
                    ErrorLogger.LogError("UpdateService.CheckForUpdatesAsync", errorMsg, "GetPatchesAsync returned null");
                    return false;
                }

                var needsUpdate = await ValidateFilesAsync(patches);
                IsUpdateAvailable = needsUpdate;
                
                if (needsUpdate)
                {
                    Terminal.Print("Updates are available");
                }
                else
                {
                    Terminal.Print("Game is up to date");
                }
                
                return needsUpdate;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Update check failed: {ex.Message}";
                Terminal.Error(errorMsg);
                ErrorLogger.LogError("UpdateService.CheckForUpdatesAsync", ex, "Failed to check for updates");
                return false;
            }
            finally
            {
                IsCheckingUpdates = false;
            }
        }

        public async Task<bool> InstallGameFromCdnAsync()
        {
            if (IsInstalling || IsUpdating)
                return false;

            IsInstalling = true;
            IsNeedingInstall = true;
            UpdateProgress = 0;
            UpdateIndeterminate = true;
            UpdateStatusFile = "Connecting...";
            UpdateStatusSpeed = "";

            try
            {
                await DownloadManager.InstallFullGame(
                    onProgress: (file, speed, percent) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            UpdateStatusFile = $"Installing {ShortFileName(file)}  {percent:F0}%";
                            UpdateStatusSpeed = string.IsNullOrWhiteSpace(speed) ? "" : speed;
                            UpdateProgress = percent;
                            UpdateIndeterminate = false;
                        });
                    },
                    onStatus: status =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            bool isExtracting = status.Contains("Extracting", StringComparison.OrdinalIgnoreCase);
                            UpdateStatusFile = status;
                            UpdateStatusSpeed = isExtracting ? "Large installs can take a few minutes." : "";
                            UpdateIndeterminate = isExtracting;
                            if (isExtracting)
                                UpdateProgress = 0;
                        });
                    },
                    onExtractProgress: extractPercent =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (extractPercent >= 100)
                            {
                                UpdateIndeterminate = false;
                                UpdateStatusFile = "Finalizing extracted files...";
                                UpdateStatusSpeed = "";
                                UpdateProgress = 100;
                            }
                        });
                    });

                IsNeedingInstall = false;
                UpdateStatusFile = "Game installed!";
                UpdateStatusSpeed = "";
                UpdateProgress = 100;
                UpdateIndeterminate = false;
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateService.InstallGameFromCdnAsync", ex, "Failed to install game from CDN");
                DownloadManager.Cleanup7zFiles();
                UpdateStatusFile = $"Install error: {ex.Message}";
                UpdateStatusSpeed = "";
                UpdateIndeterminate = false;
                return false;
            }
            finally
            {
                IsInstalling = false;
            }
        }

        public async Task<bool> ValidateGameFilesAsync()
        {
            if (IsUpdating || IsInstalling)
                return false;

            var patches = await GetPatchesAsync();
            if (patches == null)
                return false;

            IsUpdating = true;
            UpdateProgress = 0;
            UpdateIndeterminate = false;
            UpdateStatusFile = "Checking game files...";
            UpdateStatusSpeed = "";

            try
            {
                var currentPatches = _cachedPatches ?? await Task.Run(() => PatchManager.ValidatePatches(validateAll: _forceValidateAllOnce));
                _cachedPatches = null;
                _forceValidateAllOnce = false;

                var allPatches = currentPatches.Missing.Concat(currentPatches.Outdated).ToList();
                if (allPatches.Count == 0)
                {
                    UpdateStatusFile = "Game is up to date!";
                    UpdateProgress = 100;
                    return true;
                }

                int totalFiles = allPatches.Count;
                int completedFiles = 0;

                foreach (var patch in allPatches)
                {
                    await DownloadManager.DownloadPatch(
                        patch,
                        onProgress: progress =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                UpdateIndeterminate = false;
                                UpdateStatusFile = $"Installing {ShortFileName(patch.File)}  {progress.ProgressPercentage:F0}%";
                                UpdateStatusSpeed = FormatDownloadSpeed(progress.BytesPerSecondSpeed);
                                UpdateProgress = ((completedFiles + progress.ProgressPercentage / 100.0) / totalFiles) * 100.0;
                            });
                        },
                        onExtract: () =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                UpdateIndeterminate = true;
                                UpdateStatusFile = $"Extracting {ShortFileName(patch.File)}...";
                                UpdateStatusSpeed = "";
                            });
                        },
                        onExtractProgress: extractPercent =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                UpdateIndeterminate = false;
                                UpdateStatusFile = $"Extracting {ShortFileName(patch.File)}... {extractPercent:F0}%";
                                UpdateProgress = ((completedFiles + extractPercent / 100.0) / totalFiles) * 100.0;
                            });
                        });

                    completedFiles++;
                    UpdateProgress = (double)completedFiles / totalFiles * 100.0;
                }

                UpdateStatusFile = "Update complete!";
                UpdateStatusSpeed = "";
                UpdateProgress = 100;
                IsUpdateAvailable = false;
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateService.ValidateGameFilesAsync", ex, "Failed to validate game files");
                UpdateStatusFile = $"Error: {ex.Message}";
                UpdateStatusSpeed = "";
                return false;
            }
            finally
            {
                IsUpdating = false;
            }
        }

        private async Task<Patches?> GetPatchesAsync()
        {
            if (_cachedPatches != null)
            {
                Terminal.Print("Using cached patch information");
                return _cachedPatches;
            }

            try
            {
                Terminal.Print("Fetching patch information from API...");
                var patches = await PatchManager.ValidatePatches();
                
                if (patches.Success)
                {
                    _cachedPatches = patches;
                    Terminal.Print($"Patch validation complete: {patches.Missing.Count} missing, {patches.Outdated.Count} outdated");
                }
                else
                {
                    string errorMsg = "Patch validation failed";
                    Terminal.Warning(errorMsg);
                    ErrorLogger.LogError("UpdateService.GetPatchesAsync", errorMsg, "PatchManager.ValidatePatches returned Success=false");
                }
                
                return patches;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to get patches: {ex.Message}";
                Terminal.Error(errorMsg);
                ErrorLogger.LogError("UpdateService.GetPatchesAsync", ex, "Failed to get patches");
                return null;
            }
        }

        private async Task<bool> ValidateFilesAsync(Patches patches)
        {
            var refreshed = await Task.Run(() => PatchManager.ValidatePatches(deleteOutdatedFiles: false));
            _cachedPatches = refreshed;
            return refreshed.Missing.Count > 0 || refreshed.Outdated.Count > 0;
        }

        private static string ShortFileName(string path)
        {
            return Path.GetFileName(path);
        }

        private static string FormatDownloadSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0)
                return string.Empty;

            return $"{bytesPerSecond / 1024.0 / 1024.0:F1} MB/s";
        }
    }
}
