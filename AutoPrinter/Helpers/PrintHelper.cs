using System;
using System.Drawing.Printing;
using System.IO;
using PdfiumViewer;

namespace AutoPrinter.Helpers
{
    public static class PrintHelper
    {
        public static void PrintPdf(byte[] fileBytes, string printerName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    Logger.Write("No printer selected.");
                    return;
                }

                using (var stream = new MemoryStream(fileBytes))
                using (var pdfDocument = PdfDocument.Load(stream))
                using (var printDocument = pdfDocument.CreatePrintDocument())
                {
                    // Set printer
                    printDocument.PrinterSettings.PrinterName = printerName;

                    // Configure for silent printing (no dialog)
                    printDocument.PrintController = new StandardPrintController();

                    // Print
                    printDocument.Print();

                    Logger.Write($"PDF sent to printer: {printerName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Print failed: {ex.Message}");
            }
        }
    }
}