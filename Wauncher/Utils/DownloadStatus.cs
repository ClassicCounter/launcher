using System;

namespace Wauncher.Utils
{
    /// <summary>
    /// Provides reusable download progress visualization components
    /// </summary>
    public class DownloadStatus
    {
        private int _dotCount = 0;
        private DateTime _lastDotUpdate = DateTime.Now;

        /// <summary>
        /// Gets animated dots for loading indicators
        /// </summary>
        /// <returns>String with 0-3 dots based on timing</returns>
        public string GetDots()
        {
            if ((DateTime.Now - _lastDotUpdate).TotalMilliseconds > 500)
            {
                _dotCount = (_dotCount + 1) % 4;
                _lastDotUpdate = DateTime.Now;
            }
            return "...".Substring(0, _dotCount);
        }

        /// <summary>
        /// Gets a text-based progress bar
        /// </summary>
        /// <param name="percentage">Progress percentage (0-100)</param>
        /// <param name="blocks">Number of blocks in the bar (default: 16)</param>
        /// <returns>String representation of progress bar</returns>
        public string GetProgressBar(double percentage, int blocks = 16)
        {
            if (percentage < 0) percentage = 0;
            if (percentage > 100) percentage = 100;

            int level = (int)(percentage / (100.0 / (blocks * 3)));
            string bar = "";

            for (int i = 0; i < blocks; i++)
            {
                int blockLevel = Math.Min(3, Math.Max(0, level - (i * 3)));
                bar += blockLevel switch
                {
                    0 => "¦",
                    1 => "¦",
                    2 => "¦",
                    3 => "¦",
                    _ => "¦"
                };
            }
            return bar;
        }

        /// <summary>
        /// Formats a complete download status line
        /// </summary>
        /// <param name="status">Status text (e.g., "Downloading", "Extracting")</param>
        /// <param name="filename">File being processed</param>
        /// <param name="progressPercentage">Current progress percentage</param>
        /// <param name="speedMBps">Download speed in MB/s</param>
        /// <param name="completedFiles">Number of completed files</param>
        /// <param name="totalFiles">Total number of files</param>
        /// <returns>Formatted status string</returns>
        public string FormatStatus(string status, string filename, double progressPercentage, 
            double speedMBps, int completedFiles, int totalFiles)
        {
            var progressText = $"{((float)completedFiles / totalFiles * 100):F1}% ({completedFiles}/{totalFiles})";
            return $"{status} {filename}{GetDots().PadRight(3)} [gray]|[/] {progressText} [gray]|[/] {GetProgressBar(progressPercentage)} {progressPercentage:F1}% [gray]|[/] {speedMBps:F1} MB/s";
        }

        /// <summary>
        /// Resets the animation state
        /// </summary>
        public void Reset()
        {
            _dotCount = 0;
            _lastDotUpdate = DateTime.Now;
        }
    }
}
