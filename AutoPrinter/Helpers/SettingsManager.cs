using System.IO;
using System.Xml.Serialization;
using AutoPrinter.Models;

namespace AutoPrinter.Helpers
{
    public static class SettingsManager
    {
        private static string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AutoPrinter",
            "settings.xml"
        );

        public static void Save(AppSettings settings)
        {
            string? folder = Path.GetDirectoryName(settingsPath);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder!); // ensure folder exists
            }

            XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
            using (var writer = new StreamWriter(settingsPath))
            {
                serializer.Serialize(writer, settings);
            }
        }

        public static AppSettings Load()
        {
            if (!File.Exists(settingsPath))
            {
                AppSettings defaultSettings = new AppSettings();
                Save(defaultSettings); // create file on first run
                return defaultSettings;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
            using (StreamReader reader = new StreamReader(settingsPath))
            {
                return (AppSettings)serializer.Deserialize(reader);
            }
        }
    }
}
