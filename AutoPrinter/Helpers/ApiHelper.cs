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

        public static async Task<LabelFile[]?> FetchAllLabelsAsync(string apiUrl, string userPin)
        {
            try
            {
                var response = await client.GetAsync($"{apiUrl}?user_id={userPin}");
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("files", out JsonElement filesElement) &&
                    filesElement.ValueKind == JsonValueKind.Array &&
                    filesElement.GetArrayLength() > 0)
                {
                    var labels = new List<LabelFile>();

                    foreach (var fileEntry in filesElement.EnumerateArray())
                    {
                        if (fileEntry.ValueKind == JsonValueKind.Array && fileEntry.GetArrayLength() >= 2)
                        {
                            string? base64 = fileEntry[0].GetString();
                            string? fileName = fileEntry[1].GetString();

                            if (!string.IsNullOrWhiteSpace(base64) && !string.IsNullOrWhiteSpace(fileName))
                            {
                                labels.Add(new LabelFile
                                {
                                    Data = Convert.FromBase64String(base64),
                                    FileName = fileName.EndsWith(".pdf") ? fileName : $"{fileName}.pdf"
                                });
                            }
                        }
                    }

                    if (labels.Count > 0)
                        return labels.ToArray();
                }

                // Log API message or unexpected response
                if (doc.RootElement.TryGetProperty("message", out JsonElement messageElement))
                {
                    Logger.Write($"API message: {messageElement.GetString()}");
                }
                else
                {
                    Logger.Write("API returned no files or unexpected response.");
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Write($"API error: {ex}");
                return null;
            }
        }



    }
}
