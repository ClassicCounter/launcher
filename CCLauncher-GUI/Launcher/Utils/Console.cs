/*
 *  Utils/Console.cs
 */

using System;
using System.Runtime.InteropServices;

namespace Launcher.Utils
{
    public static class ConsoleManager
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private static IntPtr ConsoleHandle;

        public static void InitializeDebugConsole()
        {
            if (Debug.Enabled())
            {
                AllocConsole();
                ConsoleHandle = GetConsoleWindow();
                ShowWindow(ConsoleHandle, SW_SHOW);
                Console.Title = "ClassicCounter Launcher - Debug Console";
                Terminal.Init();
            }
            else
            {
                HideConsole();
            }
        }

        public static void HideConsole()
        {
            ConsoleHandle = GetConsoleWindow();
            if (ConsoleHandle != IntPtr.Zero)
            {
                ShowWindow(ConsoleHandle, SW_HIDE);
                FreeConsole();
            }
        }
    }
}