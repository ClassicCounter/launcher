using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wauncher.Utils
{
    public class SteamNative
    {
        // 64-bit steam_api calls
        [DllImport("platform/steam_api64", EntryPoint = "SteamAPI_InitFlat", CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern int SteamAPI64_InitFlat(byte* steamErrMsg);
        [DllImport("platform/steam_api64", EntryPoint = "SteamAPI_GetSteamInstallPath", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI64_GetSteamInstallPath();
        [DllImport("platform/steam_api64", EntryPoint = "SteamAPI_Shutdown", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI64_Shutdown();
        [DllImport("platform/steam_api64", EntryPoint = "SteamAPI_SteamUser_v023", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI64_SteamUser();
        [DllImport("platform/steam_api64", EntryPoint = "SteamAPI_ISteamUser_GetSteamID", CallingConvention = CallingConvention.Cdecl)]
        private static extern UInt64 SteamAPI64_ISteamUser_GetSteamID(IntPtr steamuser);
        [DllImport("platform/steam_api64", EntryPoint = "SteamAPI_ISteamUser_BLoggedOn", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SteamAPI64_ISteamUser_BLoggedOn(IntPtr steamuser);

        // 32-bit steam_api calls
        [DllImport("platform/steam_api", EntryPoint = "SteamAPI_InitFlat", CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern int SteamAPI_InitFlat(byte* steamErrMsg);
        [DllImport("platform/steam_api", EntryPoint = "SteamAPI_GetSteamInstallPath", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_GetSteamInstallPath();
        [DllImport("platform/steam_api", EntryPoint = "SteamAPI_Shutdown", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI_Shutdown();
        [DllImport("platform/steam_api", EntryPoint = "SteamAPI_SteamUser_v023", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_SteamUser();
        [DllImport("platform/steam_api", EntryPoint = "SteamAPI_ISteamUser_GetSteamID", CallingConvention = CallingConvention.Cdecl)]
        private static extern UInt64 SteamAPI_ISteamUser_GetSteamID(IntPtr steamuser);
        [DllImport("platform/steam_api", EntryPoint = "SteamAPI_ISteamUser_BLoggedOn", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SteamAPI_ISteamUser_BLoggedOn(IntPtr steamuser);


        private static string? _steamPath = null;

        private static string? _steamId2 = null;
        private static string? _steamId64 = null;

        private static UInt64 _rawSteamId64 = 0;

        public static string? GetSteamInstallPath()
        {
            // If was already found return it right away.
            if (_steamPath != null)
                return _steamPath;

            // If it wasn't found before by registry, try using Steamworks.
            // (Steamworks.NET doesn't give access to native methods)
            string steamDll = Environment.Is64BitProcess ? "steam_api64.dll" : "steam_api.dll";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(steamDll)
                ?? throw new Exception($"{steamDll} wasn't found in binary!");

            // If needed steam_api(64).dll doesn't exist in folder, unpack it from the binary.
            var outputPath = Path.Combine(AppContext.BaseDirectory, "platform", steamDll);
            if (!File.Exists(outputPath))
            {
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "platform"));
                using (var file = File.Create(outputPath))
                    stream.CopyTo(file);
            }

            // Make sure steam_appid.txt exists, because if it doesn't steam throws an error.
            if (!File.Exists("steam_appid.txt"))
                File.WriteAllText("steam_appid.txt", "730");

            unsafe
            {
                byte* errMsg = stackalloc byte[1024];
                int result = Environment.Is64BitProcess ? SteamAPI64_InitFlat(errMsg) : SteamAPI_InitFlat(errMsg);
                if (result > 0)
                {
                    ConsoleManager.ShowError($"Steamworks couldn't initialize to find Steam. (Error ({result}) {Marshal.PtrToStringUTF8((IntPtr)errMsg)})");
                    return null;
                }
                _steamPath = Marshal.PtrToStringUTF8(Environment.Is64BitProcess ? SteamAPI64_GetSteamInstallPath() : SteamAPI_GetSteamInstallPath());

                IntPtr steamuser = 0;
                if (Environment.Is64BitProcess) steamuser = SteamAPI64_SteamUser(); else steamuser = SteamAPI_SteamUser();
               
                if (steamuser != IntPtr.Zero)
                {
                    if (Environment.Is64BitProcess ? SteamAPI64_ISteamUser_BLoggedOn(steamuser) : SteamAPI_ISteamUser_BLoggedOn(steamuser))
                    {
                        if (Environment.Is64BitProcess) 
                            _rawSteamId64 = SteamAPI64_ISteamUser_GetSteamID(steamuser); 
                        else 
                            _rawSteamId64 = SteamAPI_ISteamUser_GetSteamID(steamuser);

                        _steamId64 = _rawSteamId64.ToString();
                        _steamId2 = ConvertToSteamID2(_steamId64);
                        //ConsoleManager.ShowError($"SteamId64: '{_steamId64}' ({_rawSteamId64}) | SteamId2: '{_steamId2}'");
                    }
                    else
                    {
                        ConsoleManager.ShowError("You're not logged into Steam. Login and try again.");
                    }
                }

                if (Environment.Is64BitProcess) SteamAPI64_Shutdown(); else SteamAPI_Shutdown();
            }

            return _steamPath;
        }

        public static string? GetSteamID64()
        {
            return _steamId64;
        }
        public static string? GetSteamID2()
        {
            return _steamId2;
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
