using Microsoft.Win32;
using Gameloop.Vdf;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using Microsoft.CSharp.RuntimeBinder;

namespace Wauncher.Utils
{
    public class Steam
    {
        public static string? recentSteamID64 { get; private set; }
        public static string? recentSteamID2 { get; private set; }

        private static string? steamPath { get; set; }

        private static string? GetSteamInstallPath()
        {
            // If was already found return it right away.
            if (steamPath != null)
                return steamPath;

            // Try finding it registry.
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey? key = hklm.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam") ?? hklm.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    steamPath = key?.GetValue("InstallPath") as string;
                    if (steamPath != null)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Steam folder found at {steamPath}");
                        return steamPath;
                    }
                }
            }

            // If registry didn't work, try natively.
            return steamPath = SteamNative.GetSteamInstallPath();
        }

        public static bool IsInstalled()
        {
            try
            {
                var path = GetSteamInstallPath();
                return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Steam.IsInstalled", ex, "Failed to check if Steam is installed");
                return false;
            }
        }

        public static async Task GetRecentLoggedInSteamID()
        {
            await GetRecentLoggedInSteamID(true);
        }

        public static async Task<bool> GetRecentLoggedInSteamID(bool exitOnMissing)
        {
            recentSteamID64 = null;
            recentSteamID2 = null;

            steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
            {
                if (!exitOnMissing)
                    return false;

                Terminal.Error("Your Steam install couldn't be found.");
                Terminal.Error("Closing launcher in 5 seconds...");
                await Task.Delay(5000);
                Environment.Exit(1);
                return false;
            }

            var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(loginUsersPath))
            {
                if (Debug.Enabled())
                    Terminal.Debug("loginusers.vdf not found, trying Steamworks API fallback...");
                
                if (SteamNative.GetSteamID2() == null)
                    SteamNative.GetSteamInstallPath();
                recentSteamID2 = SteamNative.GetSteamID2();
                recentSteamID64 = SteamNative.GetSteamID64();
                
                if (Debug.Enabled() && !string.IsNullOrEmpty(recentSteamID64))
                {
                    Terminal.Debug($"Steamworks fallback succeeded - SteamID64: {recentSteamID64}");
                    Terminal.Debug($"Steamworks fallback succeeded - SteamID2: {recentSteamID2}");
                }
                
                if (!exitOnMissing)
                    return !string.IsNullOrEmpty(recentSteamID2);

                if (string.IsNullOrEmpty(recentSteamID2))
                {
                    Terminal.Error("Steam login data couldn't be found and Steamworks fallback failed.");
                    Terminal.Error("Closing launcher in 5 seconds...");
                    await Task.Delay(5000);
                    Environment.Exit(1);
                }
                return !string.IsNullOrEmpty(recentSteamID2);
            }

            dynamic loginUsers = VdfConvert.Deserialize(File.ReadAllText(loginUsersPath));
            string? fallbackSteamId64 = null;

            foreach (var user in loginUsers.Value)
            {
                string? steamId64 = null;

                try
                {
                    steamId64 = Convert.ToString(user.Key);
                    if (string.IsNullOrWhiteSpace(fallbackSteamId64) && !string.IsNullOrWhiteSpace(steamId64))
                        fallbackSteamId64 = steamId64;

                    var mostRecent = Convert.ToString(user.Value?.MostRecent?.Value);
                    if (mostRecent == "1" && !string.IsNullOrWhiteSpace(steamId64))
                    {
                        recentSteamID64 = steamId64;
                        recentSteamID2 = ConvertToSteamID2(steamId64);
                        break;
                    }
                }
                catch (RuntimeBinderException ex)
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Skipping malformed Steam loginusers entry for {steamId64 ?? "unknown user"}.");
                    ErrorLogger.LogError("Steam.GetRecentLoggedInSteamID", ex, $"Malformed Steam loginusers entry for {steamId64 ?? "unknown user"}");
                }
            }

            if (string.IsNullOrWhiteSpace(recentSteamID64) && !string.IsNullOrWhiteSpace(fallbackSteamId64))
            {
                recentSteamID64 = fallbackSteamId64;
                recentSteamID2 = ConvertToSteamID2(fallbackSteamId64);
            }

            // If VDF method failed, try Steamworks API as fallback
            if (string.IsNullOrWhiteSpace(recentSteamID64))
            {
                if (Debug.Enabled())
                    Terminal.Debug("VDF method failed, trying Steamworks API fallback...");
                
                if (SteamNative.GetSteamID2() == null)
                    SteamNative.GetSteamInstallPath();
                recentSteamID2 = SteamNative.GetSteamID2();
                recentSteamID64 = SteamNative.GetSteamID64();
                
                if (Debug.Enabled() && !string.IsNullOrEmpty(recentSteamID64))
                {
                    Terminal.Debug($"Steamworks fallback succeeded - SteamID64: {recentSteamID64}");
                    Terminal.Debug($"Steamworks fallback succeeded - SteamID2: {recentSteamID2}");
                }
            }
            if (Debug.Enabled() && !string.IsNullOrEmpty(recentSteamID64))
            {
                Terminal.Debug($"Most recent Steam account (SteamID64): {recentSteamID64}");
                Terminal.Debug($"Most recent Steam account (SteamID2): {recentSteamID2}");
            }

            return !string.IsNullOrEmpty(recentSteamID2);
        }

        private static string ConvertToSteamID2(string steamID64)
        {
            ulong id64 = ulong.Parse(steamID64);
            ulong constValue = 76561197960265728;
            ulong accountID = id64 - constValue;
            ulong y = accountID % 2;
            ulong z = accountID / 2;
            return $"STEAM_1:{y}:{z}";
        }
    }
}

