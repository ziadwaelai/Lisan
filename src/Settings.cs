using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Lisan
{
    internal class Settings
    {
        public string ApiKey = "";
        public string Model = "gemini-3.5-flash-lite";        // fast: rewriting + Arabic->English
        public string ArabicModel = "gemini-3.1-flash-lite"; // cleaner Egyptian Arabic
        public string TranslateHotkey = "Ctrl+Alt+T";
        public string RewriteHotkey = "Ctrl+Alt+R";
        public string ArabicStyle = "Egyptian";   // Egyptian | Standard | Gulf
        public string RewriteStyle = "Natural";   // Natural | Professional | Casual | Shorter

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lisan"); }
        }
        static string FilePath { get { return Path.Combine(Folder, "settings.ini"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) map[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
                string v;
                if (map.TryGetValue("ApiKey", out v)) s.ApiKey = v;
                if (map.TryGetValue("Model", out v) && v != "") s.Model = v;
                if (map.TryGetValue("ArabicModel", out v) && v != "") s.ArabicModel = v;
                if (map.TryGetValue("TranslateHotkey", out v) && v != "") s.TranslateHotkey = v;
                if (map.TryGetValue("RewriteHotkey", out v) && v != "") s.RewriteHotkey = v;
                if (map.TryGetValue("ArabicStyle", out v) && v != "") s.ArabicStyle = v;
                if (map.TryGetValue("RewriteStyle", out v) && v != "") s.RewriteStyle = v;
            }
            catch { }
            return s;
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder);
            var sb = new StringBuilder();
            sb.AppendLine("ApiKey=" + ApiKey);
            sb.AppendLine("Model=" + Model);
            sb.AppendLine("ArabicModel=" + ArabicModel);
            sb.AppendLine("TranslateHotkey=" + TranslateHotkey);
            sb.AppendLine("RewriteHotkey=" + RewriteHotkey);
            sb.AppendLine("ArabicStyle=" + ArabicStyle);
            sb.AppendLine("RewriteStyle=" + RewriteStyle);
            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
        }

        // ---- Start with Windows ----
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool StartWithWindows
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("Lisan") != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue("Lisan", "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                    else k.DeleteValue("Lisan", false);
                }
            }
        }
    }
}
