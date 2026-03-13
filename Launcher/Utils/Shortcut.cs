using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Launcher.Utils
{
    public static class ShortcutManager
    {
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        internal interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        internal interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig]
            int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        public static void CreateDesktopShortcut(string targetPath, string shortcutName, string description = "", string arguments = "", string iconPath = "")
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, $"{shortcutName}.lnk");

                if (File.Exists(shortcutPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Desktop shortcut already exists: {shortcutPath}");
                    return;
                }

                IShellLink link = (IShellLink)new ShellLink();
                link.SetPath(targetPath);
                link.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? "");
                
                if (!string.IsNullOrEmpty(description))
                    link.SetDescription(description);
                
                if (!string.IsNullOrEmpty(arguments))
                    link.SetArguments(arguments);
                
                if (!string.IsNullOrEmpty(iconPath))
                    link.SetIconLocation(iconPath, 0);
                else
                    link.SetIconLocation(targetPath, 0);

                IPersistFile file = (IPersistFile)link;
                file.Save(shortcutPath, false);

                if (Debug.Enabled())
                    Terminal.Debug($"Created desktop shortcut: {shortcutPath}");
            }
            catch (Exception ex)
            {
                Terminal.Warning($"Failed to create desktop shortcut: {ex.Message}");
            }
        }

        public static void CreateStartMenuShortcut(string targetPath, string shortcutName, string description = "", string arguments = "", string iconPath = "")
        {
            try
            {
                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string shortcutPath = Path.Combine(startMenuPath, $"{shortcutName}.lnk");

                if (File.Exists(shortcutPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Start menu shortcut already exists: {shortcutPath}");
                    return;
                }

                IShellLink link = (IShellLink)new ShellLink();
                link.SetPath(targetPath);
                link.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? "");
                
                if (!string.IsNullOrEmpty(description))
                    link.SetDescription(description);
                
                if (!string.IsNullOrEmpty(arguments))
                    link.SetArguments(arguments);
                
                if (!string.IsNullOrEmpty(iconPath))
                    link.SetIconLocation(iconPath, 0);
                else
                    link.SetIconLocation(targetPath, 0);

                IPersistFile file = (IPersistFile)link;
                file.Save(shortcutPath, false);

                if (Debug.Enabled())
                    Terminal.Debug($"Created start menu shortcut: {shortcutPath}");
            }
            catch (Exception ex)
            {
                Terminal.Warning($"Failed to create start menu shortcut: {ex.Message}");
            }
        }

        public static void AddToSteamLibrary(string gamePath, string gameName)
        {
            try
            {
                string? steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    Terminal.Warning("Steam installation not found. Cannot add to Steam library.");
                    return;
                }

                string? userDataPath = GetSteamUserDataPath(steamPath);
                if (string.IsNullOrEmpty(userDataPath))
                {
                    Terminal.Warning("Steam user data not found. Cannot add to Steam library.");
                    return;
                }

                string shortcutsPath = Path.Combine(userDataPath, "config", "shortcuts.vdf");
                
                if (Debug.Enabled())
                    Terminal.Debug($"Adding {gameName} to Steam library at: {shortcutsPath}");

                List<byte> fileBytes = new List<byte>();
                int nextIndex = 0;

                if (File.Exists(shortcutsPath))
                {
                    try
                    {
                        byte[] existingBytes = File.ReadAllBytes(shortcutsPath);
                        
                        if (CheckIfGameExists(existingBytes, gameName, gamePath))
                        {
                            if (Debug.Enabled())
                                Terminal.Debug($"{gameName} is already in Steam library");
                            return;
                        }

                        nextIndex = GetNextShortcutIndex(existingBytes);
                        fileBytes.AddRange(existingBytes.Take(existingBytes.Length - 2));
                    }
                    catch (Exception ex)
                    {
                        if (Debug.Enabled())
                            Terminal.Debug($"Could not parse existing shortcuts.vdf: {ex.Message}");
                        
                        fileBytes.AddRange(Encoding.UTF8.GetBytes("\x00shortcuts\x00"));
                    }
                }
                else
                {
                    fileBytes.AddRange(Encoding.UTF8.GetBytes("\x00shortcuts\x00"));
                }

                fileBytes.AddRange(CreateShortcutEntry(nextIndex, gameName, gamePath));
                fileBytes.Add(0x08);
                fileBytes.Add(0x08);

                Directory.CreateDirectory(Path.GetDirectoryName(shortcutsPath) ?? "");
                File.WriteAllBytes(shortcutsPath, fileBytes.ToArray());

                if (Debug.Enabled())
                    Terminal.Debug($"Successfully added {gameName} to Steam library");
                
                Terminal.Success($"Added {gameName} to Steam library! Restart Steam to see it in your library.");
            }
            catch (Exception ex)
            {
                Terminal.Warning($"Failed to add game to Steam library: {ex.Message}");
                if (Debug.Enabled())
                    Terminal.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        private static byte[] CreateShortcutEntry(int index, string appName, string exePath)
        {
            List<byte> entry = new List<byte>();
            string startDir = Path.GetDirectoryName(exePath) ?? "";
            
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes(index.ToString()));
            entry.Add(0x00);
            
            entry.Add(0x02);
            entry.AddRange(Encoding.UTF8.GetBytes("appid"));
            entry.Add(0x00);
            entry.AddRange(BitConverter.GetBytes(GenerateAppId(exePath)));
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("AppName"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes(appName));
            entry.Add(0x00);
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("Exe"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes($"\"{exePath}\""));
            entry.Add(0x00);
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("StartDir"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes($"\"{startDir}\""));
            entry.Add(0x00);
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("icon"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes(""));
            entry.Add(0x00);
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("ShortcutPath"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes(""));
            entry.Add(0x00);
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("LaunchOptions"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes(""));
            entry.Add(0x00);
            
            entry.Add(0x02);
            entry.AddRange(Encoding.UTF8.GetBytes("IsHidden"));
            entry.Add(0x00);
            entry.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            
            entry.Add(0x02);
            entry.AddRange(Encoding.UTF8.GetBytes("AllowDesktopConfig"));
            entry.Add(0x00);
            entry.AddRange(new byte[] { 0x01, 0x00, 0x00, 0x00 });
            
            entry.Add(0x02);
            entry.AddRange(Encoding.UTF8.GetBytes("AllowOverlay"));
            entry.Add(0x00);
            entry.AddRange(new byte[] { 0x01, 0x00, 0x00, 0x00 });
            
            entry.Add(0x02);
            entry.AddRange(Encoding.UTF8.GetBytes("OpenVR"));
            entry.Add(0x00);
            entry.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            
            entry.Add(0x02);
            entry.AddRange(Encoding.UTF8.GetBytes("Devkit"));
            entry.Add(0x00);
            entry.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("DevkitGameID"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes(""));
            entry.Add(0x00);
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("DevkitOverrideAppID"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes(""));
            entry.Add(0x00);
            
            entry.Add(0x02);
            entry.AddRange(Encoding.UTF8.GetBytes("LastPlayTime"));
            entry.Add(0x00);
            entry.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("FlatpakAppID"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes(""));
            entry.Add(0x00);
            
            entry.Add(0x01);
            entry.AddRange(Encoding.UTF8.GetBytes("sortas"));
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes(""));
            entry.Add(0x00);
            
            entry.Add(0x00);
            entry.AddRange(Encoding.UTF8.GetBytes("tags"));
            entry.Add(0x00);
            entry.Add(0x08);
            
            entry.Add(0x08);
            
            return entry.ToArray();
        }

        private static bool CheckIfGameExists(byte[] vdfBytes, string gameName, string gamePath)
        {
            string content = Encoding.UTF8.GetString(vdfBytes);
            return content.Contains($"\"{gameName}\"") && content.Contains($"\"{gamePath}\"");
        }

        private static int GetNextShortcutIndex(byte[] vdfBytes)
        {
            int maxIndex = -1;
            string content = Encoding.UTF8.GetString(vdfBytes);
            
            for (int i = 0; i < 100; i++)
            {
                if (content.Contains($"\x00{i}\x00"))
                    maxIndex = i;
            }
            
            return maxIndex + 1;
        }

        private static string? GetSteamInstallPath()
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    using (RegistryKey? key = hklm.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam") ?? hklm.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                    {
                        return key?.GetValue("InstallPath") as string;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static string? GetSteamUserDataPath(string steamPath)
        {
            try
            {
                string userDataDir = Path.Combine(steamPath, "userdata");
                if (!Directory.Exists(userDataDir))
                    return null;

                var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (!File.Exists(loginUsersPath))
                    return null;

                dynamic loginUsers = Gameloop.Vdf.VdfConvert.Deserialize(File.ReadAllText(loginUsersPath));
                string? mostRecentId = null;
                
                foreach (var user in loginUsers.Value)
                {
                    var mostRecent = user.Value.MostRecent.Value;
                    if (mostRecent == "1")
                    {
                        string steamId64 = user.Key;
                        mostRecentId = ConvertSteamId64ToAccountId(steamId64);
                        break;
                    }
                }

                if (mostRecentId != null)
                {
                    string userPath = Path.Combine(userDataDir, mostRecentId);
                    if (Directory.Exists(userPath))
                        return userPath;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string ConvertSteamId64ToAccountId(string steamId64)
        {
            ulong id64 = ulong.Parse(steamId64);
            ulong constValue = 76561197960265728;
            ulong accountID = id64 - constValue;
            return accountID.ToString();
        }

        private static int GenerateAppId(string gamePath)
        {
            string key = gamePath + "ClassicCounter";
            uint hash = 0;
            foreach (char c in key)
            {
                hash = hash * 31 + c;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
