namespace AutoPrinter.Helpers
{
    public class LabelFile
    {
        public byte[] Data { get; set; }
        public string FileName { get; set; }
        public string? Id { get; set; } // Add ID tracking if API provides it
    }
}