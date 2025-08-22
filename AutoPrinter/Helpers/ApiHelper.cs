using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoPrinter.Helpers
{
    public static class ApiHelper
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task<LabelFile?> FetchLabelAsync(string apiUrl, string userPin)
        {
            try
            {
                var response = await client.GetAsync($"{apiUrl}?user_id={userPin}");
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                // Case: files array returned
                if (doc.RootElement.TryGetProperty("files", out JsonElement filesElement) &&
                    filesElement.ValueKind == JsonValueKind.Array &&
                    filesElement.GetArrayLength() > 0)
                {
                    var firstFile = filesElement[0];
                    string base64 = firstFile[0].GetString();
                    string fileName = firstFile[1].GetString();

                    if (!string.IsNullOrWhiteSpace(base64) && !string.IsNullOrWhiteSpace(fileName))
                    {
                        return new LabelFile
                        {
                            Data = Convert.FromBase64String(base64),
                            FileName = fileName.EndsWith(".pdf") ? fileName : $"{fileName}.pdf"
                        };
                    }
                }

                // Fallback: message or empty files
                if (doc.RootElement.TryGetProperty("message", out JsonElement messageElement))
                {
                    Logger.Write($"API message: {messageElement.GetString()}");
                    return null;
                }

                Logger.Write("API returned unexpected response.");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Write($"API error: {ex.Message}");
                return null;
            }
        }


    }
}
