using System.IO;
namespace AutoPrinter.Models
{
    public class AppSettings
    {
        // General
        public bool DirectPrint { get; set; } = true;
        public bool SaveToFolder { get; set; } = true;
        public string PathToSavePdf { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AutoPrinter",
            "SavedPdfs"
        );

        // Constructor to ensure default directory exists
        public AppSettings()
        {
            try
            {
                Directory.CreateDirectory(PathToSavePdf);
            }
            catch (Exception ex)
            {
                // Handle or log exception as needed
                System.Diagnostics.Debug.WriteLine($"Failed to create default directory: {ex.Message}");
            }
        }

        // API
        public int PollingDurationSeconds { get; set; } = 30;
        public string ApiUrl { get; set; } = "https://gettabox.channeldispatch.co.uk/api/v1/download_file";
        public string UserPin { get; set; } = "";

        // Printing
        public string SelectedPrinter { get; set; } = "";
        public string PageRange { get; set; } = "All"; // "All" or "Range"
        public string PagesToPrint { get; set; } = "";
        public int Copies { get; set; } = 1;
        public string Collation { get; set; } = "By documents";
        public string Scaling { get; set; } = "Scale to fit";
        public bool AutoPortrait { get; set; } = true;
        public string EmailAttachments { get; set; } = "Ignore";
        public bool PrintCoverPage { get; set; } = false;
    }
}
