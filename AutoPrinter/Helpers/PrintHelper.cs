using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoPrinter.Helpers
{
    public static class PrintHelper
    {
        // Keep track of temp files for cleanup
        private static readonly List<string> tempFiles = new List<string>();
        private static Timer cleanupTimer;

        static PrintHelper()
        {
            // Setup periodic cleanup of old temp files
            cleanupTimer = new Timer(CleanupOldTempFiles, null,
                TimeSpan.FromMinutes(5), // Initial delay
                TimeSpan.FromMinutes(30)); // Repeat every 30 minutes
        }

        public static void PrintPdf(byte[] fileBytes, string printerName)
        {
            string tempPath = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    Logger.Write("No printer selected.");
                    return;
                }

                // Detect actual file type
                string fileType = DetectFileType(fileBytes);
                string extension = GetFileExtension(fileType);

                if (fileType != "pdf" && fileType != "unknown")
                {
                    Logger.Write($"File is {fileType.ToUpper()}, will print as-is.");
                }

                // Create temp file with timestamp to avoid collisions
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssffff");
                tempPath = Path.Combine(Path.GetTempPath(), $"print_{timestamp}_{Guid.NewGuid()}.{extension}");

                File.WriteAllBytes(tempPath, fileBytes);
                tempFiles.Add(tempPath);

                // Try multiple print methods
                bool printSuccess = TryPrintWithVerb(tempPath, printerName);

                if (!printSuccess)
                {
                    // Fallback to command line printing
                    printSuccess = TryPrintWithCommandLine(tempPath, printerName);
                }

                if (!printSuccess)
                {
                    Logger.Write($"Failed to initiate print job for {printerName}");
                }

                // Schedule cleanup after print job should be done
                Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ =>
                {
                    TryDeleteTempFile(tempPath);
                });
            }
            catch (Exception ex)
            {
                Logger.Write($"Error while printing: {ex.Message}");

                // Try to cleanup on error
                if (!string.IsNullOrEmpty(tempPath))
                {
                    TryDeleteTempFile(tempPath);
                }
            }
        }

        private static string DetectFileType(byte[] data)
        {
            if (data == null || data.Length < 4)
                return "unknown";

            // Check for PDF signature (%PDF)
            if (data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46)
                return "pdf";

            // Check for PNG signature
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return "png";

            // Check for JPEG signature
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return "jpeg";

            return "unknown";
        }

        private static string GetFileExtension(string fileType)
        {
            switch (fileType)
            {
                case "pdf":
                    return "pdf";
                case "png":
                    return "png";
                case "jpeg":
                    return "jpg";
                default:
                    return "pdf"; // Default to PDF for unknown types
            }
        }

        private static bool TryPrintWithVerb(string filePath, string printerName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    Verb = "printto",
                    Arguments = $"\"{printerName}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true,
                    ErrorDialog = false
                };

                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        // Wait for process to start printing (not complete)
                        bool exited = process.WaitForExit(10000); // 10 seconds timeout

                        if (!exited)
                        {
                            Logger.Write("Print process is taking longer than expected, continuing...");
                        }

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Verb print method failed: {ex.Message}");
            }

            return false;
        }

        private static bool TryPrintWithCommandLine(string filePath, string printerName)
        {
            try
            {
                // Alternative method using Windows print command
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c print /D:\"{printerName}\" \"{filePath}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        process.WaitForExit(5000);
                        return process.ExitCode == 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Command line print method failed: {ex.Message}");
            }

            return false;
        }

        private static void TryDeleteTempFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // Try multiple times in case file is locked
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            File.Delete(filePath);
                            tempFiles.Remove(filePath);
                            break;
                        }
                        catch
                        {
                            if (i < 2)
                                Thread.Sleep(1000); // Wait 1 second before retry
                        }
                    }
                }
            }
            catch
            {
                // Silent fail - will be cleaned up later
            }
        }

        private static void CleanupOldTempFiles(object state)
        {
            try
            {
                var tempDir = Path.GetTempPath();
                // Clean up any print files (PDF, PNG, JPG)
                var patterns = new[] { "print_*.pdf", "print_*.png", "print_*.jpg" };

                foreach (var pattern in patterns)
                {
                    var oldFiles = Directory.GetFiles(tempDir, pattern)
                        .Where(f =>
                        {
                            try
                            {
                                var fileInfo = new FileInfo(f);
                                return fileInfo.CreationTime < DateTime.Now.AddHours(-1); // Older than 1 hour
                            }
                            catch
                            {
                                return false;
                            }
                        });

                    foreach (var file in oldFiles)
                    {
                        TryDeleteTempFile(file);
                    }
                }

                // Clean up our tracked files
                var filesToRemove = tempFiles.Where(f => !File.Exists(f)).ToList();
                foreach (var file in filesToRemove)
                {
                    tempFiles.Remove(file);
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error during temp file cleanup: {ex.Message}");
            }
        }

        // Call this when application closes
        public static void Cleanup()
        {
            cleanupTimer?.Dispose();

            // Try to clean up all temp files
            foreach (var file in tempFiles.ToList())
            {
                TryDeleteTempFile(file);
            }
        }
    }
}