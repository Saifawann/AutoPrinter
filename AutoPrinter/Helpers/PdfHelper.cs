using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.Drawing;
using System.Drawing.Imaging;

namespace AutoPrinter.Helpers
{
    public static class PdfHelper
    {
        private static readonly HashSet<string> processedFiles = new HashSet<string>();
        private static readonly object lockObject = new object();

        // File signature constants
        private static readonly Dictionary<string, byte[]> FileSignatures = new Dictionary<string, byte[]>
        {
            { "pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 } },      // %PDF
            { "png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },      // PNG
            { "jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },           // JPEG
            { "gif", new byte[] { 0x47, 0x49, 0x46 } },            // GIF
            { "bmp", new byte[] { 0x42, 0x4D } },                  // BM
            { "tiff_le", new byte[] { 0x49, 0x49 } },              // II (Little Endian)
            { "tiff_be", new byte[] { 0x4D, 0x4D } }               // MM (Big Endian)
        };

        public static string SavePdf(byte[] fileBytes, string folderPath, string fileName)
        {
            // Input validation
            if (fileBytes == null || fileBytes.Length == 0)
            {
                Logger.Write($"Error: Empty or null file data for {fileName}");
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                Logger.Write("Error: No folder path configured.");
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                Logger.Write("Error: No filename provided.");
                return string.Empty;
            }

            try
            {
                // Ensure directory exists
                EnsureDirectoryExists(folderPath);

                // Prepare filename with .pdf extension
                string fullPath = GetUniqueFilePath(folderPath, fileName, fileBytes.Length);

                if (string.IsNullOrEmpty(fullPath))
                {
                    return string.Empty; // File already exists with same size
                }

                // Detect file type and process accordingly
                string actualType = DetectFileType(fileBytes);
                bool saveSuccess = false;

                switch (actualType)
                {
                    case "pdf":
                        saveSuccess = SaveAsPdf(fileBytes, fullPath);
                        break;

                    case "png":
                    case "jpeg":
                    case "gif":
                    case "bmp":
                    case "tiff":
                        saveSuccess = ConvertImageToPdf(fileBytes, fullPath, actualType);
                        break;

                    default:
                        // Try to save as-is if unknown type (might still be valid PDF)
                        Logger.Write($"Warning: Unknown file type for {fileName}, attempting to save as PDF");
                        saveSuccess = SaveAsPdf(fileBytes, fullPath);
                        break;
                }

                if (saveSuccess)
                {
                    // Verify file was created and get final size
                    FileInfo savedFile = new FileInfo(fullPath);
                    if (savedFile.Exists)
                    {
                        Logger.Write($"✓ File saved: {Path.GetFileName(fullPath)} " +
                                   $"({savedFile.Length:N0} bytes) [Original type: {actualType}]");

                        // Thread-safe addition to processed files
                        lock (lockObject)
                        {
                            processedFiles.Add(Path.GetFileName(fullPath));
                        }

                        return fullPath;
                    }
                }

                Logger.Write($"Error: Failed to save {fileName}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Write($"Error saving file {fileName}: {ex.Message}");
                Logger.Write($"Stack trace: {ex.StackTrace}");
                return string.Empty;
            }
        }

        private static void EnsureDirectoryExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Logger.Write($"Created directory: {folderPath}");
            }
        }

        // Add this method to your existing PdfHelper class:

        public static byte[] ConvertToPdfBytes(byte[] fileBytes)
        {
            // If already PDF, return as-is
            string fileType = DetectFileType(fileBytes);

            if (fileType == "pdf")
            {
                return fileBytes;
            }

            // If it's an image, convert to PDF
            if (fileType == "png" || fileType == "jpeg" || fileType == "gif" || fileType == "bmp" || fileType == "tiff")
            {
                using (var imageStream = new MemoryStream(fileBytes))
                using (var img = Image.FromStream(imageStream))
                {
                    var doc = new PdfDocument();
                    var page = doc.AddPage();

                    // Standard A4 dimensions
                    double maxWidth = 595;
                    double maxHeight = 842;

                    double imgWidth = img.Width;
                    double imgHeight = img.Height;

                    // Calculate scaling
                    double scaleX = maxWidth / imgWidth;
                    double scaleY = maxHeight / imgHeight;
                    double scale = Math.Min(scaleX, scaleY);

                    if (scale > 1)
                    {
                        page.Width = imgWidth;
                        page.Height = imgHeight;
                    }
                    else
                    {
                        page.Width = maxWidth;
                        page.Height = maxHeight;
                        imgWidth *= scale;
                        imgHeight *= scale;
                    }

                    using (var gfx = XGraphics.FromPdfPage(page))
                    using (var xImgStream = new MemoryStream(fileBytes))
                    using (var xImg = XImage.FromStream(xImgStream))
                    {
                        double x = (page.Width - imgWidth) / 2;
                        double y = (page.Height - imgHeight) / 2;

                        gfx.DrawImage(xImg, x, y, imgWidth, imgHeight);
                    }

                    using (var pdfStream = new MemoryStream())
                    {
                        doc.Save(pdfStream);
                        Logger.Write($"Converted {fileType} to PDF for printing");
                        return pdfStream.ToArray();
                    }
                }
            }

            // Unknown type, return as-is and hope for the best
            Logger.Write($"Warning: Unknown file type, returning original bytes");
            return fileBytes;
        }

        private static string GetUniqueFilePath(string folderPath, string fileName, int fileSize)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string pdfFileName = $"{nameWithoutExt}.pdf";
            string fullPath = Path.Combine(folderPath, pdfFileName);

            if (File.Exists(fullPath))
            {
                FileInfo existingFile = new FileInfo(fullPath);

                // Check if it's the same file by size comparison
                if (existingFile.Length == fileSize)
                {
                    Logger.Write($"File already exists with same size, skipping: {pdfFileName}");
                    return string.Empty;
                }

                // Generate unique filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                pdfFileName = $"{nameWithoutExt}_{timestamp}.pdf";
                fullPath = Path.Combine(folderPath, pdfFileName);
                Logger.Write($"File exists with different size, will save as: {pdfFileName}");
            }

            return fullPath;
        }

        private static bool SaveAsPdf(byte[] fileBytes, string fullPath)
        {
            try
            {
                File.WriteAllBytes(fullPath, fileBytes);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Write($"Error writing PDF file: {ex.Message}");
                return false;
            }
        }

        private static bool ConvertImageToPdf(byte[] imageBytes, string fullPath, string imageType)
        {
            MemoryStream imageStream = null;
            Image img = null;
            PdfDocument doc = null;
            XGraphics gfx = null;
            XImage xImg = null;

            try
            {
                // Load image from bytes
                imageStream = new MemoryStream(imageBytes);
                img = Image.FromStream(imageStream);

                // Create PDF document
                doc = new PdfDocument();
                doc.Info.Title = Path.GetFileNameWithoutExtension(fullPath);
                doc.Info.Creator = "AutoPrinter PDF Converter";

                // Calculate page dimensions maintaining aspect ratio
                PdfPage page = doc.AddPage();

                // Standard A4 dimensions in points (1 inch = 72 points)
                double maxWidth = 595;  // A4 width in points
                double maxHeight = 842; // A4 height in points

                double imgWidth = img.Width;
                double imgHeight = img.Height;

                // Calculate scaling to fit page
                double scaleX = maxWidth / imgWidth;
                double scaleY = maxHeight / imgHeight;
                double scale = Math.Min(scaleX, scaleY);

                // Set page size to fit image (or use A4 if image is smaller)
                if (scale > 1)
                {
                    // Image is smaller than A4, use actual size
                    page.Width = imgWidth;
                    page.Height = imgHeight;
                }
                else
                {
                    // Scale image to fit A4
                    page.Width = maxWidth;
                    page.Height = maxHeight;
                    imgWidth *= scale;
                    imgHeight *= scale;
                }

                // Draw image on PDF page
                gfx = XGraphics.FromPdfPage(page);

                // Create XImage from memory stream
                using (var xImgStream = new MemoryStream(imageBytes))
                {
                    xImg = XImage.FromStream(xImgStream);

                    // Center image on page if it's smaller than page
                    double x = (page.Width - imgWidth) / 2;
                    double y = (page.Height - imgHeight) / 2;

                    gfx.DrawImage(xImg, x, y, imgWidth, imgHeight);
                }

                // Save PDF
                doc.Save(fullPath);
                Logger.Write($"Converted {imageType} image to PDF successfully");
                return true;
            }
            catch (OutOfMemoryException)
            {
                Logger.Write($"Error: Image file is too large or corrupted");
                return false;
            }
            catch (ArgumentException ex)
            {
                Logger.Write($"Error: Invalid image format - {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Write($"Error converting image to PDF: {ex.Message}");
                return false;
            }
            finally
            {
                // Cleanup resources
                xImg?.Dispose();
                gfx?.Dispose();
                doc?.Dispose();
                img?.Dispose();
                imageStream?.Dispose();
            }
        }

        private static string DetectFileType(byte[] data)
        {
            if (data == null || data.Length < 4)
                return "unknown";

            // Check PDF
            if (data.Take(4).SequenceEqual(FileSignatures["pdf"]))
                return "pdf";

            // Check PNG
            if (data.Take(4).SequenceEqual(FileSignatures["png"]))
                return "png";

            // Check JPEG
            if (data.Take(3).SequenceEqual(FileSignatures["jpeg"]))
                return "jpeg";

            // Check GIF
            if (data.Take(3).SequenceEqual(FileSignatures["gif"]))
                return "gif";

            // Check BMP
            if (data.Take(2).SequenceEqual(FileSignatures["bmp"]))
                return "bmp";

            // Check TIFF
            if (data.Take(2).SequenceEqual(FileSignatures["tiff_le"]) ||
                data.Take(2).SequenceEqual(FileSignatures["tiff_be"]))
                return "tiff";

            return "unknown";
        }

        public static bool IsAlreadyProcessed(string fileName)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string pdfFileName = $"{nameWithoutExt}.pdf";

            lock (lockObject)
            {
                return processedFiles.Any(f =>
                    f.Equals(pdfFileName, StringComparison.OrdinalIgnoreCase) ||
                    f.StartsWith($"{nameWithoutExt}_", StringComparison.OrdinalIgnoreCase));
            }
        }

        public static void ClearProcessedFiles()
        {
            lock (lockObject)
            {
                processedFiles.Clear();
                Logger.Write("Cleared processed files cache");
            }
        }

        public static int GetProcessedFileCount()
        {
            lock (lockObject)
            {
                return processedFiles.Count;
            }
        }

        // Helper method to validate if file appears to be valid
        public static bool ValidateFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                FileInfo file = new FileInfo(filePath);

                // Check if file has content
                if (file.Length == 0)
                {
                    Logger.Write($"Warning: File {filePath} is empty");
                    return false;
                }

                // Try to open file to ensure it's not locked
                using (FileStream fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // File can be opened
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"File validation failed for {filePath}: {ex.Message}");
                return false;
            }
        }
    }
}