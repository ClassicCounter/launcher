using Downloader;
using Refit;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using Spectre.Console;

namespace Wauncher.Utils
{
    public static class DownloadManager
    {
        private static readonly DownloadConfiguration _settings = new()
        {
            ChunkCount = 8,
            ParallelDownload = true
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
                        string path = Directory.GetCurrentDirectory() + dependency.Path;
                        if (File.Exists(path))
                            File.Delete(path);
                        if (Debug.Enabled())
                            Terminal.Debug($"Downloading {dependency.Name}");
                        await _downloader.DownloadFileTaskAsync(
                            $"{dependency.URL}",
                            $"{Directory.GetCurrentDirectory()}{dependency.Path}");
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
            string downloadPath = $"{Directory.GetCurrentDirectory()}/{patch.File}";

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
                $"{Directory.GetCurrentDirectory()}/{patch.File}"
            );

            if (patch.File.EndsWith(".7z"))
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Download complete, starting extraction of: {patch.File}");
                onExtract?.Invoke();
                string extractPath = $"{Directory.GetCurrentDirectory()}/{originalFileName}";
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
                ctx.Status = $"{status} {fileTypePlural}{GetDots().PadRight(3)} [gray]|[/] {progressText} [gray]|[/] {GetProgressBar(progress.ProgressPercentage)} {progress.ProgressPercentage:F1}% [gray]|[/] {speed:F1} MB/s";
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
                    string filePath = Path.Combine(Directory.GetCurrentDirectory(), file.File);
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
                                ctx.Status = $"Downloading {file.File}{GetDots().PadRight(3)} [gray]|[/] {progressText} [gray]|[/] {GetProgressBar(e.ProgressPercentage)} {e.ProgressPercentage:F1}% [gray]|[/] {speed:F1} MB/s";
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
            var gameFiles = await Api.ClassicCounter.GetFullGameDownload(Steam.recentSteamID2);

            if (gameFiles?.Files == null || gameFiles.Files.Count == 0)
                throw new Exception("No game files returned. You may not be whitelisted.\nVisit classiccounter.cc/whitelist to request access.");

            int total = gameFiles.Files.Count;
            int completed = 0;

            foreach (var file in gameFiles.Files)
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), file.File);

                if (File.Exists(filePath) &&
                    CalculateMD5(filePath).Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    completed++;
                    onProgress?.Invoke(file.File, "", (double)completed / total * 100.0);
                    continue;
                }

                using var downloader = new DownloadService(_settings);
                downloader.DownloadProgressChanged += (s, e) =>
                    onProgress?.Invoke(
                        file.File,
                        $"{e.BytesPerSecondSpeed / 1024.0 / 1024.0:F1} MB/s",
                        (completed + e.ProgressPercentage / 100.0) / total * 100.0);

                await downloader.DownloadFileTaskAsync(file.Link, filePath);
                completed++;
            }

            onStatus?.Invoke("Extracting game files...");
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

        // meant only for downloading whole game for now
        // todo maybe make it more modular/allow other functions to use this
        // FOR DOWNLOAD STATUS
        public static int dotCount = 0;
        public static DateTime lastDotUpdate = DateTime.Now;
        public static string GetDots()
        {
            if ((DateTime.Now - lastDotUpdate).TotalMilliseconds > 500)
            {
                dotCount = (dotCount + 1) % 4;
                lastDotUpdate = DateTime.Now;
            }
            return "...".Substring(0, dotCount);
        }
        public static string GetProgressBar(double percentage)
        {
            int blocks = 16;
            int level = (int)(percentage / (100.0 / (blocks * 3)));
            string bar = "";

            for (int i = 0; i < blocks; i++)
            {
                int blockLevel = Math.Min(3, Math.Max(0, level - (i * 3)));
                bar += blockLevel switch
                {
                    0 => "¦",
                    1 => "¦",
                    2 => "¦",
                    3 => "¦",
                    _ => "¦"
                };
            }
            return bar;
        }
        // DOWNLOAD STATUS OVER
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

            string firstFile = files[0];
            string extractPath = Directory.GetCurrentDirectory();
            string tempExtractPath = Path.Combine(extractPath, "ClassicCounter_temp");

            try
            {
                Directory.CreateDirectory(tempExtractPath);

                if (Debug.Enabled())
                    Terminal.Debug("Starting in-process extraction to temp directory...");

                await ExtractSplitArchiveToDirectory(files, tempExtractPath, onProgress);

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
                        File.Delete(file);
                        if (Debug.Enabled())
                            Terminal.Debug($"Deleted archive part: {file}");
                    }
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
                catch { }

                throw;
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

                if (Path.GetFileName(filePath).Equals("launcher.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (Debug.Enabled())
                        Terminal.Debug("Skipping launcher.exe");
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
                    .Select(part => new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), part)))
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
                string directory = Directory.GetCurrentDirectory();
                var files = Directory.GetFiles(directory, "*.7z", SearchOption.AllDirectories);

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
    }
}



