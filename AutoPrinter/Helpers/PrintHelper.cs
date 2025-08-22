using System;
using System.Diagnostics;
using System.IO;

namespace AutoPrinter.Helpers
{
    public static class PrintHelper
    {
        public static void PrintPdf(byte[] pdfBytes, string printerName)
        {
            try
            {
                // Save temp file
                string tempPath = Path.Combine(Path.GetTempPath(), $"print_{Guid.NewGuid()}.pdf");
                File.WriteAllBytes(tempPath, pdfBytes);

                // Prepare process info
                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    Verb = "printto", // "print" prints to default, "printto" allows custom printer
                    Arguments = $"\"{printerName}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        process.WaitForExit(10_000); // wait up to 10s for print command to fire
                    }
                }

                // Cleanup (optional – keep if debugging)
                // File.Delete(tempPath);

            }
            catch (Exception ex)
            {
                Logger.Write($"Error while printing PDF: {ex.Message}");
            }
        }
    }
}
