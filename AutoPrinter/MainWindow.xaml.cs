using AutoPrinter.Helpers;
using AutoPrinter.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace AutoPrinter
{
    public partial class MainWindow : Window
    {
        private System.Timers.Timer apiTimer;
        private AppSettings settings;

        public MainWindow()
        {
            InitializeComponent();
            LoadLogs();
            StartApiPolling();
        }

        private void StartApiPolling()
        {
            apiTimer = new System.Timers.Timer(30_000); // every 30 seconds
            apiTimer.Elapsed += async (s, e) => await FetchAndSaveLabel();
            apiTimer.AutoReset = true;
            apiTimer.Start();

            Logger.Write("Started API polling every 30 seconds.");
        }

        private async Task FetchAndSaveLabel()
        {
            try
            {
                settings = SettingsManager.Load();
                if (string.IsNullOrWhiteSpace(settings.ApiUrl) || string.IsNullOrWhiteSpace(settings.UserPin))
                {
                    Logger.Write("API settings missing. Skipping request.");
                    return;
                }

                // === Step 1: Call API ===
                var labelFile = await ApiHelper.FetchLabelAsync(settings.ApiUrl, settings.UserPin);
                if (labelFile == null || labelFile.Data == null || labelFile.Data.Length == 0)
                {
                    Logger.Write("API returned empty label.");
                    return;
                }

                // === Step 2: Save to folder (if enabled) ===
                if (settings.SaveToFolder && !string.IsNullOrWhiteSpace(settings.PathToSavePdf))
                {
                    string savedFile = PdfHelper.SavePdf(labelFile.Data, settings.PathToSavePdf, labelFile.FileName);
                }

                // === Step 3: Direct Print (if enabled) ===
                if (settings.DirectPrint)
                {
                    PrintHelper.PrintPdf(labelFile.Data, settings.SelectedPrinter);
                    Logger.Write($"PDF sent to printer: {settings.SelectedPrinter}");
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error in API cycle: {ex.Message}");
            }
        }


        private void LoadLogs()
        {
            var logs = Logger.ReadAll();
            TxtLogs.Text = string.Join(Environment.NewLine, logs);
            TxtLogs.ScrollToEnd();
            Logger.LogWritten += OnLogWritten;
        }

        private void OnLogWritten(string logMessage)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLogs.AppendText(logMessage + Environment.NewLine);
                TxtLogs.ScrollToEnd();
            });
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
    }
}
