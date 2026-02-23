using Avalonia;
using Wauncher.Utils;

namespace Wauncher
{
    internal sealed class Program
    {
        public static EventWaitHandle ProgramStarted;

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            if (OnStartup(args) == false)
            {
                Environment.Exit(0);
                return;
            }

            BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        }

        // Reference (COPYPASTA) 
        // https://github.com/2dust/v2rayN/blob/d9843dc77502454b1ec48cec6244e115f1abd082/v2rayN/v2rayN.Desktop/Program.cs#L25-L52
        private static bool OnStartup(string[]? Args)
        {

            if (Services.IsWindows())
            {
                var exePathKey = Services.GetMd5(Services.GetExePath());
                var rebootas = (Args ?? []).Any(t => t == "rebootas");
                ProgramStarted = new EventWaitHandle(false, EventResetMode.AutoReset, exePathKey, out var bCreatedNew);
                if (!rebootas && !bCreatedNew)
                {
                    ProgramStarted.Set();
                    return false;
                }
            }
            else
            {
                _ = new Mutex(true, "Wauncher", out var bOnlyOneInstance);
                if (!bOnlyOneInstance)
                {
                    return false;
                }
            }
            return true;
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
