using Avalonia;
using Avalonia.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Wauncher.Utils;
using Wauncher.ViewModels;
using static Wauncher.Utils.Services;

namespace Wauncher
{
    internal sealed class Program
    {
        public static EventWaitHandle? ProgramStarted;

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                var exeDirectory = Path.GetDirectoryName(GetExePath());
                if (!string.IsNullOrWhiteSpace(exeDirectory) && Directory.Exists(exeDirectory))
                    Directory.SetCurrentDirectory(exeDirectory);

                // Self-update: if a strictly newer release exists, hand off to the
                // updater and exit. Fails safe (offline/error -> just launch normally).
                if (TrySelfUpdate(args))
                {
                    Environment.Exit(0);
                    return;
                }

                if (OnStartup(args) == false)
                {
                    Environment.Exit(0);
                    return;
                }

                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Program.Main", ex, "Application startup failed");
                throw;
            }
            finally
            {
                // Cleanup EventWaitHandle to prevent zombie processes
                ProgramStarted?.Dispose();
                ProgramStarted = null;
            }
        }

        /// <summary>
        /// Checks GitHub for a strictly-newer release. If found, downloads the updater
        /// and hands off to it, returning true so the app exits. Fails safe: any error,
        /// offline state, or a not-newer version returns false and the app launches normally.
        /// </summary>
        private static bool TrySelfUpdate(string[] args)
        {
            try
            {
                if (!IsWindows())
                    return false;

                if (args.Any(a => string.Equals(a, "--skip-updates", StringComparison.OrdinalIgnoreCase)))
                    return false;

                // Was just relaunched by the updater; don't immediately re-check.
                if (args.Any(a => string.Equals(a, "--updated", StringComparison.OrdinalIgnoreCase)))
                    return false;

                var latest = Wauncher.Utils.Version.GetLatestVersion().GetAwaiter().GetResult();

                // Only update when the latest release is strictly newer (prevents
                // downgrades and update loops if versions merely differ in format).
                if (!System.Version.TryParse(latest, out var latestVer) ||
                    !System.Version.TryParse(Wauncher.Utils.Version.Current, out var currentVer) ||
                    latestVer <= currentVer)
                    return false;

                var updaterPath = Path.Combine(Directory.GetCurrentDirectory(), "updater.exe");
                DownloadManager.DownloadUpdater(updaterPath).GetAwaiter().GetResult();
                if (!File.Exists(updaterPath))
                    return false;

                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"--version={latest} --ui",
                    UseShellExecute = false
                });
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Program.TrySelfUpdate", ex, "Self-update check failed");
                return false;
            }
        }

        // Reference (COPYPASTA)
        // https://github.com/2dust/v2rayN/blob/d9843dc77502454b1ec48cec6244e115f1abd082/v2rayN/v2rayN.Desktop/Program.cs#L25-L52
        private static bool OnStartup(string[]? Args)
        {
            try
            {
                if (IsWindows())
                {
                    var exePathKey = GetMd5(GetExePath());
                    var rebootas = (Args ?? []).Any(t => t == "rebootas");
                    ProgramStarted = new EventWaitHandle(false, EventResetMode.AutoReset, exePathKey, out var bCreatedNew);
                    if (!rebootas && !bCreatedNew)
                    {
                        ProgramStarted?.Set();
                        ProgramStarted?.Dispose();
                        ProgramStarted = null;
                        return false;
                    }
                }
                else
                {
                    _ = new Mutex(true, "Wauncher", out var bOnlyOneInstance);
                    if (!bOnlyOneInstance)
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Program.OnStartup", ex, "Startup validation failed");
                return true; // Allow app to continue anyway
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect();

            if (IsWindows() && IsHardwareAccelerationDisabled())
            {
                builder = builder.With(new Win32PlatformOptions
                {
                    RenderingMode = new[] { Win32RenderingMode.Software }
                });
            }

            return builder
                .WithInterFont()
                .LogToTrace();
        }

        private static bool IsHardwareAccelerationDisabled()
        {
            try
            {
                var path = SettingsWindowViewModel.SettingsPath();
                if (!File.Exists(path))
                    return false;

                foreach (var line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    var key = line[..eq].Trim();
                    var value = line[(eq + 1)..].Trim();

                    if (key == "DisableHardwareAcceleration")
                        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Program.IsHardwareAccelerationDisabled", ex, "Failed to check hardware acceleration setting");
            }

            return false;
        }
    }
}
