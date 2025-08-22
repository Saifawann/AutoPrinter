using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoPrinter.Models;
using AutoPrinter.Helpers;
using System.Printing;
using Microsoft.Win32;
using System.IO;


namespace AutoPrinter
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private AppSettings settings;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
            LoadPrinters();
        }

        private void LoadSettings()
        {
            settings = SettingsManager.Load();

            // === General ===
            DirectPrint.IsChecked = settings.DirectPrint;
            ChkSaveToFolder.IsChecked = settings.SaveToFolder;
            TxtPathToSavePdf.Text = settings.PathToSavePdf;

            // === API ===
            DurationBox.Text = settings.PollingDurationSeconds.ToString();
            UrlBox.Text = settings.ApiUrl;
            PinBox.Password = settings.UserPin;

            // === Printing ===
            CmbPrinters.SelectedItem = settings.SelectedPrinter;
            RbPrintAll.IsChecked = settings.PageRange == "All";
            RbPrintRange.IsChecked = settings.PageRange == "Range";
            TxtPagesToPrint.Text = settings.PagesToPrint;
            TxtCopies.Text = settings.Copies.ToString();
            CmbCollation.Text = settings.Collation;
            CmbScaling.Text = settings.Scaling;
            ChkAutoPortrait.IsChecked = settings.AutoPortrait;
            CmbEmailAttach.Text = settings.EmailAttachments;
            ChkPrintCoverPage.IsChecked = settings.PrintCoverPage;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = false; // allow selecting non-existent file
            dialog.FileName = "Select folder"; // placeholder name
            dialog.ValidateNames = false;    // ignore file name validation
            dialog.Title = "Select folder to save PDF";

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                // Get the folder path from the selected "file"
                string folderPath = Path.GetDirectoryName(dialog.FileName);
                TxtPathToSavePdf.Text = folderPath;
            }
        }
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // === General ===
            settings.DirectPrint = DirectPrint.IsChecked == true;
            settings.SaveToFolder = ChkSaveToFolder.IsChecked == true;
            settings.PathToSavePdf = TxtPathToSavePdf.Text;

            // === API ===
            settings.PollingDurationSeconds = GetCurrentPollingDuration();
            settings.ApiUrl = UrlBox.Text;
            settings.UserPin = PinBox.Password;

            // === Printing ===
            settings.SelectedPrinter = CmbPrinters.SelectedItem?.ToString() ?? "";
            settings.PageRange = RbPrintAll.IsChecked == true ? "All" : "Range";
            settings.PagesToPrint = TxtPagesToPrint.Text;
            settings.Copies = int.TryParse(TxtCopies.Text, out int copies) ? copies : 1;
            settings.Collation = CmbCollation.Text;
            settings.Scaling = CmbScaling.Text;
            settings.AutoPortrait = ChkAutoPortrait.IsChecked == true;
            settings.EmailAttachments = CmbEmailAttach.Text;
            settings.PrintCoverPage = ChkPrintCoverPage.IsChecked == true;



            SettingsManager.Save(settings);
            AutoPrinter.Helpers.Logger.Write("Settings updated successfully.");

            if (Owner is MainWindow mainWindow)
            {
                mainWindow.OnSettingsUpdated(settings); // Pass the updated settings
            }


            this.Close();
        }

        private int GetCurrentPollingDuration()
        {
            if (int.TryParse(DurationBox.Text, out int duration) && duration > 0)
            {
                return duration;
            }
            return 30; // Default fallback
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // cancels
            this.Close();
        }

        private void LoadPrinters()
        {
            try
            {
                List<string> printers = new List<string>();
                LocalPrintServer printServer = new LocalPrintServer();

                foreach (PrintQueue queue in printServer.GetPrintQueues())
                {
                    printers.Add(queue.Name);
                }

                CmbPrinters.ItemsSource = printers;

                // Try to select the saved printer if it exists
                if (!string.IsNullOrEmpty(settings.SelectedPrinter) &&
                    printers.Contains(settings.SelectedPrinter))
                {
                    CmbPrinters.SelectedItem = settings.SelectedPrinter;
                }
                else if (printers.Count > 0)
                {
                    // Default: first printer in list
                    CmbPrinters.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading printers: {ex.Message}");
            }
        }

    }
}
