using System;
using System.IO;
using System.Threading.Tasks;

namespace Wauncher.Utils
{
    public static class ErrorLogger
    {
        private static readonly string LogFilePath = Path.Combine(
            Path.GetDirectoryName(System.Environment.ProcessPath) ?? ".", 
            "WauncherLog.txt"
        );
        
        private static readonly object _lock = new object();

        public static void LogError(string componentName, Exception exception, string? additionalContext = null)
        {
            try
            {
                var logEntry = FormatLogEntry(componentName, exception, additionalContext);
                
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath, logEntry);
                }
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
                var logEntry = FormatLogEntry(componentName, errorMessage, additionalContext);
                
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath, logEntry);
                }
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
                var logEntry = FormatLogEntry(componentName, exception, additionalContext);
                
                await Task.Run(() =>
                {
                    lock (_lock)
                    {
                        File.AppendAllText(LogFilePath, logEntry);
                    }
                });
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
                var logEntry = FormatLogEntry(componentName, errorMessage, additionalContext);
                
                await Task.Run(() =>
                {
                    lock (_lock)
                    {
                        File.AppendAllText(LogFilePath, logEntry);
                    }
                });
            }
            catch
            {
                // If logging fails, don't throw an exception to avoid infinite loops
            }
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
