using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Launcher.Utils
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

    public static class PatchManager
    {
        private static string GetOriginalFileName(string fileName)
        {
            return fileName.EndsWith(".7z") ? fileName[..^3] : fileName;
        }

        private static async Task<List<Patch>> GetPatches(bool validateAll = false)
        {
            List<Patch> patches = new List<Patch>();

            try
            {
                string responseString = await Api.ClassicCounter.GetPatches();

                JObject responseJson = JObject.Parse(responseString);

                if (responseJson["files"] != null)
                    patches = responseJson["files"]!.ToObject<Patch[]>()!.ToList();
            }
            catch
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Couldn't get {(validateAll ? "full game" : "patch")} API data.");
            }

            return patches;
        }

        private static async Task<string> GetHash(string filePath)
        {
            MD5 md5 = MD5.Create();

            byte[] buffer = await File.ReadAllBytesAsync(filePath);
            byte[] hash = md5.ComputeHash(buffer, 0, buffer.Length);

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static async Task<Patches> ValidatePatches(bool validateAll = false)
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
                    if (Debug.Enabled())
                        Terminal.Debug("Checking hash for: csgo/pak_dat.vpk");

                    string pakDatHash = await GetHash(pakDatPath);
                    if (pakDatHash == pakDatPatch.Hash)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug("csgo/pak_dat.vpk is up to date - skipping other file checks");
                        skipValidation = true;
                        return new Patches(true, missing, outdated);
                    }
                    else
                    {
                        if (Debug.Enabled())
                            Terminal.Debug("csgo/pak_dat.vpk is outdated - will check all files");
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
                        if (dirHash != dirPatch.Hash)
                        {
                            if (Debug.Enabled())
                                Terminal.Debug("csgo/pak01_dir.vpk is outdated!");

                            File.Delete(dirPath);
                            outdated.Add(dirPatch);
                            needPak01Update = true;
                        }
                        else if (!Argument.Exists("--validate-all"))
                        {
                            if (Debug.Enabled())
                                Terminal.Debug("csgo/pak01_dir.vpk is up to date - will skip pak01 files");
                        }
                        else
                        {
                            if (Debug.Enabled())
                                Terminal.Debug("csgo/pak01_dir.vpk is up to date - checking all files anyway due to --validate-all");
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

                await Parallel.ForEachAsync(patches, parallelOptions, async (patch, cancellationToken) =>
                {
                    string originalFileName = GetOriginalFileName(patch.File);

                    // skip dir file (we already checked it)
                    if (originalFileName.Contains("pak01_dir.vpk"))
                        return;

                    // are you a pak01 file?
                    bool isPak01File = originalFileName.Contains("pak01_");
                    string path = Path.Combine(Directory.GetCurrentDirectory(), originalFileName);

                    if (isPak01File && !needPak01Update && !Argument.Exists("--validate-all"))
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
                        Terminal.Debug($"Checking hash for: {originalFileName}{(isPak01File && Argument.Exists("--validate-all") ? " (--validate-all)" : "")}");

                    string hash = await GetHash(path);
                    if (hash != patch.Hash)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Outdated: {originalFileName}");

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
