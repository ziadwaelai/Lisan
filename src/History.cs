using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Lisan
{
    internal class HistoryItem
    {
        public string Source { get; set; }
        public string Result { get; set; }
        public DateTime Time { get; set; }
    }

    /// Last 20 translations and last 20 fixes, newest first. Saved only on this PC (%APPDATA%\Lisan\history.json).
    internal class History
    {
        public const int Max = 20;
        public List<HistoryItem> Translations { get; set; }
        public List<HistoryItem> Fixes { get; set; }

        public History() { Translations = new List<HistoryItem>(); Fixes = new List<HistoryItem>(); }

        static string FilePath { get { return Path.Combine(Settings.Folder, "history.json"); } }

        public static History Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var h = new JavaScriptSerializer().Deserialize<History>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (h != null)
                    {
                        if (h.Translations == null) h.Translations = new List<HistoryItem>();
                        if (h.Fixes == null) h.Fixes = new List<HistoryItem>();
                        return h;
                    }
                }
            }
            catch { } // a damaged file just starts a fresh history
            return new History();
        }

        public void AddTranslation(string source, string result) { Add(Translations, source, result); }
        public void AddFix(string source, string result) { Add(Fixes, source, result); }

        void Add(List<HistoryItem> list, string source, string result)
        {
            source = source.Trim(); result = result.Trim();
            // The same text again moves to the top instead of appearing twice.
            list.RemoveAll(i => i.Source == source);
            list.Insert(0, new HistoryItem { Source = source, Result = result, Time = DateTime.Now });
            if (list.Count > Max) list.RemoveRange(Max, list.Count - Max);
            Save();
        }

        public void Clear() { Translations.Clear(); Fixes.Clear(); Save(); }

        void Save()
        {
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
