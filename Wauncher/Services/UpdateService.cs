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

        private Patches? _cachedPatches;
        private bool _forceValidateAllOnce;
        private long _lastInstallProgressTick;

        [ObservableProperty]
        private bool _isExtracting;

        public async Task<bool> CheckForUpdatesAsync()
        {
            if (IsCheckingUpdates || IsUpdating || IsInstalling)
                return false;

            IsCheckingUpdates = true;
            // Clear any stale error from a previous (e.g. offline) check so a successful
            // re-check doesn't keep showing "Can't connect to update server".
            if (UpdateStatusFile.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                UpdateStatusFile = "";

            try
            {
                string csgoExe = Path.Combine(WauncherDirectory, "csgo.exe");
                
                if (!File.Exists(csgoExe))
                {
                    IsNeedingInstall = true;
                    return true;
                }

                var patches = await GetPatchesAsync();
                if (patches == null)
                    return false;

                // GetPatchesAsync already hashed/validated every file once; reuse that
                // result instead of re-validating the whole game a second time.
                var needsUpdate = patches.Missing.Count > 0 || patches.Outdated.Count > 0;
                IsUpdateAvailable = needsUpdate;

                return needsUpdate;
            }
            catch (UpdateServerUnreachableException ex)
            {
                ErrorLogger.LogError("UpdateService.CheckForUpdatesAsync", ex, "Update server unreachable");
                UpdateStatusFile = "Error: Can't connect to update server";
                return false;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateService.CheckForUpdatesAsync", ex, "Failed to check for updates");
                UpdateStatusFile = "Error: Couldn't check for updates";
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

            _lastInstallProgressTick = 0;
            try
            {

                await DownloadManager.InstallFullGame(
                    onProgress: (file, speed, percent) =>
                    {
                        // Throttle to ~10 UI updates/sec — the download library fires this
                        // hundreds of times per second which saturates the UI thread.
                        var now = Environment.TickCount64;
                        if (now - _lastInstallProgressTick < 100) return;
                        _lastInstallProgressTick = now;

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
                            bool nowExtracting = status.Contains("Extracting", StringComparison.OrdinalIgnoreCase);
                            IsExtracting = nowExtracting;
                            UpdateStatusFile = nowExtracting ? "Extracting..." : status;
                            UpdateStatusSpeed = nowExtracting ? "This may take a few minutes." : "";
                            if (nowExtracting)
                                UpdateProgress = 0; // Reset so extraction % shows from 0 in button text
                            else
                                UpdateIndeterminate = true;
                        });
                    },
                    onExtractProgress: extractPercent =>
                    {
                        var now = Environment.TickCount64;
                        if (extractPercent < 100 && now - _lastInstallProgressTick < 100) return;
                        _lastInstallProgressTick = now;

                        Dispatcher.UIThread.Post(() =>
                        {
                            if (extractPercent >= 100)
                            {
                                IsExtracting = false;
                                UpdateProgress = 0; // Reset so it doesn't flash "Downloading 100%"
                                UpdateIndeterminate = false;
                                UpdateStatusFile = "Finalizing...";
                                UpdateStatusSpeed = "";
                            }
                            else
                            {
                                UpdateProgress = extractPercent;
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
                IsExtracting = false;
                IsInstalling = false;
            }
        }

        public async Task<bool> ValidateGameFilesAsync(bool fullValidate = false)
        {
            if (IsUpdating || IsInstalling)
                return false;

            var patches = await GetPatchesAsync();
            if (patches == null)
                return false;

            IsUpdating = true;
            UpdateProgress = 0;
            UpdateIndeterminate = false;
            UpdateStatusFile = fullValidate ? "Verifying game files..." : "Checking game files...";
            UpdateStatusSpeed = "";

            try
            {
                Patches currentPatches;
                if (fullValidate)
                {
                    // "Verify Game Files": always hash every file from scratch (ignore the
                    // fast-path and any cached result), reporting live progress.
                    currentPatches = await Task.Run(() => PatchManager.ValidatePatches(
                        validateAll: true,
                        onProgress: (done, total) => Dispatcher.UIThread.Post(() =>
                        {
                            UpdateIndeterminate = false;
                            UpdateStatusFile = total > 0 ? $"Verifying game files...  {done}/{total}" : "Verifying game files...";
                            UpdateProgress = total > 0 ? (double)done / total * 100.0 : 0;
                            UpdateStatusSpeed = "";
                        })));
                }
                else
                {
                    currentPatches = _cachedPatches ?? await Task.Run(() => PatchManager.ValidatePatches(validateAll: _forceValidateAllOnce));
                }
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
                // Safety net: remove any leftover .7z archives that weren't deleted
                // during extraction (e.g. a momentarily locked file).
                DownloadManager.Cleanup7zFiles();
            }
        }

        private async Task<Patches?> GetPatchesAsync()
        {
            if (_cachedPatches != null)
                return _cachedPatches;

            try
            {
                var patches = await PatchManager.ValidatePatches(deleteOutdatedFiles: false);
                _cachedPatches = patches;
                return patches;
            }
            catch (UpdateServerUnreachableException ex)
            {
                // Surface a clear, friendly message in the bottom bar instead of hanging/failing silently.
                ErrorLogger.LogError("UpdateService.GetPatchesAsync", ex, "Update server unreachable");
                UpdateStatusFile = "Error: Can't connect to update server";
                return null;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateService.GetPatchesAsync", ex, "Failed to get patches");
                UpdateStatusFile = "Error: Couldn't check for updates";
                return null;
            }
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
