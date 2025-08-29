using AutoPrinter.Helpers;
using AutoPrinter.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using System.Windows.Threading;

namespace AutoPrinter
{
    public partial class MainWindow : Window
    {
        // API and Settings
        private System.Timers.Timer apiTimer;
        private AppSettings settings;
        private bool isPollingPaused = false;

        // Console Management
        private const int MAX_LOG_LINES = 100000;
        private readonly Queue<string> logLines = new Queue<string>();

        // Performance Optimization
        private readonly ConcurrentQueue<LogEntry> _pendingLogs = new ConcurrentQueue<LogEntry>();
        private DispatcherTimer _batchUpdateTimer;
        private volatile bool _isUpdating = false;
        private const int BATCH_UPDATE_THRESHOLD = 10;
        private const int UPDATE_DELAY_MS = 50;

        // Scroll Management
        private bool _isUserScrolling = false;
        private ScrollViewer _scrollViewer;
        private double _lastKnownScrollPosition = 0;

        // Memory Management
        private DispatcherTimer _memoryCleanupTimer;
        private long _lastMemoryCheck = 0;

        // Timers
        private DispatcherTimer clockTimer;

        // Console color scheme
        private readonly SolidColorBrush ConsoleBackground = new SolidColorBrush(Color.FromRgb(12, 12, 12));
        private readonly SolidColorBrush ConsoleForeground = new SolidColorBrush(Color.FromRgb(204, 204, 204));
        private readonly SolidColorBrush ConsoleTimestamp = new SolidColorBrush(Color.FromRgb(108, 108, 108));
        private readonly SolidColorBrush ConsoleError = new SolidColorBrush(Color.FromRgb(255, 85, 85));
        private readonly SolidColorBrush ConsoleWarning = new SolidColorBrush(Color.FromRgb(255, 193, 7));
        private readonly SolidColorBrush ConsoleSuccess = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        private readonly SolidColorBrush ConsoleInfo = new SolidColorBrush(Color.FromRgb(59, 142, 234));
        private readonly SolidColorBrush ConsoleDebug = new SolidColorBrush(Color.FromRgb(156, 109, 255));

        // Log Entry Structure
        private struct LogEntry
        {
            public string Message;
            public LogLevel Level;
            public string FullMessage;
            public DateTime Timestamp;
        }

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
            InitializeConsole();
            StartApiPolling();
        }

        #region Console Initialization

        private void InitializeConsole()
        {
            SetupConsoleStyle();
            SetupScrollViewer();
            SetupBatchUpdateTimer();
            SetupMemoryManagement();
            StartClockTimer();
            LoadLogs();
        }

        private void SetupConsoleStyle()
        {
            if (ConsoleOutput != null)
            {
                ConsoleOutput.Document.Blocks.Clear();

                // Optimize rendering
                ConsoleOutput.IsReadOnly = true;
                ConsoleOutput.IsDocumentEnabled = true;
                TextOptions.SetTextFormattingMode(ConsoleOutput, TextFormattingMode.Display);
                TextOptions.SetTextRenderingMode(ConsoleOutput, TextRenderingMode.ClearType);
            }
        }

        private void SetupScrollViewer()
        {
            if (ConsoleOutput == null) return;

            ConsoleOutput.Loaded += (s, e) =>
            {
                _scrollViewer = GetScrollViewer(ConsoleOutput);
                if (_scrollViewer != null)
                {
                    _scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
                }
            };
        }

        private ScrollViewer GetScrollViewer(DependencyObject element)
        {
            if (element == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                if (child is ScrollViewer scrollViewer)
                    return scrollViewer;

                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Ignore programmatic scrolls (content size changes)
            if (Math.Abs(e.ExtentHeightChange) > 0.001)
                return;

            // Check if user is scrolling
            if (Math.Abs(e.VerticalChange) > 0.001)
            {
                bool wasAtBottom = Math.Abs(_lastKnownScrollPosition + e.ViewportHeight - e.ExtentHeight) < 10.0;
                bool isNowAtBottom = IsScrolledToBottom();

                // User scrolled up from bottom
                if (wasAtBottom && !isNowAtBottom && e.VerticalChange < 0)
                {
                    _isUserScrolling = true;
                }
                // User scrolled to bottom
                else if (isNowAtBottom)
                {
                    _isUserScrolling = false;
                }
            }

            _lastKnownScrollPosition = e.VerticalOffset;
        }

        private void SetupBatchUpdateTimer()
        {
            _batchUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(UPDATE_DELAY_MS)
            };
            _batchUpdateTimer.Tick += ProcessBatchedLogs;
            _batchUpdateTimer.Start();
        }

        private void SetupMemoryManagement()
        {
            _memoryCleanupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _memoryCleanupTimer.Tick += (s, e) =>
            {
                var currentMemory = GC.GetTotalMemory(false);
                if (currentMemory > _lastMemoryCheck * 1.5 && currentMemory > 50_000_000)
                {
                    GC.Collect(2, GCCollectionMode.Optimized);
                    _lastMemoryCheck = GC.GetTotalMemory(true);
                }
                else
                {
                    _lastMemoryCheck = currentMemory;
                }
            };
            _memoryCleanupTimer.Start();
        }

        #endregion

        #region Console Display

        private void ShowConsoleHeader()
        {
            if (ConsoleOutput == null) return;

            var document = ConsoleOutput.Document;
            document.Blocks.Clear();

            // Header
            var headerPara = new Paragraph
            {
                FontFamily = new FontFamily("Consolas"),
                LineHeight = 14,
                Margin = new Thickness(0)
            };

            string header = @"╔══════════════════════════════════════════════════════════╗
║          AutoPrinter - Label Processing System           ║
╚══════════════════════════════════════════════════════════╝";

            headerPara.Inlines.Add(new Run(header)
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118)),
                FontSize = 11
            });
            document.Blocks.Add(headerPara);

            // Version info
            var versionPara = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
            versionPara.Inlines.Add(new Run($"Version 1.0.0 | .NET {Environment.Version} | {DateTime.Now:yyyy-MM-dd}")
            {
                Foreground = ConsoleDebug,
                FontSize = 11
            });
            document.Blocks.Add(versionPara);

            // Ready message
            var readyPara = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
            readyPara.Inlines.Add(new Run("System ready. Waiting for commands...")
            {
                Foreground = ConsoleSuccess,
                FontSize = 11
            });
            document.Blocks.Add(readyPara);

            // Separator
            var separatorPara = new Paragraph { Margin = new Thickness(0, 2, 0, 4) };
            separatorPara.Inlines.Add(new Run(new string('─', 60))
            {
                Foreground = ConsoleForeground,
                FontSize = 11
            });
            document.Blocks.Add(separatorPara);
        }

        private void StartClockTimer()
        {
            clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            clockTimer.Tick += (s, e) =>
            {
                if (TimeText != null)
                    TimeText.Text = DateTime.Now.ToString("HH:mm:ss");

                if (MemoryText != null)
                {
                    var memory = GC.GetTotalMemory(false) / (1024 * 1024);
                    MemoryText.Text = $"Memory: {memory:N0} MB";
                }
            };
            clockTimer.Start();
        }

        #endregion

        #region Log Processing

        private void LoadLogs()
        {
            var logs = Logger.ReadRecent(100);

            if (ConsoleOutput != null)
            {
                ConsoleOutput.Document.Blocks.Clear();
            }

            ShowConsoleHeader();

            // Process initial logs directly (no batching for initial load)
            foreach (var logEntry in logs)
            {
                if (!string.IsNullOrWhiteSpace(logEntry))
                {
                    logLines.Enqueue(logEntry);
                    string messageOnly = ExtractMessageFromLog(logEntry);
                    LogLevel level = DetermineLogLevel(messageOnly);
                    WriteConsoleLineDirect(messageOnly, level, logEntry);
                }
            }

            // Subscribe to future log events
            Logger.LogWritten -= OnLogWritten;
            Logger.LogWritten += OnLogWritten;

            // Ensure we scroll to end after initial load
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ConsoleOutput?.ScrollToEnd();
            }), DispatcherPriority.ContextIdle);
        }

        private void OnLogWritten(string logMessage)
        {
            if (string.IsNullOrWhiteSpace(logMessage)) return;

            string messageOnly = ExtractMessageFromLog(logMessage);
            LogLevel level = DetermineLogLevel(messageOnly);

            _pendingLogs.Enqueue(new LogEntry
            {
                Message = messageOnly,
                Level = level,
                FullMessage = logMessage,
                Timestamp = DateTime.Now
            });

            // Force immediate update for errors
            if (level == LogLevel.Error && !_isUpdating)
            {
                Dispatcher.BeginInvoke(new Action(() => ProcessBatchedLogs(null, null)),
                    DispatcherPriority.Send);
            }
        }

        private void ProcessBatchedLogs(object sender, EventArgs e)
        {
            if (_isUpdating || _pendingLogs.IsEmpty) return;

            _isUpdating = true;

            try
            {
                // Check scroll position before updates
                bool shouldAutoScroll = !_isUserScrolling && IsScrolledToBottom();

                // Process up to BATCH_UPDATE_THRESHOLD logs
                int processed = 0;
                while (processed < BATCH_UPDATE_THRESHOLD && _pendingLogs.TryDequeue(out LogEntry entry))
                {
                    WriteConsoleLineDirect(entry.Message, entry.Level, entry.FullMessage);
                    processed++;
                }

                UpdateStatusBar();

                // Auto-scroll if appropriate
                if (shouldAutoScroll && ConsoleOutput != null)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_isUserScrolling)
                            ConsoleOutput.ScrollToEnd();
                    }), DispatcherPriority.ContextIdle);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void WriteConsoleLineDirect(string message, LogLevel level, string fullLogMessage)
        {
            if (ConsoleOutput == null) return;

            var document = ConsoleOutput.Document;

            var para = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 1),
                LineHeight = 12
            };

            // Extract timestamp
            string timestamp = ExtractTimestamp(fullLogMessage);

            para.Inlines.Add(new Run($"[{timestamp}] ")
            {
                Foreground = ConsoleTimestamp,
                FontSize = 11
            });

            // Get formatting
            var (icon, color, weight) = GetLogFormatting(level);

            if (!string.IsNullOrEmpty(icon))
            {
                para.Inlines.Add(new Run(icon)
                {
                    Foreground = color,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11
                });
            }

            para.Inlines.Add(new Run(message)
            {
                Foreground = color,
                FontWeight = weight,
                FontSize = 11
            });

            document.Blocks.Add(para);

            // Trim excess lines
            while (document.Blocks.Count > MAX_LOG_LINES)
            {
                document.Blocks.Remove(document.Blocks.FirstBlock);
            }
        }

        // This method maintains backward compatibility with your existing code
        private void WriteConsoleLineColored(string message, LogLevel level = LogLevel.Default, string fullLogMessage = null)
        {
            // Queue the log for batch processing instead of direct write
            _pendingLogs.Enqueue(new LogEntry
            {
                Message = message,
                Level = level,
                FullMessage = fullLogMessage ?? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}",
                Timestamp = DateTime.Now
            });
        }

        #endregion

        #region Helper Methods

        private bool IsScrolledToBottom()
        {
            if (ConsoleOutput == null) return true;

            double verticalOffset = ConsoleOutput.VerticalOffset;
            double viewportHeight = ConsoleOutput.ViewportHeight;
            double extentHeight = ConsoleOutput.ExtentHeight;

            const double threshold = 10.0;
            return extentHeight <= viewportHeight ||
                   verticalOffset + viewportHeight >= extentHeight - threshold;
        }

        private string ExtractMessageFromLog(string logMessage)
        {
            if (string.IsNullOrWhiteSpace(logMessage))
                return string.Empty;

            int bracketEnd = logMessage.IndexOf(']');
            if (bracketEnd > 0 && bracketEnd < logMessage.Length - 1)
            {
                return logMessage.Substring(bracketEnd + 1).Trim();
            }
            return logMessage;
        }

        private string ExtractTimestamp(string fullLogMessage)
        {
            if (string.IsNullOrEmpty(fullLogMessage))
                return DateTime.Now.ToString("HH:mm:ss");

            int start = fullLogMessage.IndexOf('[');
            int end = fullLogMessage.IndexOf(']');

            if (start >= 0 && end > start)
            {
                string fullTimestamp = fullLogMessage.Substring(start + 1, end - start - 1);
                var parts = fullTimestamp.Split(' ');
                return parts.Length > 1 ? parts[1] : DateTime.Now.ToString("HH:mm:ss");
            }

            return DateTime.Now.ToString("HH:mm:ss");
        }

        private LogLevel DetermineLogLevel(string message)
        {
            if (string.IsNullOrEmpty(message))
                return LogLevel.Default;

            string lowerMessage = message.ToLowerInvariant();

            if (lowerMessage.Contains("error") || lowerMessage.Contains("failed") || lowerMessage.Contains("exception"))
                return LogLevel.Error;
            if (lowerMessage.Contains("warning") || lowerMessage.Contains("warn") || lowerMessage.Contains("skip"))
                return LogLevel.Warning;
            if (lowerMessage.Contains("success") || lowerMessage.Contains("saved") ||
                lowerMessage.Contains("printed") || lowerMessage.Contains("completed") ||
                lowerMessage.Contains("started"))
                return LogLevel.Success;
            if (lowerMessage.Contains("fetching") || lowerMessage.Contains("loading") ||
                lowerMessage.Contains("processing"))
                return LogLevel.Info;
            if (lowerMessage.Contains("debug") || lowerMessage.Contains("trace"))
                return LogLevel.Debug;

            return LogLevel.Default;
        }

        private (string icon, SolidColorBrush color, FontWeight weight) GetLogFormatting(LogLevel level)
        {
            return level switch
            {
                LogLevel.Error => ("✗ ", ConsoleError, FontWeights.Bold),
                LogLevel.Warning => ("⚠ ", ConsoleWarning, FontWeights.Normal),
                LogLevel.Success => ("✓ ", ConsoleSuccess, FontWeights.Normal),
                LogLevel.Info => ("ℹ ", ConsoleInfo, FontWeights.Normal),
                LogLevel.Debug => ("» ", ConsoleDebug, FontWeights.Normal),
                _ => ("  ", ConsoleForeground, FontWeights.Normal)
            };
        }

        private void UpdateStatusBar(int? lineCount = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (lineCount == null && ConsoleOutput != null)
                {
                    lineCount = ConsoleOutput.Document.Blocks.Count;
                }

                if (LineCountText != null)
                    LineCountText.Text = $"Lines: {lineCount}";

                if (BottomStatusText != null)
                {
                    BottomStatusText.Text = isPollingPaused ? "Paused" : "Running";
                }
            });
        }

        #endregion

        #region API Polling

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
                if (string.IsNullOrWhiteSpace(settings.ApiUrl) ||
                    string.IsNullOrWhiteSpace(settings.UserPin))
                {
                    WriteConsoleLineColored("API settings missing. Skipping request.", LogLevel.Warning);
                    return;
                }

                WriteConsoleLineColored("Fetching labels from API...", LogLevel.Info);
                var labelFiles = await ApiHelper.FetchAllLabelsAsync(settings.ApiUrl, settings.UserPin);

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
                        WriteConsoleLineColored($"Skip: {labelFile.FileName} (already processed)",
                            LogLevel.Warning);
                        continue;
                    }

                    if (settings.SaveToFolder && !string.IsNullOrWhiteSpace(settings.PathToSavePdf))
                    {
                        try
                        {
                            string savedFile = PdfHelper.SavePdf(labelFile.Data,
                                settings.PathToSavePdf, labelFile.FileName);
                            if (!string.IsNullOrEmpty(savedFile))
                            {
                                WriteConsoleLineColored($"Saved: {labelFile.FileName}", LogLevel.Success);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteConsoleLineColored($"Save failed: {labelFile.FileName} - {ex.Message}",
                                LogLevel.Error);
                        }
                    }

                    if (settings.DirectPrint)
                    {
                        try
                        {
                            PrintHelper.PrintPdf(labelFile.Data, settings.SelectedPrinter);
                            WriteConsoleLineColored($"Printed: {labelFile.FileName} → {settings.SelectedPrinter}",
                                LogLevel.Success);
                        }
                        catch (Exception ex)
                        {
                            WriteConsoleLineColored($"Print failed: {labelFile.FileName} - {ex.Message}",
                                LogLevel.Error);
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
                WriteConsoleLineColored($"Polling restarted - Interval: {settings.PollingDurationSeconds}s",
                    LogLevel.Success);
            }
            else
            {
                WriteConsoleLineColored($"Timer updated - Interval: {settings.PollingDurationSeconds}s (paused)",
                    LogLevel.Info);
            }
        }

        #endregion

        #region Event Handlers

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
            _pendingLogs.Clear();
            _isUserScrolling = false;

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

            // Stop all timers
            clockTimer?.Stop();
            _batchUpdateTimer?.Stop();
            _memoryCleanupTimer?.Stop();
            apiTimer?.Stop();
            apiTimer?.Dispose();

            Logger.LogWritten -= OnLogWritten;

            if (_scrollViewer != null)
                _scrollViewer.ScrollChanged -= OnScrollViewerScrollChanged;

            WriteConsoleLineColored("Goodbye!", LogLevel.Success);

            base.OnClosed(e);
        }

        #endregion

        #region Public Console Methods

        public void ScrollToBottom()
        {
            _isUserScrolling = false;
            ConsoleOutput?.ScrollToEnd();
        }

        public void PauseAutoScroll()
        {
            _isUserScrolling = true;
        }

        public void ResumeAutoScroll()
        {
            _isUserScrolling = false;
            ScrollToBottom();
        }

        #endregion
    }
}