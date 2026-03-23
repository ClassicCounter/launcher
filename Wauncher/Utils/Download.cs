using Downloader;
using Refit;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using Spectre.Console;
using System.Diagnostics;

namespace Wauncher.Utils
{
    public static class DownloadManager
    {
        private static string WauncherDirectory =>
            Path.GetDirectoryName(Services.GetExePath()) ?? Directory.GetCurrentDirectory();

        private static readonly DownloadConfiguration _settings = new()
        {
            ChunkCount = 8,
            ParallelDownload = true
        };
        private static readonly DownloadConfiguration _fullGameSettings = new()
        {
            ChunkCount = 1,
            ParallelDownload = false
        };
        // Shared only for DownloadUpdater / DownloadDependencies (console-launcher, always sequential)
        private static DownloadService _downloader = new DownloadService(_settings);

        public static async Task DownloadUpdater(string path)
        {
            await _downloader.DownloadFileTaskAsync(
                $"https://github.com/ClassicCounter/updater/releases/download/updater/updater.exe",
                path
            );
        }

        public static async Task<Dependencies> DownloadDependencies(StatusContext ctx, List<Dependency> dependencies)
        {
            List<Dependency> local = new List<Dependency>();
            List<Dependency> remote = new List<Dependency>();
            Dependencies? _dependencies;
            foreach (var dependency in dependencies)
            {
                if (!DependencyManager.IsInstalled(ctx, dependency))
                {
                    if (dependency.URL != null)
                    {
                        string path = WauncherDirectory + dependency.Path;
                        if (File.Exists(path))
                            File.Delete(path);
                        if (Debug.Enabled())
                            Terminal.Debug($"Downloading {dependency.Name}");
                        await _downloader.DownloadFileTaskAsync(
                            $"{dependency.URL}",
                            $"{WauncherDirectory}{dependency.Path}");
                        remote.Add(dependency);
                    }
                    else
                    {
                        local.Add(dependency);
                    }
                }
            }
            _dependencies = new Dependencies(false, local, remote);
            return _dependencies;
        }

        public static async Task DownloadPatch(
            Patch patch,
            bool validateAll = false,
            Action<Downloader.DownloadProgressChangedEventArgs>? onProgress = null,
            Action? onExtract = null,
            Action<double>? onExtractProgress = null)
        {
            string originalFileName = patch.File.EndsWith(".7z") ? patch.File[..^3] : patch.File;
            string downloadPath = Path.Combine(WauncherDirectory, patch.File);

            if (Debug.Enabled())
                Terminal.Debug($"Starting download of: {patch.File}");

            if (patch.File.EndsWith(".7z") && File.Exists(downloadPath))
            {
                try
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Found existing .7z file, trying to delete: {downloadPath}");
                    File.Delete(downloadPath);
                }
                catch (Exception ex)
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Failed to delete existing .7z file: {ex.Message}");
                }
            }

            string baseUrl = "https://patch.classiccounter.cc";

            // Use a fresh DownloadService per call so concurrent or back-to-back downloads
            // never share state on the same instance.
            using var downloader = new DownloadService(_settings);
            if (onProgress != null)
                downloader.DownloadProgressChanged += (sender, e) => onProgress(e);

            await downloader.DownloadFileTaskAsync(
                $"{baseUrl}/{patch.File}",
                Path.Combine(WauncherDirectory, patch.File)
            );

            if (patch.File.EndsWith(".7z"))
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Download complete, starting extraction of: {patch.File}");
                onExtract?.Invoke();
                string extractPath = Path.Combine(WauncherDirectory, originalFileName);
                await Extract7z(downloadPath, extractPath, onExtractProgress);
            }
        }

        public static async Task HandlePatches(Patches patches, StatusContext ctx, bool isGameFiles, int startingProgress = 0)
        {
            string fileType = isGameFiles ? "game file" : "patch";
            string fileTypePlural = isGameFiles ? "game files" : "patches";

            var allFiles = patches.Missing.Concat(patches.Outdated).ToList();
            int totalFiles = allFiles.Count;
            int completedFiles = startingProgress;
            int failedFiles = 0;

            // status update
            Action<Downloader.DownloadProgressChangedEventArgs, string> updateStatus = (progress, filename) =>
            {
                var speed = progress.BytesPerSecondSpeed / (1024.0 * 1024.0);
                var progressText = $"{((float)completedFiles / totalFiles * 100):F1}% ({completedFiles}/{totalFiles})";
                var status = filename.EndsWith(".7z") && progress.ProgressPercentage >= 100 ? "Extracting" : "Downloading new";
                ctx.Status = _statusFormatter.FormatStatus(status, fileTypePlural, progress.ProgressPercentage, speed, completedFiles, totalFiles);
            };

            foreach (var patch in allFiles)
            {
                try
                {
                    await DownloadPatch(patch, isGameFiles, progress => updateStatus(progress, patch.File));
                    completedFiles++;
                }
                catch
                {
                    failedFiles++;
                    Terminal.Warning($"Couldn't process {fileType}: {patch.File}, possibly due to missing permissions.");
                }
            }

            if (failedFiles > 0)
                Terminal.Warning($"Couldn't download {failedFiles} {(failedFiles == 1 ? fileType : fileTypePlural)}!");
        }

        public static async Task DownloadFullGame(StatusContext ctx)
        {
            try
            {
                await Steam.GetRecentLoggedInSteamID();
                if (string.IsNullOrEmpty(Steam.recentSteamID2))
                {
                    Terminal.Error("Steam does not seem to be installed. Please make sure that you have Steam installed.");
                    Terminal.Error("Closing launcher in 5 seconds...");
                    await Task.Delay(5000);
                    Environment.Exit(1);
                    return;
                }

                var gameFiles = await Api.ClassicCounter.GetFullGameDownload(Steam.recentSteamID2);

                if (gameFiles?.Files == null || gameFiles.Files.Count == 0)
                {
                    Terminal.Error("No game files returned from the API. You may not be whitelisted.");
                    Terminal.Error("Closing launcher in 5 seconds...");
                    await Task.Delay(5000);
                    Environment.Exit(1);
                    return;
                }

                int totalFiles = gameFiles.Files.Count;
                int completedFiles = 0;
                List<string> failedFiles = new List<string>();

                foreach (var file in gameFiles.Files)
                {
                    string filePath = Path.Combine(WauncherDirectory, file.File);
                    bool needsDownload = true;

                    if (File.Exists(filePath))
                    {
                        string fileHash = CalculateMD5(filePath);
                        if (fileHash.Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                        {
                            needsDownload = false;
                            completedFiles++;
                            continue;
                        }
                    }

                    if (needsDownload)
                    {
                        try
                        {
                            EventHandler<Downloader.DownloadProgressChangedEventArgs> progressHandler = (sender, e) =>
                            {
                                var speed = e.BytesPerSecondSpeed / (1024.0 * 1024.0);
                                var progressText = $"{((float)completedFiles / totalFiles * 100):F1}% ({completedFiles}/{totalFiles})";
                                ctx.Status = _statusFormatter.FormatStatus("Downloading", file.File, e.ProgressPercentage, speed, completedFiles, totalFiles);
                            };
                            _downloader.DownloadProgressChanged += progressHandler;

                            try
                            {
                                await _downloader.DownloadFileTaskAsync(file.Link, filePath);

                                string downloadedHash = CalculateMD5(filePath);
                                if (!downloadedHash.Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                                {
                                    failedFiles.Add(file.File);
                                    Terminal.Error($"Hash mismatch for {file.File}");
                                    continue;
                                }

                                completedFiles++;
                            }
                            finally
                            {
                                _downloader.DownloadProgressChanged -= progressHandler;
                            }
                        }
                        catch (Exception ex)
                        {
                            failedFiles.Add(file.File);
                            Terminal.Error($"Failed to download {file.File}: {ex.Message}");
                        }
                    }
                }

                if (failedFiles.Count == 0)
                {
                    ctx.Status = "Extracting game files... Please do not close the launcher.";
                    await ExtractSplitArchive(gameFiles.Files.Select(f => f.File).ToList());
                    Terminal.Success("Game files downloaded and extracted successfully!");
                }
                else
                {
                    Terminal.Error($"Failed to download {failedFiles.Count} files. Closing launcher in 5 seconds...");
                    await Task.Delay(5000);
                    Environment.Exit(1);
                }
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Terminal.Error("You are not whitelisted on ClassicCounter! (https://classiccounter.cc/whitelist)");
                Terminal.Error("If you are whitelisted, check if you have Steam installed & you're logged into the whitelisted account.");
                Terminal.Error("If you're still facing issues, use one of our other download links to download the game.");
                Terminal.Warning("Closing launcher in 10 seconds...");
                await Task.Delay(10000);
                Environment.Exit(1);
            }
            catch (ApiException ex)
            {
                Terminal.Error($"Failed to get game files from API: {ex.Message}");
                Terminal.Error("Closing launcher in 5 seconds...");
                await Task.Delay(5000);
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Terminal.Error($"An error occurred: {ex.Message}");
                Terminal.Error("Closing launcher in 5 seconds...");
                await Task.Delay(5000);
                Environment.Exit(1);
            }
        }
        /// <summary>
        /// Downloads and installs the full game from ClassicCounter's CDN.
        /// Designed for use from a GUI — takes progress/status callbacks instead of a StatusContext.
        /// Throws on error so the caller can handle it.
        /// </summary>
        public static async Task InstallFullGame(
            Action<string, string, double>? onProgress,  // (filename, speed, totalPercent)
            Action<string>? onStatus,
            Action<double>? onExtractProgress = null)
        {
            await Steam.GetRecentLoggedInSteamID();
            if (string.IsNullOrEmpty(Steam.recentSteamID2))
                throw new Exception("Steam does not appear to be installed or you are not logged in.");

            onStatus?.Invoke("Fetching game files...");
            FullGameDownloadResponse gameFiles;
            try
            {
                gameFiles = await Api.ClassicCounter.GetFullGameDownload(Steam.recentSteamID2);
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new Exception("Not whitelisted. Visit classiccounter.cc/whitelist");
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Exception("Wrong Steam account or not logged in");
            }
            catch (ApiException ex) when ((int)ex.StatusCode >= 500)
            {
                throw new Exception("Download server is down. Try again soon");
            }
            catch (ApiException)
            {
                throw new Exception("Couldn't fetch game files. Try again soon");
            }
            catch (HttpRequestException)
            {
                throw new Exception("No internet or server unreachable");
            }

            if (gameFiles?.Files == null || gameFiles.Files.Count == 0)
                throw new Exception("No game files returned. You may not be whitelisted.\nVisit classiccounter.cc/whitelist to request access.");

            int total = gameFiles.Files.Count;
            int completed = 0;

            foreach (var file in gameFiles.Files)
            {
                string filePath = Path.Combine(WauncherDirectory, file.File);

                if (File.Exists(filePath) &&
                    CalculateMD5(filePath).Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    completed++;
                    onProgress?.Invoke(file.File, "", (double)completed / total * 100.0);
                    continue;
                }

                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Terminal.Warning($"Failed to delete existing file {filePath}: {ex.Message}");
                }

                using var downloader = new DownloadService(_fullGameSettings);
                downloader.DownloadProgressChanged += (s, e) =>
                    onProgress?.Invoke(
                        file.File,
                        $"{e.BytesPerSecondSpeed / 1024.0 / 1024.0:F1} MB/s",
                        (completed + e.ProgressPercentage / 100.0) / total * 100.0);

                await downloader.DownloadFileTaskAsync(file.Link, filePath);

                string downloadedHash = CalculateMD5(filePath);
                if (!downloadedHash.Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        Terminal.Warning($"Failed to delete corrupted file {filePath}: {ex.Message}");
                    }

                    throw new Exception($"Downloaded file failed verification: {file.File}");
                }

                completed++;
            }

            onStatus?.Invoke("Verifying downloaded archives...");
            await VerifySplitArchive(gameFiles.Files.Select(f => f.File).ToList());

            onStatus?.Invoke("Extracting game files... This may take a few minutes.");
            await ExtractSplitArchive(gameFiles.Files.Select(f => f.File).ToList(), onExtractProgress);
        }

        private static string CalculateMD5(string filename)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var stream = File.OpenRead(filename))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static readonly DownloadStatus _statusFormatter = new DownloadStatus();
        public static async Task ExtractSplitArchive(List<string> files, Action<double>? onProgress = null)
        {
            if (files == null || files.Count == 0)
            {
                throw new ArgumentException("No files provided for extraction");
            }

            files.Sort();

            if (Debug.Enabled())
            {
                Terminal.Debug("Starting extraction of split archive:");
                foreach (var file in files)
                {
                    Terminal.Debug($"Found part: {file}");
                }
            }

            string firstFile = Path.Combine(WauncherDirectory, files[0]);
            string extractPath = WauncherDirectory;
            string tempExtractPath = Path.Combine(extractPath, "ClassicCounter_temp");

            try
            {
                Directory.CreateDirectory(tempExtractPath);

                await Download7za();

                string? launcherDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (launcherDir == null)
                    throw new InvalidOperationException("Could not determine launcher directory");

                string exePath = Path.Combine(launcherDir, "7za.exe");

                if (Debug.Enabled())
                    Terminal.Debug("Starting 7za extraction to temp directory...");

                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"x \"{firstFile}\" -o\"{tempExtractPath}\" -y",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    process.Start();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                        throw new Exception($"7za extraction failed with exit code: {process.ExitCode}");
                }

                onProgress?.Invoke(100.0);

                string classicCounterPath = Path.Combine(tempExtractPath, "ClassicCounter");
                if (Directory.Exists(classicCounterPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug("Moving contents from ClassicCounter folder to root directory...");
                    await Task.Run(() => MoveExtractedClassicCounterFiles(classicCounterPath, extractPath));
                }
                else
                {
                    throw new DirectoryNotFoundException("ClassicCounter folder not found in extracted contents");
                }

                try
                {
                    Directory.Delete(tempExtractPath, true);
                    if (Debug.Enabled())
                        Terminal.Debug("Deleted temporary extraction directory");

                    foreach (string file in files)
                    {
                        string filePath = Path.Combine(WauncherDirectory, file);
                        if (File.Exists(filePath))
                            File.Delete(filePath);
                        if (Debug.Enabled())
                            Terminal.Debug($"Deleted archive part: {file}");
                    }

                    Delete7zaExecutable();
                }
                catch (Exception ex)
                {
                    Terminal.Warning($"Failed to cleanup some temporary files: {ex.Message}");
                }

                if (Debug.Enabled())
                    Terminal.Debug("Extraction and file movement completed successfully!");
            }
            catch (Exception ex)
            {
                Terminal.Error($"Extraction failed: {ex.Message}");
                if (Debug.Enabled())
                    Terminal.Debug($"Stack trace: {ex.StackTrace}");

                try
                {
                    if (Directory.Exists(tempExtractPath))
                        Directory.Delete(tempExtractPath, true);
                }
                catch (Exception cleanupEx)
                {
                    Terminal.Warning($"Failed to cleanup temporary directory {tempExtractPath}: {cleanupEx.Message}");
                }

                CleanupSplitArchiveFiles(files);
                Delete7zaExecutable();

                throw;
            }
        }

        private static async Task VerifySplitArchive(List<string> files)
        {
            if (files == null || files.Count == 0)
                throw new ArgumentException("No files provided for archive verification");

            string firstFile = Path.Combine(WauncherDirectory, files[0]);
            await Download7za();

            string? launcherDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (launcherDir == null)
                throw new InvalidOperationException("Could not determine launcher directory");

            string exePath = Path.Combine(launcherDir, "7za.exe");

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"t \"{firstFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            string stdOut = await process.StandardOutput.ReadToEndAsync();
            string stdErr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                CleanupSplitArchiveFiles(files);
                Delete7zaExecutable();

                string details = string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr;
                if (details.Contains("Data Error", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Downloaded archives were corrupted. Please try install again.");

                throw new Exception($"Archive verification failed (7za exit code: {process.ExitCode})");
            }
        }

        private static async Task Extract7z(string archivePath, string outputPath, Action<double>? onProgress = null)
        {
            try
            {
                if (!File.Exists(archivePath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Archive file not found: {archivePath}");
                    return;
                }

                await ExtractArchiveToDirectory(archivePath, Path.GetDirectoryName(outputPath)!, onProgress);

                try
                {
                    File.Delete(archivePath);
                    if (Debug.Enabled())
                        Terminal.Debug($"Deleted archive file: {archivePath}");
                }
                catch (Exception ex)
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Failed to delete archive file: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Terminal.Error($"Extraction failed: {ex.Message}\nStack trace: {ex.StackTrace}");
                throw;
            }
        }

        private static void MoveExtractedClassicCounterFiles(string classicCounterPath, string extractPath)
        {
            foreach (string dirPath in Directory.GetDirectories(classicCounterPath, "*", SearchOption.AllDirectories))
            {
                string newDirPath = dirPath.Replace(classicCounterPath, extractPath);
                Directory.CreateDirectory(newDirPath);
            }

            foreach (string filePath in Directory.GetFiles(classicCounterPath, "*.*", SearchOption.AllDirectories))
            {
                string newFilePath = filePath.Replace(classicCounterPath, extractPath);

                string fileName = Path.GetFileName(filePath);
                if (fileName.Equals("launcher.exe", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("wauncher.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Skipping {fileName}");
                    continue;
                }

                try
                {
                    if (File.Exists(newFilePath))
                    {
                        File.Delete(newFilePath);
                    }
                    File.Move(filePath, newFilePath);
                }
                catch (Exception ex)
                {
                    Terminal.Warning($"Failed to move file {filePath}: {ex.Message}");
                }
            }
        }

        private static async Task Download7za()
        {
            string? launcherDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (launcherDir == null)
                throw new InvalidOperationException("Could not determine launcher directory");

            string exePath = Path.Combine(launcherDir, "7za.exe");
            if (File.Exists(exePath))
                return;

            string[] fallbackUrls =
            {
                "https://fastdl.classiccounter.cc/7za.exe",
                "https://ollumcc.github.io/7za.exe"
            };

            Exception? lastError = null;
            foreach (var url in fallbackUrls)
            {
                try
                {
                    await _downloader.DownloadFileTaskAsync(url, exePath);
                    if (File.Exists(exePath))
                        return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw new Exception($"Couldn't download 7za.exe{(lastError != null ? $": {lastError.Message}" : string.Empty)}");
        }

        private static void Delete7zaExecutable()
        {
            try
            {
                string? launcherDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (string.IsNullOrWhiteSpace(launcherDir))
                    return;

                string exePath = Path.Combine(launcherDir, "7za.exe");
                if (!File.Exists(exePath))
                    return;

                File.Delete(exePath);

                if (Debug.Enabled())
                    Terminal.Debug("Deleted 7za.exe");
            }
            catch (Exception ex)
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Failed to delete 7za.exe: {ex.Message}");
            }
        }

        private static async Task ExtractArchiveToDirectory(string archivePath, string outputDirectory, Action<double>? onProgress = null)
        {
            await Task.Run(() =>
            {
                using var archive = ArchiveFactory.OpenArchive(new FileInfo(archivePath), new ReaderOptions());
                var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToArray();
                int totalEntries = entries.Length > 0 ? entries.Length : 1;
                int completedEntries = 0;

                onProgress?.Invoke(0);

                foreach (var entry in entries)
                {
                    entry.WriteToDirectory(outputDirectory, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });

                    completedEntries++;
                    onProgress?.Invoke((double)completedEntries / totalEntries * 100.0);
                }
            });
        }


        private static async Task ExtractSplitArchiveToDirectory(IEnumerable<string> archiveParts, string outputDirectory, Action<double>? onProgress = null)
        {
            await Task.Run(() =>
            {
                var parts = archiveParts
                    .Select(part => new FileInfo(Path.Combine(WauncherDirectory, part)))
                    .ToArray();

                using var archive = SevenZipArchive.OpenArchive(parts, new ReaderOptions());
                var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToArray();
                int totalEntries = entries.Length > 0 ? entries.Length : 1;
                int completedEntries = 0;

                onProgress?.Invoke(0);

                foreach (var entry in entries)
                {
                    entry.WriteToDirectory(outputDirectory, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });

                    completedEntries++;
                    onProgress?.Invoke((double)completedEntries / totalEntries * 100.0);
                }
            });
        }

        public static void Cleanup7zFiles()
        {
            try
            {
                string directory = WauncherDirectory;
                var files = Directory.GetFiles(directory, "*.7z", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(directory, "*.7z.*", SearchOption.AllDirectories))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (string file in files)
                {
                    try
                    {
                        File.Delete(file);
                        if (Debug.Enabled())
                            Terminal.Debug($"Deleted .7z file: {file}");
                    }
                    catch (Exception ex)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Failed to delete .7z file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Failed to perform cleanup: {ex.Message}");
            }
        }

        private static void CleanupSplitArchiveFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                try
                {
                    string filePath = Path.Combine(WauncherDirectory, file);
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Failed to delete archive part {file}: {ex.Message}");
                }
            }
        }
    }
}



