using System.Runtime.InteropServices;

namespace Launcher.Utils
{
    public static class ConsoleManager
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private const int SW_HIDE = 0;
        private const uint MB_ICONERROR = 0x00000010;

        private static IntPtr ConsoleHandle = GetConsoleWindow();

        public static void HideConsole()
        {
            ShowWindow(ConsoleHandle, SW_HIDE);
        }

        public static void ShowError(string message)
        {
            if (OperatingSystem.IsWindows())
            {
                MessageBox(IntPtr.Zero, message, "ClassicCounter Error", MB_ICONERROR);
            }
        }
    }
}

