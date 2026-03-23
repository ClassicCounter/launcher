using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Wauncher.Utils
{
    public static class MemoryManager
    {
        private static Timer? _cleanupTimer;
        private const int CleanupIntervalMinutes = 5;
        private static readonly object _timerLock = new();

        public static void CleanupMemory(object? state = null)
        {
            try
            {
                // Force garbage collection for large objects
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Optional: Trim process working set on Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        public static void ForceCleanup()
        {
            CleanupMemory();
        }

        public static void StartBackgroundCleanup()
        {
            lock (_timerLock)
            {
                _cleanupTimer ??= new Timer(
                    CleanupMemory,
                    null,
                    TimeSpan.FromMinutes(CleanupIntervalMinutes),
                    TimeSpan.FromMinutes(CleanupIntervalMinutes));
            }
        }

        public static void StopBackgroundCleanup()
        {
            lock (_timerLock)
            {
                _cleanupTimer?.Dispose();
                _cleanupTimer = null;
            }
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, int dwMinimumWorkingSetSize, int dwMaximumWorkingSetSize);
    }
}
