using Microsoft.Win32;
using Gameloop.Vdf;

namespace Launcher.Utils
{
    public class Steam
    {
        public static string? recentSteamID {  get; private set; }
        private static string? steamPath { get; set; }
        private static async Task GetSteamInstallPath()
        {
            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (var key = hklm.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam") ?? hklm.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    steamPath = key?.GetValue("InstallPath") as string;
                    if (Debug.Enabled())
                        Terminal.Debug($"Steam folder found at {steamPath}");
                }
            }
        }
        public static async Task GetRecentLoggedInSteamID()
        {
            await GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath))
            {
                Terminal.Error("Steam not found. Get Steam.");
                Terminal.Error("Closing launcher in 5 seconds...");
                await Task.Delay(5000);
                Environment.Exit(1);
            }

            var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
            dynamic loginUsers = VdfConvert.Deserialize(File.ReadAllText(loginUsersPath));

            foreach(var user in loginUsers.Value)
            {
                var mostRecent = user.Value.MostRecent.Value;
                if (mostRecent == "1")
                {
                    recentSteamID = user.Key;
                }
            }
            if (Debug.Enabled() && !string.IsNullOrEmpty(recentSteamID))
                Terminal.Debug($"Most recent Steam account: {recentSteamID}");
        }
    }
}
