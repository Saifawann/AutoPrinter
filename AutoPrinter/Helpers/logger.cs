using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AutoPrinter.Helpers
{
    public static class Logger
    {
        private static readonly string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AutoPrinter",
            "logs.txt"
        );

        private static readonly object lockObject = new object();
        private static readonly TimeSpan LOG_RETENTION_PERIOD = TimeSpan.FromHours(2);
        private static DateTime lastCleanupTime = DateTime.MinValue;
        private static readonly TimeSpan CLEANUP_INTERVAL = TimeSpan.FromMinutes(5); // Run cleanup every 5 minutes

        public static event Action<string>? LogWritten;

        static Logger()
        {
            string? folder = Path.GetDirectoryName(logPath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder!);
            }

            // Perform initial cleanup on startup
            CleanupOldLogs();
        }

        public static void Write(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            // Thread-safe file writing
            lock (lockObject)
            {
                try
                {
                    // Check if it's time to cleanup old logs
                    if (DateTime.Now - lastCleanupTime > CLEANUP_INTERVAL)
                    {
                        CleanupOldLogs();
                    }

                    File.AppendAllText(logPath, logMessage + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // Handle file access errors gracefully
                    System.Diagnostics.Debug.WriteLine($"Logger error: {ex.Message}");
                }
            }

            LogWritten?.Invoke(logMessage);
        }

        private static void CleanupOldLogs()
        {
            try
            {
                if (!File.Exists(logPath))
                    return;

                var cutoffTime = DateTime.Now - LOG_RETENTION_PERIOD;
                var allLines = File.ReadAllLines(logPath);
                var recentLines = new List<string>();

                foreach (var line in allLines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Try to parse the timestamp from the log line
                    var timestamp = ExtractTimestamp(line);
                    if (timestamp.HasValue && timestamp.Value >= cutoffTime)
                    {
                        recentLines.Add(line);
                    }
                    else if (!timestamp.HasValue)
                    {
                        // If we can't parse the timestamp, keep the line if we're keeping recent logs
                        // This handles edge cases like header lines or malformed entries
                        if (recentLines.Count > 0)
                        {
                            recentLines.Add(line);
                        }
                    }
                }

                // Rewrite the file with only recent logs
                File.WriteAllLines(logPath, recentLines);
                lastCleanupTime = DateTime.Now;

                // Log the cleanup action (optional - you might want to skip this to avoid log spam)
                if (allLines.Length != recentLines.Count)
                {
                    System.Diagnostics.Debug.WriteLine($"Logger cleanup: Removed {allLines.Length - recentLines.Count} old log entries");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logger cleanup error: {ex.Message}");
            }
        }

        private static DateTime? ExtractTimestamp(string logLine)
        {
            try
            {
                // Look for timestamp pattern [yyyy-MM-dd HH:mm:ss]
                int startIndex = logLine.IndexOf('[');
                int endIndex = logLine.IndexOf(']');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string timestampStr = logLine.Substring(startIndex + 1, endIndex - startIndex - 1);
                    if (DateTime.TryParse(timestampStr, out DateTime timestamp))
                    {
                        return timestamp;
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return null;
        }

        public static string[] ReadAll()
        {
            lock (lockObject)
            {
                try
                {
                    // Perform cleanup before reading to ensure we only return recent logs
                    if (DateTime.Now - lastCleanupTime > CLEANUP_INTERVAL)
                    {
                        CleanupOldLogs();
                    }

                    if (File.Exists(logPath))
                        return File.ReadAllLines(logPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Logger read error: {ex.Message}");
                }
            }

            return Array.Empty<string>();
        }

        public static string[] ReadRecent(int lineCount = 100)
        {
            var allLines = ReadAll();
            if (allLines.Length <= lineCount)
                return allLines;

            var recentLines = new string[lineCount];
            Array.Copy(allLines, allLines.Length - lineCount, recentLines, 0, lineCount);
            return recentLines;
        }

    }
}