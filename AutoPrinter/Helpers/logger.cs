using System;
using System.IO;

namespace AutoPrinter.Helpers
{
    public static class Logger
    {
        private static readonly string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AutoPrinter",
            "logs.txt"
        );

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
            File.AppendAllText(logPath, logMessage + Environment.NewLine);

            LogWritten?.Invoke(logMessage);

        }

        public static string[] ReadAll()
        {
            if (File.Exists(logPath))
                return File.ReadAllLines(logPath);
            else
                return Array.Empty<string>();
        }
    }
}
