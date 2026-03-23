using Avalonia;
using Avalonia.Win32;
using System.IO;
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
                try
                {
                    var logPath = Path.Combine(Path.GetDirectoryName(System.Environment.ProcessPath) ?? ".", "wauncher_error.log");
                    File.WriteAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}");
                }
                catch
                {
                }

                throw;
            }
            finally
            {
                // Cleanup EventWaitHandle to prevent zombie processes
                ProgramStarted?.Dispose();
                ProgramStarted = null;
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
                try
                {
                    var logPath = Path.Combine(Path.GetDirectoryName(System.Environment.ProcessPath) ?? ".", "wauncher_startup_error.log");
                    File.WriteAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\nOnStartup Error:\n{ex}");
                }
                catch
                {
                }

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
            catch
            {
            }

            return false;
        }
    }
}
