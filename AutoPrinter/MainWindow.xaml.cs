using AutoPrinter.Helpers;
using AutoPrinter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;

namespace AutoPrinter
{
    public partial class MainWindow : Window
    {
        private System.Timers.Timer apiTimer;
        private AppSettings settings;
        private bool isPollingPaused = false;
        private const int MAX_LOG_LINES = 10000;
        private readonly Queue<string> logLines = new Queue<string>();

        // Console color scheme
        private readonly SolidColorBrush ConsoleBackground = new SolidColorBrush(Color.FromRgb(12, 12, 12));
        private readonly SolidColorBrush ConsoleForeground = new SolidColorBrush(Color.FromRgb(204, 204, 204));
        private readonly SolidColorBrush ConsoleTimestamp = new SolidColorBrush(Color.FromRgb(108, 108, 108));
        private readonly SolidColorBrush ConsoleError = new SolidColorBrush(Color.FromRgb(255, 85, 85));
        private readonly SolidColorBrush ConsoleWarning = new SolidColorBrush(Color.FromRgb(255, 193, 7));
        private readonly SolidColorBrush ConsoleSuccess = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        private readonly SolidColorBrush ConsoleInfo = new SolidColorBrush(Color.FromRgb(59, 142, 234));
        private readonly SolidColorBrush ConsoleDebug = new SolidColorBrush(Color.FromRgb(156, 109, 255));
        private System.Windows.Threading.DispatcherTimer clockTimer;

        public enum LogLevel
        {
            Default,
            Info,
            Success,
            Warning,
            Error,
            Debug
        }

        public MainWindow()
        {
            InitializeComponent();
            SetupConsoleStyle();
            LoadLogs();  // Load logs will call ShowConsoleHeader internally
            StartApiPolling();
            StartClockTimer();
        }

        private void SetupConsoleStyle()
        {
            // The RichTextBox is already styled in XAML, just ensure it's ready
            if (ConsoleOutput != null)
            {
                ConsoleOutput.Document.Blocks.Clear();
            }
        }

        private void ShowConsoleHeader()
        {
            Dispatcher.Invoke(() =>
            {
                if (ConsoleOutput == null) return;

                var document = ConsoleOutput.Document;
                document.Blocks.Clear();

                // ASCII Art Header (simplified for better display)
                Paragraph headerPara = new Paragraph();
                headerPara.FontFamily = new FontFamily("Consolas");
                headerPara.LineHeight = 14;

                string header = @"╔══════════════════════════════════════════════════════════╗
║          AutoPrinter - Label Processing System          ║
╚══════════════════════════════════════════════════════════╝";

                Run headerRun = new Run(header)
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118)),
                    FontSize = 11
                };
                headerPara.Inlines.Add(headerRun);
                document.Blocks.Add(headerPara);

                // Add version info  - don't call WriteConsoleLineColored during initialization
                Paragraph versionPara = new Paragraph();
                Run versionRun = new Run($"Version 1.0.0 | .NET {Environment.Version} | {DateTime.Now:yyyy-MM-dd}")
                {
                    Foreground = ConsoleDebug,
                    FontSize = 11
                };
                versionPara.Inlines.Add(versionRun);
                document.Blocks.Add(versionPara);

                Paragraph readyPara = new Paragraph();
                Run readyRun = new Run("System ready. Waiting for commands...")
                {
                    Foreground = ConsoleSuccess,
                    FontSize = 11
                };
                readyPara.Inlines.Add(readyRun);
                document.Blocks.Add(readyPara);

                Paragraph separatorPara = new Paragraph();
                Run separatorRun = new Run("─".PadRight(60, '─'))
                {
                    Foreground = ConsoleForeground,
                    FontSize = 11
                };
                separatorPara.Inlines.Add(separatorRun);
                document.Blocks.Add(separatorPara);
            });
        }

        private void StartClockTimer()
        {
            clockTimer = new System.Windows.Threading.DispatcherTimer();
            clockTimer.Interval = TimeSpan.FromSeconds(1);
            clockTimer.Tick += (s, e) =>
            {
                if (TimeText != null)
                    TimeText.Text = DateTime.Now.ToString("HH:mm:ss");

                // Update memory usage
                if (MemoryText != null)
                {
                    var memory = GC.GetTotalMemory(false) / (1024 * 1024);
                    MemoryText.Text = $"Memory: {memory} MB";
                }
            };
            clockTimer.Start();
        }

        private void LoadLogs()
        {
            // Use the Logger's ReadRecent method to get last 100 logs
            var logs = Logger.ReadRecent(100);

            // Clear existing document first
            if (ConsoleOutput != null)
            {
                ConsoleOutput.Document.Blocks.Clear();
            }

            // Display the console header first
            ShowConsoleHeader();

            // Then add the recent logs
            foreach (var logEntry in logs)
            {
                if (!string.IsNullOrWhiteSpace(logEntry))
                {
                    logLines.Enqueue(logEntry);
                    // Parse and display existing logs
                    string messageOnly = ExtractMessageFromLog(logEntry);
                    LogLevel level = DetermineLogLevel(messageOnly);
                    WriteConsoleLineColored(messageOnly, level, logEntry);
                }
            }

            // Subscribe to future log events
            Logger.LogWritten += OnLogWritten;

            if (ConsoleOutput != null)
            {
                ConsoleOutput.ScrollToEnd();
            }
        }

        private void OnLogWritten(string logMessage)
        {
            Dispatcher.Invoke(() =>
            {
                // The logMessage already contains timestamp from Logger, so parse it
                string messageOnly = ExtractMessageFromLog(logMessage);
                LogLevel level = DetermineLogLevel(messageOnly);
                WriteConsoleLineColored(messageOnly, level, logMessage);
                UpdateStatusBar();
            });
        }

        private string ExtractMessageFromLog(string logMessage)
        {
            // Logger format: [yyyy-MM-dd HH:mm:ss] message
            // Extract just the message part
            if (string.IsNullOrWhiteSpace(logMessage))
                return string.Empty;

            int bracketEnd = logMessage.IndexOf(']');
            if (bracketEnd > 0 && bracketEnd < logMessage.Length - 1)
            {
                return logMessage.Substring(bracketEnd + 1).Trim();
            }
            return logMessage;
        }

        private LogLevel DetermineLogLevel(string message)
        {
            string lowerMessage = message.ToLower();

            if (lowerMessage.Contains("error") || lowerMessage.Contains("failed") || lowerMessage.Contains("exception"))
                return LogLevel.Error;
            if (lowerMessage.Contains("warning") || lowerMessage.Contains("warn") || lowerMessage.Contains("skip"))
                return LogLevel.Warning;
            if (lowerMessage.Contains("success") || lowerMessage.Contains("saved") || lowerMessage.Contains("printed") || lowerMessage.Contains("completed") || lowerMessage.Contains("started"))
                return LogLevel.Success;
            if (lowerMessage.Contains("fetching") || lowerMessage.Contains("loading") || lowerMessage.Contains("processing"))
                return LogLevel.Info;
            if (lowerMessage.Contains("debug") || lowerMessage.Contains("trace"))
                return LogLevel.Debug;

            return LogLevel.Default;
        }

        private void WriteConsoleLineColored(string message, LogLevel level = LogLevel.Default, string fullLogMessage = null)
        {
            Dispatcher.Invoke(() =>
            {
                var document = ConsoleOutput.Document;

                // Create new paragraph for each line
                Paragraph para = new Paragraph();
                para.Margin = new Thickness(0, 0, 0, 1);

                // Extract timestamp from full log message if provided, otherwise use current time
                string timestamp;
                if (!string.IsNullOrEmpty(fullLogMessage))
                {
                    // Extract timestamp from log format: [yyyy-MM-dd HH:mm:ss]
                    int start = fullLogMessage.IndexOf('[');
                    int end = fullLogMessage.IndexOf(']');
                    if (start >= 0 && end > start)
                    {
                        string fullTimestamp = fullLogMessage.Substring(start + 1, end - start - 1);
                        // Just get the time part (HH:mm:ss)
                        var parts = fullTimestamp.Split(' ');
                        timestamp = parts.Length > 1 ? parts[1] : DateTime.Now.ToString("HH:mm:ss");
                    }
                    else
                    {
                        timestamp = DateTime.Now.ToString("HH:mm:ss");
                    }
                }
                else
                {
                    timestamp = DateTime.Now.ToString("HH:mm:ss");
                }

                Run timestampRun = new Run($"[{timestamp}] ")
                {
                    Foreground = ConsoleTimestamp,
                    FontSize = 11
                };
                para.Inlines.Add(timestampRun);

                // Add icon and message based on level
                string icon = "";
                SolidColorBrush messageColor = ConsoleForeground;
                FontWeight weight = FontWeights.Normal;

                switch (level)
                {
                    case LogLevel.Error:
                        icon = "✗ ";
                        messageColor = ConsoleError;
                        weight = FontWeights.Bold;
                        break;
                    case LogLevel.Warning:
                        icon = "⚠ ";
                        messageColor = ConsoleWarning;
                        break;
                    case LogLevel.Success:
                        icon = "✓ ";
                        messageColor = ConsoleSuccess;
                        break;
                    case LogLevel.Info:
                        icon = "ℹ ";
                        messageColor = ConsoleInfo;
                        break;
                    case LogLevel.Debug:
                        icon = "» ";
                        messageColor = ConsoleDebug;
                        break;
                    default:
                        icon = "  ";
                        break;
                }

                // Add icon if present
                if (!string.IsNullOrEmpty(icon))
                {
                    Run iconRun = new Run(icon)
                    {
                        Foreground = messageColor,
                        FontWeight = FontWeights.Bold
                    };
                    para.Inlines.Add(iconRun);
                }

                // Add the message
                Run messageRun = new Run(message)
                {
                    Foreground = messageColor,
                    FontWeight = weight
                };
                para.Inlines.Add(messageRun);

                // Add paragraph to document
                document.Blocks.Add(para);

                // Keep log lines under control
                while (document.Blocks.Count > MAX_LOG_LINES)
                {
                    document.Blocks.Remove(document.Blocks.FirstBlock);
                }

                // Auto-scroll to bottom
                ConsoleOutput.ScrollToEnd();
            });
        }

        private void UpdateStatusBar(int? lineCount = null)
        {
            Dispatcher.Invoke(() =>
            {
                // Update line count
                if (lineCount == null && ConsoleOutput != null)
                {
                    lineCount = ConsoleOutput.Document.Blocks.Count;
                }

                if (LineCountText != null)
                    LineCountText.Text = $"Lines: {lineCount}";

                // Update status
                if (BottomStatusText != null)
                {
                    BottomStatusText.Text = isPollingPaused ? "Paused" : "Running";
                }
            });
        }

        private void StartApiPolling()
        {
            if (apiTimer != null)
            {
                apiTimer.Stop();
                apiTimer.Elapsed -= async (s, e) => await FetchAndSaveLabelsAsync();
                apiTimer.Dispose();
            }

            settings = SettingsManager.Load();
            double intervalMs = settings.PollingDurationSeconds * 1000;

            apiTimer = new System.Timers.Timer(intervalMs);
            apiTimer.Elapsed += async (s, e) => await FetchAndSaveLabelsAsync();
            apiTimer.AutoReset = true;
            apiTimer.Start();

            Logger.Write($"API polling started - Interval: {settings.PollingDurationSeconds}s");
        }

        private async Task FetchAndSaveLabelsAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settings.ApiUrl) || string.IsNullOrWhiteSpace(settings.UserPin))
                {
                    WriteConsoleLineColored("API settings missing. Skipping request.", LogLevel.Warning);
                    return;
                }

                WriteConsoleLineColored("Fetching labels from API...", LogLevel.Info);
                var labelFiles = await ApiHelper.FetchAllLabelsAsync(settings.ApiUrl, settings.UserPin);

                // Update last poll time
                Dispatcher.Invoke(() =>
                {
                    if (LastPollText != null)
                        LastPollText.Text = $"Last poll: {DateTime.Now:HH:mm:ss}";
                });

                if (labelFiles == null || labelFiles.Length == 0)
                {
                    return;
                }

                WriteConsoleLineColored($"Processing {labelFiles.Length} label(s)", LogLevel.Info);

                foreach (var labelFile in labelFiles)
                {
                    if (PdfHelper.IsAlreadyProcessed(labelFile.FileName))
                    {
                        WriteConsoleLineColored($"Skip: {labelFile.FileName} (already processed)", LogLevel.Warning);
                        continue;
                    }

                    if (settings.SaveToFolder && !string.IsNullOrWhiteSpace(settings.PathToSavePdf))
                    {
                        try
                        {
                            string savedFile = PdfHelper.SavePdf(labelFile.Data, settings.PathToSavePdf, labelFile.FileName);
                            if (!string.IsNullOrEmpty(savedFile))
                            {
                                WriteConsoleLineColored($"Saved: {labelFile.FileName}", LogLevel.Success);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteConsoleLineColored($"Save failed: {labelFile.FileName} - {ex.Message}", LogLevel.Error);
                        }
                    }

                    if (settings.DirectPrint)
                    {
                        try
                        {
                            PrintHelper.PrintPdf(labelFile.Data, settings.SelectedPrinter);
                            WriteConsoleLineColored($"Printed: {labelFile.FileName} → {settings.SelectedPrinter}", LogLevel.Success);
                        }
                        catch (Exception ex)
                        {
                            WriteConsoleLineColored($"Print failed: {labelFile.FileName} - {ex.Message}", LogLevel.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteConsoleLineColored($"API cycle error: {ex.Message}", LogLevel.Error);
            }
        }

        public void OnSettingsUpdated(AppSettings updatedSettings)
        {
            settings = updatedSettings;
            RestartApiPolling();
        }

        private void RestartApiPolling()
        {
            if (apiTimer != null)
            {
                apiTimer.Stop();
                apiTimer.Elapsed -= async (s, e) => await FetchAndSaveLabelsAsync();
                apiTimer.Dispose();
            }

            settings = SettingsManager.Load();
            double intervalMs = settings.PollingDurationSeconds * 1000;

            apiTimer = new System.Timers.Timer(intervalMs);
            apiTimer.Elapsed += async (s, e) => await FetchAndSaveLabelsAsync();
            apiTimer.AutoReset = true;

            if (!isPollingPaused)
            {
                apiTimer.Start();
                WriteConsoleLineColored($"Polling restarted - Interval: {settings.PollingDurationSeconds}s", LogLevel.Success);
            }
            else
            {
                WriteConsoleLineColored($"Timer updated - Interval: {settings.PollingDurationSeconds}s (paused)", LogLevel.Info);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            WriteConsoleLineColored("Opening settings...", LogLevel.Info);
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private void PauseResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (apiTimer == null) return;

            if (isPollingPaused)
            {
                apiTimer.Start();
                PauseResumeText.Text = "Pause";
                PauseResumeIcon.Text = "⏸";
                PauseIconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fef3c7"));
                PauseResumeIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b"));
                WriteConsoleLineColored("Polling resumed", LogLevel.Success);
                isPollingPaused = false;

                // Update status indicator
                if (StatusIndicator != null && StatusText != null)
                {
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    StatusText.Text = "Connected";
                }
            }
            else
            {
                apiTimer.Stop();
                PauseResumeText.Text = "Resume";
                PauseResumeIcon.Text = "▶";
                PauseIconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dcfce7"));
                PauseResumeIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16a34a"));
                WriteConsoleLineColored("Polling paused", LogLevel.Warning);
                isPollingPaused = true;

                // Update status indicator
                if (StatusIndicator != null && StatusText != null)
                {
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                    StatusText.Text = "Paused";
                }
            }

            UpdateStatusBar();
        }

        private void ClearLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (ConsoleOutput != null)
                {
                    ConsoleOutput.Document.Blocks.Clear();
                    ShowConsoleHeader();
                    WriteConsoleLineColored("Console cleared", LogLevel.Info);
                    UpdateStatusBar();
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            WriteConsoleLineColored("Shutting down...", LogLevel.Info);
            base.OnClosed(e);

            clockTimer?.Stop();
            apiTimer?.Stop();
            apiTimer?.Dispose();
            PrintHelper.Cleanup();
            Logger.LogWritten -= OnLogWritten;

            WriteConsoleLineColored("Goodbye!", LogLevel.Success);
        }
    }
}