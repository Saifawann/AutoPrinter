using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoPrinter.Helpers
{
    public static class Base64Helper
    {
        /// <summary>
        /// Safely converts base64 string to PDF bytes with validation
        /// </summary>
        public static byte[]? ConvertBase64ToPdf(string base64String, string fileName)
        {
            try
            {
                // Step 1: Clean the base64 string (remove whitespace, newlines, etc.)
                string cleanBase64 = CleanBase64String(base64String);

                // Step 2: Validate base64 format
                if (!IsValidBase64(cleanBase64))
                {
                    Logger.Write($"Invalid base64 format for {fileName}");
                    return null;
                }

                // Step 3: Convert to bytes
                byte[] pdfBytes = Convert.FromBase64String(cleanBase64);

                // Step 4: Validate PDF format
                if (!IsValidPdf(pdfBytes))
                {
                    Logger.Write($"Warning: {fileName} doesn't appear to be a valid PDF file");
                    // Still return it - might be a different format or corrupted
                }
                else
                {
                    Logger.Write($"Successfully converted {fileName}: {cleanBase64.Length} base64 chars -> {pdfBytes.Length} bytes");
                }

                return pdfBytes;
            }
            catch (FormatException ex)
            {
                Logger.Write($"Base64 format error for {fileName}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Write($"Error converting base64 for {fileName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cleans base64 string by removing whitespace and common formatting
        /// </summary>
        private static string CleanBase64String(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return string.Empty;

            // Remove all whitespace, newlines, carriage returns
            string cleaned = Regex.Replace(base64, @"\s+", "");

            // Remove data URI prefix if present (e.g., "data:application/pdf;base64,")
            int commaIndex = cleaned.IndexOf(',');
            if (cleaned.StartsWith("data:") && commaIndex > 0)
            {
                cleaned = cleaned.Substring(commaIndex + 1);
            }

            return cleaned;
        }

        /// <summary>
        /// Validates if string is valid base64
        /// </summary>
        private static bool IsValidBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return false;

            // Check if length is multiple of 4
            if (base64.Length % 4 != 0)
                return false;

            // Check for valid base64 characters
            return Regex.IsMatch(base64, @"^[a-zA-Z0-9\+/]*={0,2}$");
        }

        /// <summary>
        /// Checks if byte array appears to be a valid PDF
        /// </summary>
        private static bool IsValidPdf(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4)
                return false;

            // Check PDF header (%PDF)
            string header = Encoding.ASCII.GetString(bytes, 0, Math.Min(4, bytes.Length));
            return header.StartsWith("%PDF");
        }

        /// <summary>
        /// Save PDF with validation
        /// </summary>
        public static bool SavePdfFromBase64(string base64String, string folderPath, string fileName)
        {
            try
            {
                // Ensure folder exists
                Directory.CreateDirectory(folderPath);

                // Convert base64 to PDF bytes
                byte[]? pdfBytes = ConvertBase64ToPdf(base64String, fileName);
                if (pdfBytes == null)
                {
                    Logger.Write($"Failed to convert base64 for {fileName}");
                    return false;
                }

                // Build full path
                string fullPath = Path.Combine(folderPath, fileName);

                // Check if file already exists
                if (File.Exists(fullPath))
                {
                    Logger.Write($"File already exists: {fileName}");
                    return false;
                }

                // Write the file
                File.WriteAllBytes(fullPath, pdfBytes);

                // Verify the file was written correctly
                FileInfo fileInfo = new FileInfo(fullPath);
                if (fileInfo.Exists && fileInfo.Length == pdfBytes.Length)
                {
                    Logger.Write($"Successfully saved {fileName} ({fileInfo.Length:N0} bytes)");
                    return true;
                }
                else
                {
                    Logger.Write($"File verification failed for {fileName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error saving PDF {fileName}: {ex.Message}");
                return false;
            }
        }
    }
}