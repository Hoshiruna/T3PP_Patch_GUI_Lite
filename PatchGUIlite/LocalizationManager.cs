using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PatchGUIlite
{
    internal static class LocalizationManager
    {
        private const string DefaultLang = "zh_CN";
        private static readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
        public static string CurrentLanguage { get; private set; } = DefaultLang;

        public static void LoadLanguage(string langCode)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(baseDir, "lang", $"{langCode}.json");
                if (!File.Exists(path))
                {
                    if (!langCode.Equals(DefaultLang, StringComparison.OrdinalIgnoreCase))
                    {
                        LoadLanguage(DefaultLang);
                    }
                    return;
                }

                string json = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();

                _strings.Clear();
                foreach (var kv in parsed)
                {
                    _strings[kv.Key] = kv.Value;
                }

                CurrentLanguage = langCode;
            }
            catch
            {
                // ignore localization load failures
            }
        }

        public static string Get(string key, string fallback)
        {
            if (_strings.TryGetValue(key, out var value))
                return value;
            return fallback;
        }
    }
}

