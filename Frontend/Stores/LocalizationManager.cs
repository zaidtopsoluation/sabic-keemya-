using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace Keemya.Frontend.Stores
{
    public static class LocalizationManager
    {
        private static readonly string LanguageFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language.txt");
        public static string CurrentLanguage { get; private set; } = "en";

        public static void Initialize()
        {
            // Read persisted language
            string lang = "en";
            try
            {
                if (File.Exists(LanguageFilePath))
                {
                    string saved = File.ReadAllText(LanguageFilePath).Trim().ToLower();
                    if (saved == "ar" || saved == "en")
                    {
                        lang = saved;
                    }
                }
            }
            catch { }

            SetLanguage(lang);
        }

        public static void SetLanguage(string lang)
        {
            CurrentLanguage = lang;

            // Persist choice
            try
            {
                File.WriteAllText(LanguageFilePath, lang);
            }
            catch { }

            // 1. Load Resource Dictionary
            string resourcePath = $"/Resources/StringResources.{lang}.xaml";
            var dict = new ResourceDictionary
            {
                Source = new Uri(resourcePath, UriKind.RelativeOrAbsolute)
            };

            // 2. Remove old localization dictionaries and add new one
            var merged = Application.Current.Resources.MergedDictionaries;
            var oldDicts = merged.Where(d => d.Source != null && d.Source.OriginalString.Contains("StringResources.")).ToList();
            foreach (var old in oldDicts)
            {
                merged.Remove(old);
            }
            merged.Add(dict);

            // 3. Update FlowDirection on all open Windows (RTL for Arabic, LTR for English!)
            FlowDirection flowDir = lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            foreach (Window window in Application.Current.Windows)
            {
                window.FlowDirection = flowDir;
            }
        }
    }
}
