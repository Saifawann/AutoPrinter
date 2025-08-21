using AutoPrinter.Helpers;
using Microsoft.Win32;
using System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Printing;
using System.Reflection;
using System.Timers;
using System.Windows;


namespace AutoPrinter
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadLogs();

        }

        private void LoadLogs()
        {
            var logs = Logger.ReadAll();
            TxtLogs.Text = string.Join(Environment.NewLine, logs);

            // Subscribe to future log events
            Logger.LogWritten += OnLogWritten;

        }


        private void OnLogWritten(string logMessage)
        {
            // Ensure update happens on UI thread
            Dispatcher.Invoke(() =>
            {
                TxtLogs.AppendText(logMessage + Environment.NewLine);
                TxtLogs.ScrollToEnd(); // Auto-scroll
            });
        }

        private void AddLog(string message)
        {
            Logger.Write(message);
            TxtLogs.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            TxtLogs.ScrollToEnd();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            // Open Settings window
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this; // Makes it a child of MainWindow
            settingsWindow.ShowDialog(); // Opens as a modal dialog
        }
    }
}
