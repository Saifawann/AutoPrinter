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
using System.Windows.Shapes;
using AutoPrinter.Models;
using AutoPrinter.Helpers;
using System.Printing;


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
        }
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // === General ===
            settings.DirectPrint = DirectPrint.IsChecked == true;
            settings.SaveToFolder = ChkSaveToFolder.IsChecked == true;
            settings.PathToSavePdf = TxtPathToSavePdf.Text;

            // === API ===
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

            MessageBox.Show("Settings saved successfully!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
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
