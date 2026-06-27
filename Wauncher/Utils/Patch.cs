using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

namespace Wauncher.Utils
{
    public class Patch
    {
        [JsonProperty(PropertyName = "file")]
        public required string File { get; set; }

        [JsonProperty(PropertyName = "hash")]
        public required string Hash { get; set; }
    };

    public class Patches(bool success, List<Patch> missing, List<Patch> outdated)
    {
        public bool Success = success;
        public List<Patch> Missing = missing;
        public List<Patch> Outdated = outdated;
    }

    /// <summary>
    /// Thrown when the update/patch server can't be reached (no internet, timeout, DNS, etc.).
    /// Lets callers show a clear "Can't connect to update server" message instead of hanging
    /// or silently treating the game as up to date.
    /// </summary>
    public class UpdateServerUnreachableException : Exception
    {
        public UpdateServerUnreachableException(string message, Exception? inner = null)
            : base(message, inner) { }
    }

    public static class PatchManager
    {
        private static string GetOriginalFileName(string fileName)
        {
            return fileName.EndsWith(".7z") ? fileName[..^3] : fileName;
        }

        private static async Task<List<Patch>> GetPatches(bool validateAll = false)
        {
            List<Patch> patches = new List<Patch>();

            string responseString;
            try
            {
                responseString = await Api.ClassicCounter.GetPatches();
            }
            catch (Exception ex)
            {
                // Network failure / timeout / DNS — surface it instead of swallowing,
                // so the UI can say "Can't connect to update server" rather than hang.
                if (Debug.Enabled())
                    Terminal.Debug($"Couldn't reach {(validateAll ? "full game" : "patch")} API: {ex.Message}");
                throw new UpdateServerUnreachableException("Can't connect to update server", ex);
            }

            try
            {
                JObject responseJson = JObject.Parse(responseString);

                if (responseJson["files"] != null)
                    patches = responseJson["files"]!.ToObject<Patch[]>()!.ToList();
            }
            catch (Exception ex)
            {
                // Reachable but returned something we can't parse — treat as a server problem.
                if (Debug.Enabled())
                    Terminal.Debug($"Update server returned invalid data: {ex.Message}");
                throw new UpdateServerUnreachableException("Update server returned invalid data", ex);
            }

            return patches;
        }

        private static async Task<string> GetHash(string filePath)
        {
            using var md5 = MD5.Create();
            await using var stream = File.OpenRead(filePath);
            byte[] hash = await Task.Run(() => md5.ComputeHash(stream));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static async Task<Patches> ValidatePatches(bool validateAll = false, bool deleteOutdatedFiles = true, Action<int, int>? onProgress = null)
        {
            List<Patch> patches = await GetPatches(validateAll);
            List<Patch> missing = new();
            List<Patch> outdated = new();
            Patch? dirPatch = null;

            // first only check pak_dat.vpk
            var pakDatPatch = patches.FirstOrDefault(p => p.File == "csgo/pak_dat.vpk");
            bool skipValidation = false;

            if (pakDatPatch != null && !validateAll)
            {
                string pakDatPath = $"{Directory.GetCurrentDirectory()}/csgo/pak_dat.vpk";

                if (Debug.Enabled())
                    Terminal.Debug("Checking csgo/pak_dat.vpk first...");

                if (File.Exists(pakDatPath))
                {
                    string pakDatHash = await GetHash(pakDatPath);
                    if (pakDatHash.Equals(pakDatPatch.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        // pak_dat.vpk matches — also check pak01_dir.vpk before declaring up to date
                        var dirPatchFast = patches.FirstOrDefault(p => p.File.Contains("pak01_dir.vpk"));
                        if (dirPatchFast != null)
                        {
                            string dirOriginal = GetOriginalFileName(dirPatchFast.File);
                            string dirPathFast = Path.Combine(Directory.GetCurrentDirectory(), dirOriginal);
                            if (File.Exists(dirPathFast))
                            {
                                string dirHash = await GetHash(dirPathFast);
                                if (dirHash.Equals(dirPatchFast.Hash, StringComparison.OrdinalIgnoreCase))
                                {
                                    skipValidation = true;
                                    return new Patches(true, missing, outdated);
                                }
                            }
                        }
                        else
                        {
                            skipValidation = true;
                            return new Patches(true, missing, outdated);
                        }
                    }
                    else
                    {
                        if (deleteOutdatedFiles)
                            File.Delete(pakDatPath);
                    }
                }
                else
                {
                    if (Debug.Enabled())
                        Terminal.Debug("Missing: csgo/pak_dat.vpk - will check all files");
                }
            }

            if (!skipValidation)
            {
                // find pak01_dir.vpk from patch api
                dirPatch = patches.FirstOrDefault(p => p.File.Contains("pak01_dir.vpk"));
                bool needPak01Update = false;

                if (dirPatch != null)
                {
                    string dirPath = $"{Directory.GetCurrentDirectory()}/csgo/pak01_dir.vpk";

                    if (Debug.Enabled())
                        Terminal.Debug("Checking csgo/pak01_dir.vpk first...");

                    if (File.Exists(dirPath))
                    {
                        if (Debug.Enabled())
                            Terminal.Debug("Checking hash for: csgo/pak01_dir.vpk");

                        string dirHash = await GetHash(dirPath);
                        if (!dirHash.Equals(dirPatch.Hash, StringComparison.OrdinalIgnoreCase))
                        {
                            if (Debug.Enabled())
                                Terminal.Debug("csgo/pak01_dir.vpk is outdated!");

                            if (deleteOutdatedFiles)
                                File.Delete(dirPath);
                            outdated.Add(dirPatch);
                            needPak01Update = true;
                        }
                        else if (!validateAll)
                        {
                            if (Debug.Enabled())
                                Terminal.Debug("csgo/pak01_dir.vpk is up to date - will skip pak01 files");
                        }
                        else
                        {
                            if (Debug.Enabled())
                                Terminal.Debug("csgo/pak01_dir.vpk is up to date - checking all files anyway due to full validation mode");
                        }
                    }
                    else
                    {
                        if (Debug.Enabled())
                            Terminal.Debug("Missing: csgo/pak01_dir.vpk!");

                        missing.Add(dirPatch);
                        needPak01Update = true;
                    }

                    if (!needPak01Update)
                    {
                        patches.Remove(dirPatch);
                    }
                }

                var concurrentMissing = new ConcurrentBag<Patch>();
                var concurrentOutdated = new ConcurrentBag<Patch>();

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4
                };

                int totalToCheck = patches.Count;
                int checkedCount = 0;
                onProgress?.Invoke(0, totalToCheck);

                await Parallel.ForEachAsync(patches, parallelOptions, async (patch, cancellationToken) =>
                {
                    onProgress?.Invoke(Interlocked.Increment(ref checkedCount), totalToCheck);
                    string originalFileName = GetOriginalFileName(patch.File);

                    // skip dir file (we already checked it)
                    if (originalFileName.Contains("pak01_dir.vpk"))
                        return;

                    // are you a pak01 file?
                    bool isPak01File = originalFileName.Contains("pak01_");
                    string path = Path.Combine(Directory.GetCurrentDirectory(), originalFileName);

                    if (isPak01File && !needPak01Update && !validateAll)
                    {
                        if (!File.Exists(path))
                        {
                            if (Debug.Enabled())
                                Terminal.Debug($"Missing: {originalFileName}");

                            concurrentMissing.Add(patch);
                            return;
                        }

                        if (Debug.Enabled())
                            Terminal.Debug($"Skipping hash check for: {originalFileName} (pak01_dir.vpk up to date)");

                        return;
                    }

                    if (!File.Exists(path))
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Missing: {originalFileName}");

                        concurrentMissing.Add(patch);
                        return;
                    }

                    if (Debug.Enabled())
                        Terminal.Debug($"Checking hash for: {originalFileName}{(isPak01File && validateAll ? " (full validation)" : "")}");

                    string hash = await GetHash(path);
                    if (!hash.Equals(patch.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Outdated: {originalFileName}");

                        if (deleteOutdatedFiles)
                            File.Delete(path);
                        concurrentOutdated.Add(patch);
                    }
                });

                missing.AddRange(concurrentMissing);
                outdated.AddRange(concurrentOutdated);

                // if pak01_dir.vpk needs update, move it to end of lists
                if (needPak01Update && dirPatch != null)
                {
                    if (outdated.Remove(dirPatch))
                        outdated.Add(dirPatch);
                    if (missing.Remove(dirPatch))
                        missing.Add(dirPatch);
                }
            }

            return new Patches(patches.Count > 0, missing, outdated);
        }
    }
}

