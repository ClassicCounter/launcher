using System;
using System.IO;
using System.Threading.Tasks;

namespace Wauncher.Utils
{
    public static class ErrorLogger
    {
        private const long MaxLogFileBytes = 1024 * 1024;
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassicCounter",
            "Wauncher",
            "logs",
            "WauncherLog.txt"
        );
        private static readonly string ArchiveLogFilePath = Path.Combine(
            Path.GetDirectoryName(LogFilePath) ?? ".",
            "WauncherLog.1.txt"
        );

        private static readonly object _lock = new object();

        public static void LogError(string componentName, Exception exception, string? additionalContext = null)
        {
            try
            {
                WriteLogEntry(FormatLogEntry(componentName, exception, additionalContext));
            }
            catch
            {
                // If logging fails, don't throw an exception to avoid infinite loops
            }
        }

        public static void LogError(string componentName, string errorMessage, string? additionalContext = null)
        {
            try
            {
                WriteLogEntry(FormatLogEntry(componentName, errorMessage, additionalContext));
            }
            catch
            {
                // If logging fails, don't throw an exception to avoid infinite loops
            }
        }

        public static async Task LogErrorAsync(string componentName, Exception exception, string? additionalContext = null)
        {
            try
            {
                await Task.Run(() => WriteLogEntry(FormatLogEntry(componentName, exception, additionalContext)));
            }
            catch
            {
                // If logging fails, don't throw an exception to avoid infinite loops
            }
        }

        public static async Task LogErrorAsync(string componentName, string errorMessage, string? additionalContext = null)
        {
            try
            {
                await Task.Run(() => WriteLogEntry(FormatLogEntry(componentName, errorMessage, additionalContext)));
            }
            catch
            {
                // If logging fails, don't throw an exception to avoid infinite loops
            }
        }

        private static void WriteLogEntry(string logEntry)
        {
            lock (_lock)
            {
                string logDirectory = Path.GetDirectoryName(LogFilePath) ?? ".";
                Directory.CreateDirectory(logDirectory);

                RotateLogIfNeeded(logEntry.Length);
                File.AppendAllText(LogFilePath, logEntry);
            }
        }

        private static void RotateLogIfNeeded(int incomingLength)
        {
            if (!File.Exists(LogFilePath))
                return;

            var fileInfo = new FileInfo(LogFilePath);
            if (fileInfo.Length + incomingLength <= MaxLogFileBytes)
                return;

            if (File.Exists(ArchiveLogFilePath))
                File.Delete(ArchiveLogFilePath);

            File.Move(LogFilePath, ArchiveLogFilePath);
        }

        private static string FormatLogEntry(string componentName, Exception exception, string? additionalContext)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] ERROR in {componentName}:\n";
            logEntry += $"Message: {exception.Message}\n";
            
            if (!string.IsNullOrEmpty(additionalContext))
            {
                logEntry += $"Context: {additionalContext}\n";
            }
            
            logEntry += $"Exception Type: {exception.GetType().Name}\n";
            logEntry += $"Stack Trace: {exception.StackTrace}\n";
            logEntry += new string('-', 80) + "\n";
            
            return logEntry;
        }

        private static string FormatLogEntry(string componentName, string errorMessage, string? additionalContext)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] ERROR in {componentName}:\n";
            logEntry += $"Message: {errorMessage}\n";
            
            if (!string.IsNullOrEmpty(additionalContext))
            {
                logEntry += $"Context: {additionalContext}\n";
            }
            
            logEntry += new string('-', 80) + "\n";
            
            return logEntry;
        }
    }
}
