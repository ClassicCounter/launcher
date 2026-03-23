using Avalonia.Platform;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Wauncher.Utils
{
    internal static class SteamShortcutManager
    {
        private const string ShortcutName = "ClassicCounter";

        public static async Task<bool> IsClassicCounterAddedToSteamAsync(string wauncherExePath)
        {
            if (string.IsNullOrWhiteSpace(wauncherExePath) || !File.Exists(wauncherExePath))
                return false;

            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
                return false;

            bool hasSteamUser = await Steam.GetRecentLoggedInSteamID(false);
            if (!hasSteamUser || string.IsNullOrWhiteSpace(Steam.recentSteamID64))
                return false;

            string userDataId = GetSteamUserDataId(Steam.recentSteamID64);
            string shortcutsPath = Path.Combine(steamPath, "userdata", userDataId, "config", "shortcuts.vdf");
            if (!File.Exists(shortcutsPath))
                return false;

            try
            {
                var shortcuts = ReadShortcuts(shortcutsPath);
                return shortcuts.Any(s =>
                    string.Equals(s.AppName, ShortcutName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeExe(s.Exe), NormalizeExe(wauncherExePath), StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public static async Task AddClassicCounterToSteamAsync(string wauncherExePath)
        {
            if (string.IsNullOrWhiteSpace(wauncherExePath) || !File.Exists(wauncherExePath))
                throw new FileNotFoundException("Wauncher executable could not be found.", wauncherExePath);

            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
                throw new InvalidOperationException("Steam install path could not be found.");

            bool restartSteam = false;
            var steamExe = Path.Combine(steamPath, "steam.exe");
            if (Process.GetProcessesByName("steam").Length > 0)
            {
                restartSteam = true;
                TryShutdownSteam(steamExe);
                await WaitForSteamToExitAsync();
            }

            bool hasSteamUser = await Steam.GetRecentLoggedInSteamID(false);
            if (!hasSteamUser || string.IsNullOrWhiteSpace(Steam.recentSteamID64))
                throw new InvalidOperationException("No logged-in Steam user could be detected.");

            string userDataId = GetSteamUserDataId(Steam.recentSteamID64);
            string configDir = Path.Combine(steamPath, "userdata", userDataId, "config");
            string shortcutsPath = Path.Combine(configDir, "shortcuts.vdf");
            string gridDir = Path.Combine(configDir, "grid");

            Directory.CreateDirectory(configDir);
            Directory.CreateDirectory(gridDir);

            var shortcuts = File.Exists(shortcutsPath)
                ? ReadShortcuts(shortcutsPath)
                : new List<SteamShortcutEntry>();

            var existing = shortcuts.FirstOrDefault(s =>
                string.Equals(s.AppName, ShortcutName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeExe(s.Exe), NormalizeExe(wauncherExePath), StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new SteamShortcutEntry
                {
                    AppId = GenerateUniqueAppId(shortcuts),
                    AppName = ShortcutName
                };
                shortcuts.Add(existing);
            }

            string startDir = Path.GetDirectoryName(wauncherExePath) ?? AppContext.BaseDirectory;
            if (!startDir.EndsWith(Path.DirectorySeparatorChar))
                startDir += Path.DirectorySeparatorChar;

            string iconPath = File.Exists(Path.Combine(startDir, "cc.exe"))
                ? Path.Combine(startDir, "cc.exe")
                : wauncherExePath;

            existing.AppName = ShortcutName;
            existing.Exe = FormatExe(wauncherExePath);
            existing.StartDir = startDir;
            existing.Icon = iconPath;
            existing.ShortcutPath = string.Empty;
            existing.LaunchOptions = string.Empty;
            existing.IsHidden = 0;
            existing.AllowDesktopConfig = 1;
            existing.AllowOverlay = 1;
            existing.OpenVR = 0;
            existing.Devkit = 0;
            existing.DevkitGameId = string.Empty;
            existing.DevkitOverrideAppId = 0;
            existing.LastPlayTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            existing.FlatpakAppId = string.Empty;
            existing.SortAs = string.Empty;
            existing.Tags.Clear();

            WriteShortcuts(shortcutsPath, shortcuts);

            uint unsignedAppId = unchecked((uint)existing.AppId);
            WriteGridArtwork(gridDir, unsignedAppId);
            CreateDesktopShortcut(unsignedAppId, wauncherExePath);

            if (restartSteam && File.Exists(steamExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = steamExe,
                    UseShellExecute = true,
                    WorkingDirectory = steamPath
                });
            }
        }

        private static string? GetSteamInstallPath()
        {
            foreach (var keyPath in new[]
                     {
                         @"SOFTWARE\Wow6432Node\Valve\Steam",
                         @"SOFTWARE\Valve\Steam"
                     })
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = hklm.OpenSubKey(keyPath);
                var installPath = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(installPath))
                    return installPath;
            }

            return null;
        }

        private static void TryShutdownSteam(string steamExe)
        {
            try
            {
                if (!File.Exists(steamExe))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = steamExe,
                    Arguments = "-shutdown",
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(steamExe) ?? Environment.CurrentDirectory
                });
            }
            catch
            {
            }
        }

        private static async Task WaitForSteamToExitAsync(int timeoutMs = 15000)
        {
            var start = Environment.TickCount64;
            while (Process.GetProcessesByName("steam").Length > 0)
            {
                if (Environment.TickCount64 - start > timeoutMs)
                    throw new InvalidOperationException("Steam did not close in time. Please fully close Steam and try again.");

                await Task.Delay(250);
            }
        }

        private static string GetSteamUserDataId(string steamId64)
        {
            ulong steamId = ulong.Parse(steamId64);
            ulong accountId = steamId - 76561197960265728UL;
            return accountId.ToString();
        }

        private static string NormalizeExe(string exe) => exe.Trim().Trim('"');

        private static string FormatExe(string path) => $"\"{path.Trim('"')}\"";

        private static int GenerateUniqueAppId(List<SteamShortcutEntry> shortcuts)
        {
            var used = shortcuts.Select(s => s.AppId).ToHashSet();
            uint seed = Crc32(Encoding.UTF8.GetBytes($"{ShortcutName}|{Environment.MachineName}|{DateTime.UtcNow.Ticks}"));

            for (int i = 0; i < 1024; i++)
            {
                int appId = unchecked((int)(0x80000000u | (seed + (uint)i)));
                if (appId != 0 && !used.Contains(appId))
                    return appId;
            }

            throw new InvalidOperationException("Failed to generate a unique Steam shortcut app id.");
        }

        private static uint Crc32(byte[] bytes)
        {
            uint crc = 0xffffffff;
            foreach (byte b in bytes)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    uint mask = (uint)-(int)(crc & 1);
                    crc = (crc >> 1) ^ (0xedb88320 & mask);
                }
            }
            return ~crc;
        }

        private static void WriteGridArtwork(string gridDir, uint appId)
        {
            WriteAssetToFile("avares://Wauncher/Assets/steam/wide_cover.png", Path.Combine(gridDir, $"{appId}.png"));
            WriteAssetToFile("avares://Wauncher/Assets/steam/cover.png", Path.Combine(gridDir, $"{appId}p.png"));
            WriteAssetToFile("avares://Wauncher/Assets/steam/hero.png", Path.Combine(gridDir, $"{appId}_hero.png"));
            WriteAssetToFile("avares://Wauncher/Assets/steam/logo.png", Path.Combine(gridDir, $"{appId}_logo.png"));
        }

        private static void WriteAssetToFile(string assetUri, string destinationPath)
        {
            using var assetStream = AssetLoader.Open(new Uri(assetUri));
            using var fileStream = File.Create(destinationPath);
            assetStream.CopyTo(fileStream);
        }

        private static void CreateDesktopShortcut(uint appId, string wauncherExePath)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktopPath, $"{ShortcutName}.url");
            ulong steamGameId = ((ulong)appId << 32) | 0x02000000UL;
            string startDir = Path.GetDirectoryName(wauncherExePath) ?? AppContext.BaseDirectory;
            string desktopIconPath = File.Exists(Path.Combine(startDir, "cc.exe"))
                ? Path.Combine(startDir, "cc.exe")
                : wauncherExePath;
            var lines = new[]
            {
                "[InternetShortcut]",
                $"URL=steam://rungameid/{steamGameId}",
                $"IconFile={desktopIconPath}",
                "IconIndex=0"
            };
            File.WriteAllLines(shortcutPath, lines, Encoding.UTF8);
        }

        private static List<SteamShortcutEntry> ReadShortcuts(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            if (reader.ReadByte() != 0)
                throw new InvalidDataException("Invalid Steam shortcuts file.");

            ReadCString(reader); // root name: shortcuts

            var shortcuts = new List<SteamShortcutEntry>();
            while (stream.Position < stream.Length)
            {
                byte type = reader.ReadByte();
                if (type == 8)
                    break;
                if (type != 0)
                    throw new InvalidDataException("Unexpected shortcut entry type.");

                ReadCString(reader); // index
                shortcuts.Add(ReadShortcutEntry(reader));
            }

            return shortcuts;
        }

        private static SteamShortcutEntry ReadShortcutEntry(BinaryReader reader)
        {
            var entry = new SteamShortcutEntry();
            while (true)
            {
                byte type = reader.ReadByte();
                if (type == 8)
                    break;

                string key = ReadCString(reader);
                switch (type)
                {
                    case 0:
                        if (key == "tags")
                            entry.Tags = ReadTags(reader);
                        else
                            SkipObject(reader);
                        break;
                    case 1:
                        SetString(entry, key, ReadCString(reader));
                        break;
                    case 2:
                        SetInt(entry, key, reader.ReadInt32());
                        break;
                    default:
                        throw new InvalidDataException($"Unsupported shortcuts field type: {type}");
                }
            }

            return entry;
        }

        private static List<string> ReadTags(BinaryReader reader)
        {
            var tags = new List<string>();
            while (true)
            {
                byte type = reader.ReadByte();
                if (type == 8)
                    break;
                string key = ReadCString(reader);
                if (type == 1)
                    tags.Add(ReadCString(reader));
                else
                    throw new InvalidDataException($"Unsupported tag type: {type} ({key})");
            }

            return tags;
        }

        private static void SkipObject(BinaryReader reader)
        {
            while (true)
            {
                byte type = reader.ReadByte();
                if (type == 8)
                    break;
                ReadCString(reader);
                if (type == 0)
                    SkipObject(reader);
                else if (type == 1)
                    ReadCString(reader);
                else if (type == 2)
                    reader.ReadInt32();
                else
                    throw new InvalidDataException($"Unsupported nested field type: {type}");
            }
        }

        private static void WriteShortcuts(string path, List<SteamShortcutEntry> shortcuts)
        {
            string tempPath = path + ".tmp";
            using (var stream = File.Create(tempPath))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write((byte)0);
                WriteCString(writer, "shortcuts");

                for (int i = 0; i < shortcuts.Count; i++)
                {
                    writer.Write((byte)0);
                    WriteCString(writer, i.ToString());
                    WriteShortcutEntry(writer, shortcuts[i]);
                }

                writer.Write((byte)8);
                writer.Write((byte)8);
            }

            File.Copy(tempPath, path, overwrite: true);
            File.Delete(tempPath);
        }

        private static void WriteShortcutEntry(BinaryWriter writer, SteamShortcutEntry entry)
        {
            WriteIntField(writer, "appid", entry.AppId);
            WriteStringField(writer, "AppName", entry.AppName);
            WriteStringField(writer, "Exe", entry.Exe);
            WriteStringField(writer, "StartDir", entry.StartDir);
            WriteStringField(writer, "icon", entry.Icon);
            WriteStringField(writer, "ShortcutPath", entry.ShortcutPath);
            WriteStringField(writer, "LaunchOptions", entry.LaunchOptions);
            WriteIntField(writer, "IsHidden", entry.IsHidden);
            WriteIntField(writer, "AllowDesktopConfig", entry.AllowDesktopConfig);
            WriteIntField(writer, "AllowOverlay", entry.AllowOverlay);
            WriteIntField(writer, "OpenVR", entry.OpenVR);
            WriteIntField(writer, "Devkit", entry.Devkit);
            WriteStringField(writer, "DevkitGameID", entry.DevkitGameId);
            WriteIntField(writer, "DevkitOverrideAppID", entry.DevkitOverrideAppId);
            WriteIntField(writer, "LastPlayTime", entry.LastPlayTime);
            WriteStringField(writer, "FlatpakAppID", entry.FlatpakAppId);
            WriteStringField(writer, "sortas", entry.SortAs);

            writer.Write((byte)0);
            WriteCString(writer, "tags");
            for (int i = 0; i < entry.Tags.Count; i++)
            {
                writer.Write((byte)1);
                WriteCString(writer, i.ToString());
                WriteCString(writer, entry.Tags[i]);
            }
            writer.Write((byte)8);

            writer.Write((byte)8);
        }

        private static void WriteStringField(BinaryWriter writer, string key, string value)
        {
            writer.Write((byte)1);
            WriteCString(writer, key);
            WriteCString(writer, value ?? string.Empty);
        }

        private static void WriteIntField(BinaryWriter writer, string key, int value)
        {
            writer.Write((byte)2);
            WriteCString(writer, key);
            writer.Write(value);
        }

        private static string ReadCString(BinaryReader reader)
        {
            var bytes = new List<byte>();
            byte value;
            while ((value = reader.ReadByte()) != 0)
                bytes.Add(value);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static void WriteCString(BinaryWriter writer, string value)
        {
            writer.Write(Encoding.UTF8.GetBytes(value));
            writer.Write((byte)0);
        }

        private static void SetString(SteamShortcutEntry entry, string key, string value)
        {
            switch (key)
            {
                case "AppName": entry.AppName = value; break;
                case "Exe": entry.Exe = value; break;
                case "StartDir": entry.StartDir = value; break;
                case "icon": entry.Icon = value; break;
                case "ShortcutPath": entry.ShortcutPath = value; break;
                case "LaunchOptions": entry.LaunchOptions = value; break;
                case "DevkitGameID": entry.DevkitGameId = value; break;
                case "FlatpakAppID": entry.FlatpakAppId = value; break;
                case "sortas": entry.SortAs = value; break;
            }
        }

        private static void SetInt(SteamShortcutEntry entry, string key, int value)
        {
            switch (key)
            {
                case "appid": entry.AppId = value; break;
                case "IsHidden": entry.IsHidden = value; break;
                case "AllowDesktopConfig": entry.AllowDesktopConfig = value; break;
                case "AllowOverlay": entry.AllowOverlay = value; break;
                case "OpenVR": entry.OpenVR = value; break;
                case "Devkit": entry.Devkit = value; break;
                case "DevkitOverrideAppID": entry.DevkitOverrideAppId = value; break;
                case "LastPlayTime": entry.LastPlayTime = value; break;
            }
        }

        private sealed class SteamShortcutEntry
        {
            public int AppId { get; set; }
            public string AppName { get; set; } = string.Empty;
            public string Exe { get; set; } = string.Empty;
            public string StartDir { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public string ShortcutPath { get; set; } = string.Empty;
            public string LaunchOptions { get; set; } = string.Empty;
            public int IsHidden { get; set; }
            public int AllowDesktopConfig { get; set; } = 1;
            public int AllowOverlay { get; set; } = 1;
            public int OpenVR { get; set; }
            public int Devkit { get; set; }
            public string DevkitGameId { get; set; } = string.Empty;
            public int DevkitOverrideAppId { get; set; }
            public int LastPlayTime { get; set; }
            public string FlatpakAppId { get; set; } = string.Empty;
            public string SortAs { get; set; } = string.Empty;
            public List<string> Tags { get; set; } = new();
        }
    }
}
