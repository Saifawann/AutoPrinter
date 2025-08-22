using System;
using System.IO;
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

        public static event Action<string>? LogWritten;

        static Logger()
        {
            string? folder = Path.GetDirectoryName(logPath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder!);
            }
        }

        public static void Write(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            // Thread-safe file writing
            lock (lockObject)
            {
                try
                {
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

        public static string[] ReadAll()
        {
            lock (lockObject)
            {
                try
                {
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

        // Optional: Add method to get recent logs only
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
