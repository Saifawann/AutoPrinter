using System;
using System.IO;

namespace AutoPrinter.Helpers
{
    public static class PdfHelper
    {
        public static string SavePdf(byte[] pdfBytes, string folderPath, string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    Logger.Write("No folder path configured.");
                    return string.Empty;
                }

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string fullPath = Path.Combine(folderPath, fileName);

                File.WriteAllBytes(fullPath, pdfBytes);

                Logger.Write($"PDF saved: {fullPath}");
                return fullPath;
            }
            catch (Exception ex)
            {
                Logger.Write($"Error saving PDF: {ex.Message}");
                return string.Empty;
            }
        }

    }
}
