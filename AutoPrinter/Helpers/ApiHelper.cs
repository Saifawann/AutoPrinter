using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoPrinter.Helpers
{
    public static class ApiHelper
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly HashSet<string> processedFiles = new HashSet<string>();
        private static string processedFilesLog = "processed_files.log";

        static ApiHelper()
        {
            // Set a timeout for HTTP requests
            client.Timeout = TimeSpan.FromSeconds(30);
            // Load previously processed files on startup
            LoadProcessedFiles();
        }

        private static void LoadProcessedFiles()
        {
            try
            {
                if (File.Exists(processedFilesLog))
                {
                    var lines = File.ReadAllLines(processedFilesLog);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            processedFiles.Add(line.Trim());
                    }
                    Logger.Write($"Loaded {processedFiles.Count} previously processed files.");
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error loading processed files log: {ex.Message}");
            }
        }

        private static void SaveProcessedFile(string fileName)
        {
            try
            {
                processedFiles.Add(fileName);
                File.AppendAllText(processedFilesLog, fileName + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Logger.Write($"Error saving to processed files log: {ex.Message}");
            }
        }

        // Helper method to safely get string value from JsonElement
        private static string? GetStringValue(JsonElement element)
        {
            try
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        return element.GetString();
                    case JsonValueKind.Number:
                        // If it's a number, convert it to string
                        if (element.TryGetInt64(out long longValue))
                            return longValue.ToString();
                        if (element.TryGetDouble(out double doubleValue))
                            return doubleValue.ToString();
                        return element.GetRawText();
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return element.GetBoolean().ToString();
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                        return null;
                    default:
                        return element.GetRawText();
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error getting string value: {ex.Message}");
                return null;
            }
        }

        public static async Task<LabelFile[]?> FetchAllLabelsAsync(string apiUrl, string userPin)
        {
            try
            {
                // Build the full URL (matching PHP format)
                string fullUrl = $"{apiUrl}?user_id={userPin}";
                Logger.Write($"Fetching from URL: {fullUrl}");

                var response = await client.GetAsync(fullUrl);

                // Log status code for debugging
                Logger.Write($"API Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Logger.Write($"API Error Response: {errorContent}");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();

                // Debug: Log full response for debugging (comment out in production)
                //if (json.Length > 0)
                //{
                //    // Log first 500 chars to see structure
                //    var preview = json.Length > 500 ? json.Substring(0, 500) + "..." : json;
                //    Logger.Write($"API Response preview: {preview}");

                //    // Also log total response size
                //    Logger.Write($"Total response size: {json.Length} characters");
                //}

                using var doc = JsonDocument.Parse(json);

                // Check if we have a files property
                if (doc.RootElement.TryGetProperty("files", out JsonElement filesElement))
                {
                    if (filesElement.ValueKind == JsonValueKind.Array)
                    {
                        int arrayLength = filesElement.GetArrayLength();
                        Logger.Write($"Found 'files' array with {arrayLength} entries");

                        if (arrayLength == 0)
                        {
                            Logger.Write("Files array is empty - no new labels");
                            return null;
                        }

                        var labels = new List<LabelFile>();
                        int processedCount = 0;
                        int skippedCount = 0;
                        int errorCount = 0;

                        foreach (var fileEntry in filesElement.EnumerateArray())
                        {
                            processedCount++;
                            try
                            {
                                Logger.Write($"Processing entry {processedCount}/{arrayLength}");

                                if (fileEntry.ValueKind == JsonValueKind.Array)
                                {
                                    int entryLength = fileEntry.GetArrayLength();
                                    Logger.Write($"Entry has {entryLength} elements");

                                    if (entryLength >= 2)
                                    {
                                        // Index 0: Base64 PDF data
                                        string? base64 = GetStringValue(fileEntry[0]);

                                        // Index 1: Filename (might be a number that needs to be converted to string)
                                        string? fileName = GetStringValue(fileEntry[1]);

                                        // Index 2: ID (optional)
                                        string? fileId = null;
                                        if (entryLength >= 3)
                                        {
                                            fileId = GetStringValue(fileEntry[2]);
                                        }

                                        // Detailed logging
                                        Logger.Write($"Raw filename value: '{fileName}', Type: {fileEntry[1].ValueKind}");
                                        Logger.Write($"Base64 length: {base64?.Length ?? 0} chars");
                                        Logger.Write($"File ID: {fileId ?? "none"}");

                                        // Validate base64 and filename
                                        if (string.IsNullOrWhiteSpace(base64))
                                        {
                                            Logger.Write($"ERROR: Base64 data is empty for entry {processedCount}");
                                            errorCount++;
                                            continue;
                                        }

                                        if (string.IsNullOrWhiteSpace(fileName))
                                        {
                                            Logger.Write($"ERROR: Filename is empty for entry {processedCount}");
                                            errorCount++;
                                            continue;
                                        }

                                        // Clean up filename - remove any quotes or special characters
                                        fileName = fileName.Trim().Trim('"').Trim();

                                        // Ensure .pdf extension
                                        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                        {
                                            fileName = $"{fileName}.pdf";
                                        }

                                        Logger.Write($"Final filename: {fileName}");

                                        // Check if already processed
                                        if (processedFiles.Contains(fileName))
                                        {
                                            Logger.Write($"Skipping: {fileName} (already processed)");
                                            skippedCount++;
                                            continue;
                                        }

                                        // Convert base64 to bytes
                                        byte[] pdfData;
                                        try
                                        {
                                            // Clean base64 string
                                            base64 = base64.Trim();

                                            // Remove any potential data URL prefix
                                            if (base64.Contains(","))
                                            {
                                                base64 = base64.Substring(base64.LastIndexOf(',') + 1);
                                            }

                                            // Remove whitespace and newlines
                                            base64 = base64.Replace("\n", "")
                                                          .Replace("\r", "")
                                                          .Replace(" ", "")
                                                          .Replace("\t", "");

                                            // Validate base64 string
                                            if (base64.Length % 4 != 0)
                                            {
                                                // Pad with = if needed
                                                int padding = 4 - (base64.Length % 4);
                                                if (padding < 4)
                                                {
                                                    base64 = base64.PadRight(base64.Length + padding, '=');
                                                    Logger.Write($"Added {padding} padding characters to base64");
                                                }
                                            }

                                            pdfData = Convert.FromBase64String(base64);

                                            // Validate PDF data (PDF files start with %PDF)
                                            if (pdfData.Length > 4)
                                            {
                                                string header = System.Text.Encoding.ASCII.GetString(pdfData, 0, 4);
                                                if (header != "%PDF")
                                                {
                                                    Logger.Write($"WARNING: File {fileName} doesn't appear to be a valid PDF (header: {header})");
                                                }
                                            }

                                            Logger.Write($"Successfully decoded {fileName}: {pdfData.Length:N0} bytes");
                                        }
                                        catch (FormatException ex)
                                        {
                                            Logger.Write($"ERROR: Invalid base64 for {fileName}: {ex.Message}");
                                            Logger.Write($"Base64 sample (first 100 chars): {(base64.Length > 100 ? base64.Substring(0, 100) : base64)}");
                                            errorCount++;
                                            continue;
                                        }

                                        // Create label file object
                                        var labelFile = new LabelFile
                                        {
                                            Data = pdfData,
                                            FileName = fileName,
                                            Id = fileId
                                        };

                                        labels.Add(labelFile);

                                        // Mark as processed
                                        SaveProcessedFile(fileName);

                                        Logger.Write($"SUCCESS: Added {fileName} to processing queue");
                                    }
                                    else
                                    {
                                        Logger.Write($"ERROR: Entry {processedCount} has insufficient elements (expected >= 2, got {entryLength})");
                                        errorCount++;
                                    }
                                }
                                else
                                {
                                    Logger.Write($"ERROR: Entry {processedCount} is not an array (type: {fileEntry.ValueKind})");
                                    errorCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Write($"ERROR processing entry {processedCount}: {ex.Message}");
                                Logger.Write($"Stack trace: {ex.StackTrace}");
                                errorCount++;
                            }
                        }

                        // Summary
                        Logger.Write($"Processing complete - New: {labels.Count}, Skipped: {skippedCount}, Errors: {errorCount}");

                        if (labels.Count > 0)
                        {
                            return labels.ToArray();
                        }
                        else if (skippedCount > 0)
                        {
                            Logger.Write("No new labels (all previously processed)");
                            return null;
                        }
                        else if (errorCount > 0)
                        {
                            Logger.Write("No valid labels found due to errors");
                            return null;
                        }
                    }
                    else
                    {
                        Logger.Write($"ERROR: 'files' is not an array (type: {filesElement.ValueKind})");
                    }
                }
                else
                {
                    // Log all available properties for debugging
                    Logger.Write("ERROR: No 'files' property in response. Available properties:");
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        Logger.Write($"  - {property.Name}: {property.Value.ValueKind}");

                        // If it's a message, log it
                        if (property.Name == "message")
                        {
                            string? message = GetStringValue(property.Value);
                            Logger.Write($"API Message: {message}");
                        }
                    }
                }

                return null;
            }
            catch (HttpRequestException ex)
            {
                Logger.Write($"NETWORK ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Logger.Write($"Inner exception: {ex.InnerException.Message}");
                }
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Write($"REQUEST TIMEOUT: The request took too long to complete");
                return null;
            }
            catch (JsonException ex)
            {
                Logger.Write($"JSON PARSE ERROR: {ex.Message}");
                Logger.Write($"This usually means the API returned invalid JSON");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Write($"UNEXPECTED ERROR: {ex.GetType().Name}: {ex.Message}");
                Logger.Write($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // Method to clear processed files history
        public static void ClearProcessedHistory()
        {
            processedFiles.Clear();
            try
            {
                if (File.Exists(processedFilesLog))
                    File.Delete(processedFilesLog);
                Logger.Write("Cleared processed files history.");
            }
            catch (Exception ex)
            {
                Logger.Write($"Error clearing processed files history: {ex.Message}");
            }
        }

        // Method to check if a file has been processed
        public static bool IsFileProcessed(string fileName)
        {
            return processedFiles.Contains(fileName);
        }

        // Method to manually test API connection
        public static async Task<bool> TestApiConnectionAsync(string apiUrl, string userPin)
        {
            try
            {
                string fullUrl = $"{apiUrl}?user_id={userPin}";
                Logger.Write($"Testing API connection to: {fullUrl}");

                var response = await client.GetAsync(fullUrl);
                Logger.Write($"Test response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    Logger.Write($"Test response length: {content.Length} chars");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Write($"API test failed: {ex.Message}");
                return false;
            }
        }
    }
}