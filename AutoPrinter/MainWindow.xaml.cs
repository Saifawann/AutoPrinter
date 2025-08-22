using AutoPrinter.Helpers;
using AutoPrinter.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoPrinter
{
    public partial class MainWindow : Window
    {
        private System.Timers.Timer apiTimer;
        private AppSettings settings;
        private bool isPollingPaused = false;
        private const int MAX_LOG_LINES = 1000; // Limit lines to prevent memory issues
        private readonly Queue<string> logLines = new Queue<string>();


        public MainWindow()
        {
            InitializeComponent();
            SetupTerminalStyle();
            LoadLogs();
            StartApiPolling();
        }

        private void SetupTerminalStyle()
        {
            // Apply terminal-like styling to TxtLogs
            TxtLogs.FontFamily = new FontFamily("Consolas, Courier New, monospace");
            TxtLogs.FontSize = 12;
            TxtLogs.IsReadOnly = true;
            TxtLogs.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            TxtLogs.TextWrapping = TextWrapping.NoWrap;

            // Disable text selection if you want pure terminal feel
            // TxtLogs.IsTabStop = false;
        }

        private void StartApiPolling()
        {
            settings = SettingsManager.Load();
            double intervalMs = settings.PollingDurationSeconds * 1000;

            apiTimer = new System.Timers.Timer(intervalMs); // every 30 seconds
            apiTimer.Elapsed += async (s, e) => await FetchAndSaveLabelsAsync();
            apiTimer.AutoReset = true;
            apiTimer.Start();

            Logger.Write($"Started API polling every {settings.PollingDurationSeconds} seconds.");
        }

        private void OnLogWritten(string logMessage)
        {
            Dispatcher.Invoke(() =>
            {
                // Check if we need to maintain scroll position at bottom
                bool wasScrolledToBottom = IsScrolledToBottom();

                // Just append the new line instead of rebuilding entire text
                if (!string.IsNullOrEmpty(TxtLogs.Text))
                {
                    TxtLogs.AppendText(Environment.NewLine);
                }
                TxtLogs.AppendText(logMessage);

                // Add to our line tracking for potential cleanup
                AddLogLine(logMessage);

                // Only scroll to bottom if user was already at bottom
                if (wasScrolledToBottom)
                {
                    TxtLogs.ScrollToEnd();
                }
            });
        }

        private void LoadLogs()
        {
            var logs = Logger.ReadAll();

            // Add existing logs to queue
            foreach (var log in logs)
            {
                AddLogLine(log); // Don't trigger scroll for initial load
            }

            // Update display
            UpdateLogDisplay();

            Logger.LogWritten += OnLogWritten;
            TxtLogs.ScrollToEnd();
        }

        private void AddLogLine(string logMessage)
        {
            logLines.Enqueue(logMessage);

            // Maintain maximum line limit - if exceeded, rebuild text
            if (logLines.Count > MAX_LOG_LINES)
            {
                logLines.Dequeue();
                // Only rebuild when we hit the limit
                RebuildLogDisplay();
            }
        }

        private void RebuildLogDisplay()
        {
            // Store scroll position
            bool wasScrolledToBottom = IsScrolledToBottom();

            // Rebuild entire text content
            TxtLogs.Text = string.Join(Environment.NewLine, logLines);

            // Restore scroll position
            if (wasScrolledToBottom)
            {
                TxtLogs.ScrollToEnd();
            }
        }

        private bool IsScrolledToBottom()
        {
            // Check if scroll is at or very close to bottom (within 1 line)
            return TxtLogs.VerticalOffset >= TxtLogs.ExtentHeight - TxtLogs.ViewportHeight - 20;
        }

        private void UpdateLogDisplay()
        {
            // This method now just rebuilds display (used for initial load)
            TxtLogs.Text = string.Join(Environment.NewLine, logLines);
        }

        private async Task FetchAndSaveLabelsAsync()
        {
            try
            {
                // Load settings
                if (string.IsNullOrWhiteSpace(settings.ApiUrl) || string.IsNullOrWhiteSpace(settings.UserPin))
                {
                    Logger.Write("API settings missing. Skipping request.");
                    return;
                }

                // === Step 1: Call API for all labels ===
                var labelFiles = await ApiHelper.FetchAllLabelsAsync(settings.ApiUrl, settings.UserPin);
                if (labelFiles == null || labelFiles.Length == 0)
                {
                    return;
                }

                foreach (var labelFile in labelFiles)
                {
                    // === Step 2: Save to folder (if enabled) ===
                    if (settings.SaveToFolder && !string.IsNullOrWhiteSpace(settings.PathToSavePdf))
                    {
                        try
                        {
                            string savedFile = PdfHelper.SavePdf(labelFile.Data, settings.PathToSavePdf, labelFile.FileName);
                        }
                        catch (Exception ex)
                        {
                            Logger.Write($"Error saving {labelFile.FileName}: {ex}");
                        }
                    }

                    // === Step 3: Direct Print (if enabled) ===
                    if (settings.DirectPrint)
                    {
                        try
                        {
                            PrintHelper.PrintPdf(labelFile.Data, settings.SelectedPrinter);
                            Logger.Write($"PDF sent to printer: {labelFile.FileName} on {settings.SelectedPrinter}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Write($"Error printing {labelFile.FileName}: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error in API cycle: {ex}");
            }
        }

        // In your MainWindow - add this method
        public void OnSettingsUpdated(AppSettings updatedSettings)
        {
            // Update the local settings object
            settings = updatedSettings;

            // Restart polling with new duration
            StartApiPolling();

        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private void PauseResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (apiTimer == null) return;

            if (isPollingPaused)
            {
                // Resume polling
                apiTimer.Start();
                PauseResumeText.Text = "Pause";
                PauseResumeIcon.Text = "⏸"; // Pause icon
                Logger.Write("Polling resumed.");
                isPollingPaused = false;
            }
            else
            {
                // Pause polling
                apiTimer.Stop();
                PauseResumeText.Text = "Resume";
                PauseResumeIcon.Text = "▶"; // Play icon
                Logger.Write("Polling paused.");
                isPollingPaused = true;
            }
        }

    }
}
