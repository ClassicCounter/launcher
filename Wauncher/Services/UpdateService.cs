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
                return false;

            IsCheckingUpdates = true;
            
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

                var needsUpdate = await ValidateFilesAsync(patches);
                IsUpdateAvailable = needsUpdate;
                
                return needsUpdate;
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
                return _cachedPatches;

            try
            {
                var patches = await PatchManager.ValidatePatches();
                _cachedPatches = patches;
                return patches;
            }
            catch
            {
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
