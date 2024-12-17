﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        private static async Task<List<Patch>> GetPatches()
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
                    Terminal.Debug("Couldn't get patches.");
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

        public static async Task<Patches> ValidatePatches()
        {
            List<Patch> patches = await GetPatches();

            List<Patch> missing = new();
            List<Patch> outdated = new();

            // find pak01_dir.vpk from patch api
            var dirPatch = patches.FirstOrDefault(p =>
                p.File.Contains("pak01_dir.vpk"));

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
                    else
                    {
                        if (Debug.Enabled())
                            Terminal.Debug("csgo/pak01_dir.vpk is up to date - will skip pak01 files");
                    }
                }
                else
                {
                    if (Debug.Enabled())
                        Terminal.Debug("Missing: csgo/pak01_dir.vpk!");

                    missing.Add(dirPatch);
                    needPak01Update = true;
                }
            }

            foreach (Patch patch in patches)
            {
                string originalFileName = GetOriginalFileName(patch.File);

                // skip dir file (we already checked it)
                if (originalFileName.Contains("pak01_dir.vpk"))
                    continue;

                // are you a pak01 file?
                bool isPak01File = originalFileName.Contains("pak01_");

                string path = $"{Directory.GetCurrentDirectory()}/{originalFileName}";

                if (isPak01File)
                {
                    if (!File.Exists(path))
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Missing: {originalFileName}");

                        missing.Add(patch);
                        continue;
                    }

                    if (needPak01Update)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Checking hash for: {originalFileName} (pak01_dir.vpk was outdated)");

                        string hash = await GetHash(path);
                        if (hash != patch.Hash)
                        {
                            if (Debug.Enabled())
                                Terminal.Debug($"Outdated: {originalFileName}");

                            File.Delete(path);
                            outdated.Add(patch);
                        }
                    }
                    else
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Skipping hash check for: {originalFileName} (pak01_dir.vpk up to date)");
                    }
                }
                else
                {
                    if (!File.Exists(path))
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Missing: {originalFileName}");

                        missing.Add(patch);
                        continue;
                    }

                    if (Debug.Enabled())
                        Terminal.Debug($"Checking hash for: {originalFileName}");

                    string hash = await GetHash(path);
                    if (hash != patch.Hash)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Outdated: {originalFileName}");

                        File.Delete(path);
                        outdated.Add(patch);
                    }
                }
            }

            return new Patches(patches.Count > 0, missing, outdated);
        }
    }
}
