namespace AutoPrinter.Models
{
    public class AppSettings
    {
        // General
        public bool DirectPrint { get; set; } = true;
        public bool SaveToFolder { get; set; } = false;
        public string PathToSavePdf { get; set; } = "";

        // API
        public string ApiUrl { get; set; } = "https://beta.channeldispatch.co.uk/api/v1/download_file";
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
