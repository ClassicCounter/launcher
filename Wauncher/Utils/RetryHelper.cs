using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wauncher.Utils
{
    /// <summary>
    /// Provides retry mechanisms for network and file operations
    /// </summary>
    public static class RetryHelper
    {
        /// <summary>
        /// Executes an operation with retry logic
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <param name="delayMs">Delay between retries in milliseconds</param>
        /// <param name="exponentialBackoff">Whether to use exponential backoff</param>
        /// <param name="retryCondition">Condition to determine if retry should occur</param>
        /// <returns>Result of the operation</returns>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = 3,
            int delayMs = 1000,
            bool exponentialBackoff = true,
            Func<Exception, bool>? retryCondition = null)
        {
            if (retryCondition == null)
                retryCondition = ex => IsRetryableException(ex);

            int attempt = 0;
            Exception? lastException = null;

            while (attempt <= maxRetries)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempt++;

                    if (attempt > maxRetries || !retryCondition(ex))
                    {
                        Terminal.Error($"Operation failed after {attempt} attempts: {ex.Message}");
                        throw;
                    }

                    int currentDelay = exponentialBackoff ? delayMs * (int)Math.Pow(2, attempt - 1) : delayMs;
                    Terminal.Warning($"Operation failed (attempt {attempt}/{maxRetries + 1}), retrying in {currentDelay}ms: {ex.Message}");
                    
                    await Task.Delay(currentDelay);
                }
            }

            throw lastException ?? new InvalidOperationException("Operation failed");
        }

        /// <summary>
        /// Executes a void operation with retry logic
        /// </summary>
        /// <param name="operation">Operation to execute</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <param name="delayMs">Delay between retries in milliseconds</param>
        /// <param name="exponentialBackoff">Whether to use exponential backoff</param>
        /// <param name="retryCondition">Condition to determine if retry should occur</param>
        public static async Task ExecuteWithRetryAsync(
            Func<Task> operation,
            int maxRetries = 3,
            int delayMs = 1000,
            bool exponentialBackoff = true,
            Func<Exception, bool>? retryCondition = null)
        {
            if (retryCondition == null)
                retryCondition = ex => IsRetryableException(ex);

            int attempt = 0;
            Exception? lastException = null;

            while (attempt <= maxRetries)
            {
                try
                {
                    await operation();
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempt++;

                    if (attempt > maxRetries || !retryCondition(ex))
                    {
                        Terminal.Error($"Operation failed after {attempt} attempts: {ex.Message}");
                        throw;
                    }

                    int currentDelay = exponentialBackoff ? delayMs * (int)Math.Pow(2, attempt - 1) : delayMs;
                    Terminal.Warning($"Operation failed (attempt {attempt}/{maxRetries + 1}), retrying in {currentDelay}ms: {ex.Message}");
                    
                    await Task.Delay(currentDelay);
                }
            }

            throw lastException ?? new InvalidOperationException("Operation failed");
        }

        /// <summary>
        /// Determines if an exception is retryable
        /// </summary>
        /// <param name="ex">Exception to check</param>
        /// <returns>True if the exception is retryable</returns>
        private static bool IsRetryableException(Exception ex)
        {
            // Network-related exceptions
            if (ex is System.Net.Http.HttpRequestException ||
                ex is System.Net.WebException ||
                ex is System.TimeoutException ||
                ex is System.IO.IOException)
            {
                return true;
            }

            // Check for specific error messages
            string message = ex.Message.ToLowerInvariant();
            return message.Contains("timeout") ||
                   message.Contains("connection") ||
                   message.Contains("network") ||
                   message.Contains("temporary") ||
                   message.Contains("retry");
        }
    }
}
