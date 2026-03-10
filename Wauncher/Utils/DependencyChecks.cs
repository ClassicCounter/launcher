using Microsoft.Win32;

namespace Wauncher.Utils
{
    public static class DependencyChecks
    {
        public static bool IsDiscordInstalled()
        {
            if (!OperatingSystem.IsWindows())
                return true;

            if (HasDiscordProtocolCommand(Registry.CurrentUser) || HasDiscordProtocolCommand(Registry.LocalMachine))
                return true;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] candidates =
            {
                Path.Combine(localAppData, "Discord", "Update.exe"),
                Path.Combine(localAppData, "DiscordCanary", "Update.exe"),
                Path.Combine(localAppData, "DiscordPTB", "Update.exe"),
                Path.Combine(programFiles, "Discord", "Update.exe"),
                Path.Combine(programFilesX86, "Discord", "Update.exe"),
            };

            return candidates.Any(File.Exists);
        }

        private static bool HasDiscordProtocolCommand(RegistryKey root)
        {
            using var key =
                root.OpenSubKey(@"Software\Classes\discord\shell\open\command") ??
                root.OpenSubKey(@"SOFTWARE\Classes\discord\shell\open\command");

            var command = key?.GetValue(string.Empty) as string;
            return !string.IsNullOrWhiteSpace(command);
        }
    }
}
