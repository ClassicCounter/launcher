using Microsoft.Win32;
using Gameloop.Vdf;

namespace Launcher.Utils
{
    public class Steam
    {
        public static string? recentSteamID64 { get; private set; }
        public static string? recentSteamID2 { get; private set; }

        private static string? steamPath { get; set; }
        private static string? GetSteamInstallPath()
        {
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey? key = hklm.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam") ?? hklm.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    steamPath = key?.GetValue("InstallPath") as string;
                    if (Debug.Enabled())
                        Terminal.Debug($"Steam folder found at {steamPath}");
                    return steamPath;
                }
            }
        }
        public static async Task GetRecentLoggedInSteamID()
        {
            steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath))
            {
                Terminal.Error("Your Steam install couldn't be found.");
                Terminal.Error("Closing launcher in 5 seconds...");
                await Task.Delay(5000);
                Environment.Exit(1);
            }
            
            try
            {
                var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                dynamic loginUsers = VdfConvert.Deserialize(File.ReadAllText(loginUsersPath));
                foreach (var user in loginUsers.Value)
                {
                    var mostRecent = user.Value.MostRecent.Value;
                    if (mostRecent == "1")
                    {
                        recentSteamID64 = user.Key;
                        recentSteamID2 = ConvertToSteamID2(user.Key);
                    }
                }
                
                if (Debug.Enabled() && !string.IsNullOrEmpty(recentSteamID64))
                {
                    Terminal.Debug($"Most recent Steam account (SteamID64): {recentSteamID64}");
                    Terminal.Debug($"Most recent Steam account (SteamID2): {recentSteamID2}");
                }
            }
            catch (Exception ex)
            {
                if (Debug.Enabled())
                    Terminal.Debug($"Failed to read Steam user data: {ex.Message}");
                
                Terminal.Warning("Could not detect Steam account automatically.");
            }
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
