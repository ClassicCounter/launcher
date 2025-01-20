using Downloader;
using Refit;
using Spectre.Console;
using System.Diagnostics;

namespace Launcher.Utils
{
    public static class DownloadManager
    {
        private static DownloadConfiguration _settings = new()
        {
            ChunkCount = 8,
            ParallelDownload = true
        };
        private static DownloadService _downloader = new DownloadService(_settings);

        public static async Task DownloadUpdater(string path)
        {
            await _downloader.DownloadFileTaskAsync(
                $"https://github.com/ClassicCounter/updater/releases/download/updater/updater.exe",
                path
            );
        }

        public static async Task DownloadPatch(
            Patch patch,
            bool validateAll = false,
            Action<Downloader.DownloadProgressChangedEventArgs>? onProgress = null,
            Action? onExtract = null)
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

            string baseUrl = validateAll ? "https://game.classiccounter.cc" : "https://patch.classiccounter.cc";

            if (onProgress != null)
            {
                EventHandler<Downloader.DownloadProgressChangedEventArgs> progressHandler = (sender, e) => onProgress(e);
                _downloader.DownloadProgressChanged += progressHandler;
                try
                {
                    await _downloader.DownloadFileTaskAsync(
                        $"{baseUrl}/{patch.File}",
                        $"{Directory.GetCurrentDirectory()}/{patch.File}"
                    );
                }
                finally
                {
                    _downloader.DownloadProgressChanged -= progressHandler;
                }
            }
            else
            {
                await _downloader.DownloadFileTaskAsync(
                    $"{baseUrl}/{patch.File}",
                    $"{Directory.GetCurrentDirectory()}/{patch.File}"
                );
            }

            if (patch.File.EndsWith(".7z"))
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Download complete, starting extraction of: {patch.File}");
                onExtract?.Invoke(); // for "extracting" status
                string extractPath = $"{Directory.GetCurrentDirectory()}/{originalFileName}";
                await Extract7z(downloadPath, extractPath);
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
                if (string.IsNullOrEmpty(Steam.recentSteamID))
                {
                    Terminal.Error("Discord not connected. Please make sure Discord is running.");
                    Terminal.Error("Closing launcher in 5 seconds...");
                    await Task.Delay(5000);
                    Environment.Exit(1);
                    return;
                }

                // pass steam id to api
                var gameFiles = await Api.ClassicCounter.GetFullGameDownload(Steam.recentSteamID);

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
                                await _downloader.DownloadFileTaskAsync(
                                    file.Link,
                                    filePath
                                );

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
                    ctx.Status = "Extracting game files...";
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
                Terminal.Error("If you are whitelisted, check if Discord is open and if you're logged into the whitelisted account.");
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
        private static async Task ExtractSplitArchive(List<string> files)
        {
            if (files == null || files.Count == 0)
            {
                throw new ArgumentException("No files provided for extraction");
            }

            files.Sort();

            if (Debug.Enabled())
            {
                Terminal.Debug($"Starting extraction of split archive:");
                foreach (var file in files)
                {
                    Terminal.Debug($"Found part: {file}");
                }
            }

            string firstFile = files[0];
            string extractPath = Directory.GetCurrentDirectory();
            string tempExtractPath = Path.Combine(extractPath, "temp_extract");

            try
            {
                Directory.CreateDirectory(tempExtractPath);

                await Download7za();

                string? launcherDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (launcherDir == null)
                {
                    throw new InvalidOperationException("Could not determine launcher directory");
                }

                string exePath = Path.Combine(launcherDir, "7za.exe");

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

                    if (Debug.Enabled())
                        Terminal.Debug($"Starting extraction to temp directory...");

                    process.Start();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"7za extraction failed with exit code: {process.ExitCode}");
                    }
                }

                string classicCounterPath = Path.Combine(tempExtractPath, "ClassicCounter");
                if (Directory.Exists(classicCounterPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug("Moving contents from ClassicCounter folder to root directory...");

                    // first, get all files and directories from the ClassicCounter folder
                    foreach (string dirPath in Directory.GetDirectories(classicCounterPath, "*", SearchOption.AllDirectories))
                    {
                        // create directory in root, removing the "ClassicCounter" part from the path
                        string newDirPath = dirPath.Replace(classicCounterPath, extractPath);
                        Directory.CreateDirectory(newDirPath);
                    }

                    foreach (string filePath in Directory.GetFiles(classicCounterPath, "*.*", SearchOption.AllDirectories))
                    {
                        string newFilePath = filePath.Replace(classicCounterPath, extractPath);

                        // skip launcher.exe
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
                    0 => "░",
                    1 => "▒",
                    2 => "▓",
                    3 => "█",
                    _ => "█"
                };
            }
            return bar;
        }
        // DOWNLOAD STATUS OVER



        private static async Task Download7za()
        {
            string? launcherDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (launcherDir == null)
            {
                throw new InvalidOperationException("Could not determine launcher directory");
            }

            string exePath = Path.Combine(launcherDir, "7za.exe");
            bool downloaded = false;
            int retryCount = 0;
            string[] fallbackUrls = new[]
            {
                "https://fastdl.classiccounter.cc/7za.exe",
                "https://ollumcc.github.io/7za.exe"
            };

            while (!downloaded && retryCount < 10)
            {
                if (!File.Exists(exePath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"7za.exe not found, downloading... (Attempt {retryCount + 1}/10)");

                    try
                    {
                        await _downloader.DownloadFileTaskAsync(
                            fallbackUrls[retryCount % fallbackUrls.Length],
                            exePath
                        );

                        if (File.Exists(exePath))
                        {
                            downloaded = true;
                            if (Debug.Enabled())
                                Terminal.Debug($"Downloaded 7za.exe to: {exePath}");
                        }
                        else
                        {
                            Terminal.Error($"Failed to download 7za.exe! Trying again... (Attempt {retryCount + 1})");
                            retryCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Failed to download 7za.exe: {ex.Message}");
                        retryCount++;
                    }

                    if (retryCount > 0)
                        await Task.Delay(1000);
                }
                else
                {
                    downloaded = true;
                }
            }

            if (!downloaded)
            {
                Terminal.Error("Couldn't download 7za.exe! Launcher will close in 5 seconds...");
                await Task.Delay(5000);
                Environment.Exit(1);
            }
        }

        private static async Task Extract7z(string archivePath, string outputPath)
        {
            try
            {
                if (!File.Exists(archivePath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Archive file not found: {archivePath}");
                    return;
                }

                await Download7za();

                string? launcherDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (launcherDir == null)
                {
                    throw new InvalidOperationException("Could not determine launcher directory");
                }

                string exePath = Path.Combine(launcherDir, "7za.exe");

                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"x \"{archivePath}\" -o\"{Path.GetDirectoryName(outputPath)}\" -y",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    if (Debug.Enabled())
                        Terminal.Debug($"Starting extraction...");

                    process.Start();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"7za extraction failed with exit code: {process.ExitCode}");
                    }

                    if (Debug.Enabled())
                        Terminal.Debug("Extraction completed successfully!");

                    Argument.AddArgument("+snd_mixahead 0.1");
                }

                // delete 7z after extract
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

                // Delete 7za.exe if it exists
                string? launcherDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (launcherDir != null)
                {
                    string sevenZaPath = Path.Combine(launcherDir, "7za.exe");
                    if (File.Exists(sevenZaPath))
                    {
                        try
                        {
                            File.Delete(sevenZaPath);
                            if (Debug.Enabled())
                                Terminal.Debug("Deleted 7za.exe");
                        }
                        catch (Exception ex)
                        {
                            if (Debug.Enabled())
                                Terminal.Debug($"Failed to delete 7za.exe: {ex.Message}");
                        }
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
