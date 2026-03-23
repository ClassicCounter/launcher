using System;
using System.IO;
using System.Threading.Tasks;

namespace Wauncher.Utils
{
    /// <summary>
    /// Provides safe file operations with proper error handling and recovery
    /// </summary>
    public static class FileOperationHelper
    {
        /// <summary>
        /// Safely deletes a file with retry logic
        /// </summary>
        /// <param name="filePath">Path to the file to delete</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <returns>True if deletion was successful</returns>
        public static async Task<bool> SafeDeleteFileAsync(string filePath, int maxRetries = 3)
        {
            if (!File.Exists(filePath))
                return true;

            return await RetryHelper.ExecuteWithRetryAsync(() =>
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    if (Debug.Enabled())
                        Terminal.Debug($"Successfully deleted file: {filePath}");
                }
                return Task.FromResult(true);
            }, maxRetries, 500);
        }

        /// <summary>
        /// Safely deletes a directory with retry logic
        /// </summary>
        /// <param name="directoryPath">Path to the directory to delete</param>
        /// <param name="recursive">Whether to delete recursively</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <returns>True if deletion was successful</returns>
        public static async Task<bool> SafeDeleteDirectoryAsync(string directoryPath, bool recursive = true, int maxRetries = 3)
        {
            if (!Directory.Exists(directoryPath))
                return true;

            return await RetryHelper.ExecuteWithRetryAsync(() =>
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive);
                    if (Debug.Enabled())
                        Terminal.Debug($"Successfully deleted directory: {directoryPath}");
                }
                return Task.FromResult(true);
            }, maxRetries, 500);
        }

        /// <summary>
        /// Safely moves a file with retry logic
        /// </summary>
        /// <param name="sourcePath">Source file path</param>
        /// <param name="destinationPath">Destination file path</param>
        /// <param name="overwrite">Whether to overwrite existing files</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <returns>True if move was successful</returns>
        public static async Task<bool> SafeMoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false, int maxRetries = 3)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");

            return await RetryHelper.ExecuteWithRetryAsync(() =>
            {
                // Ensure destination directory exists
                string destinationDir = Path.GetDirectoryName(destinationPath) ?? throw new InvalidOperationException($"Invalid destination path: {destinationPath}");
                if (!Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                File.Move(sourcePath, destinationPath, overwrite);
                if (Debug.Enabled())
                    Terminal.Debug($"Successfully moved file from {sourcePath} to {destinationPath}");
                
                return Task.FromResult(true);
            }, maxRetries, 500);
        }

        /// <summary>
        /// Safely creates a directory if it doesn't exist
        /// </summary>
        /// <param name="directoryPath">Directory path to create</param>
        /// <returns>True if directory exists or was created successfully</returns>
        public static Task<bool> SafeCreateDirectoryAsync(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    if (Debug.Enabled())
                        Terminal.Debug($"Created directory: {directoryPath}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Terminal.Error($"Failed to create directory {directoryPath}: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Checks if a file is locked by another process
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <returns>True if the file is locked</returns>
        public static bool IsFileLocked(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Waits for a file to become unlocked
        /// </summary>
        /// <param name="filePath">Path to the file to wait for</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
        /// <returns>True if the file became unlocked, false if timeout occurred</returns>
        public static async Task<bool> WaitForFileUnlockAsync(string filePath, int timeoutMs = 30000)
        {
            if (!File.Exists(filePath))
                return true;

            var startTime = DateTime.Now;
            while (IsFileLocked(filePath))
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                {
                    Terminal.Warning($"Timeout waiting for file to unlock: {filePath}");
                    return false;
                }

                await Task.Delay(100);
            }

            return true;
        }
    }
}
