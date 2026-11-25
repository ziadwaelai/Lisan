using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Lisan
{
    internal static class Program
    {
        public static Settings Settings;

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "Lisan.TranslateAndFix.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("Lisan is already running. Look for the ع icon in the tray (bottom-right).", "Lisan");
                    return;
                }
                try { SetProcessDPIAware(); } catch { }
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Settings = Settings.Load();
                Application.Run(new TrayApp());
            }
        }
    }

    internal class TrayApp : ApplicationContext
    {
        const int TranslateId = 1, RewriteId = 2, HistoryId = 3;

        public readonly Gemini Gemini;
        public readonly History History = History.Load();
        readonly NotifyIcon tray;
        readonly HotkeyWindow hotkeys;
        TranslatePopup popup;
        HistoryPopup historyPopup;
        bool busy;

        public TrayApp()
        {
            Gemini = new Gemini(Program.Settings);
            hotkeys = new HotkeyWindow();
            hotkeys.Pressed += OnHotkey;

            tray = new NotifyIcon
            {
                Icon = IconArt.MakeIcon(SystemInformation.SmallIconSize.Width),
                Text = "Lisan – translate & fix text",
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip(),
            };
            tray.ContextMenuStrip.Opening += delegate { BuildMenu(tray.ContextMenuStrip); };
            BuildMenu(tray.ContextMenuStrip);
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowPopup(IntPtr.Zero, null); };
            tray.BalloonTipClicked += delegate { OpenSettings(); };

            bool firstRun = string.IsNullOrWhiteSpace(Program.Settings.ApiKey);
            RegisterHotkeys(!firstRun);

            // Run after the message loop starts.
            var t = new Timer { Interval = 400 };
            t.Tick += delegate
            {
                t.Stop(); t.Dispose();
                if (firstRun) { if (OpenSettings()) ShowWelcome(); }
            };
            t.Start();
        }

        void ShowWelcome()
        {
            var s = Program.Settings;
            tray.ShowBalloonTip(8000, "Lisan is ready",
                "Select text anywhere, then:\n" + s.TranslateHotkey + "  →  translate\n" + s.RewriteHotkey + "  →  fix my writing\n" + s.HistoryHotkey + "  →  history",
                ToolTipIcon.Info);
        }

        void BuildMenu(ContextMenuStrip menu)
        {
            var s = Program.Settings;
            menu.Items.Clear();
            menu.Font = Ui.Font(9.5f);

            var tr = new ToolStripMenuItem("Translate selection", null, delegate { ShowPopup(IntPtr.Zero, null); }) { ShortcutKeyDisplayString = s.TranslateHotkey };
            tr.Font = Ui.Font(9.5f, FontStyle.Bold);
            menu.Items.Add(tr);
            menu.Items.Add(new ToolStripMenuItem("Fix my writing", null, delegate
            {
                MessageBox.Show("Select the text you wrote (or just click inside the message box), then press " + s.RewriteHotkey + ".", "Lisan");
            }) { ShortcutKeyDisplayString = s.RewriteHotkey });
            menu.Items.Add(new ToolStripMenuItem("History", null, delegate { ShowHistory(false); }) { ShortcutKeyDisplayString = s.HistoryHotkey });
            menu.Items.Add(new ToolStripSeparator());

            var style = new ToolStripMenuItem("Rewrite style");
            foreach (var v in SettingsForm.RewriteValues)
            {
                string val = v;
                var item = new ToolStripMenuItem(val, null, delegate { s.RewriteStyle = val; TrySave(); }) { Checked = s.RewriteStyle == val };
                style.DropDownItems.Add(item);
            }
            menu.Items.Add(style);
            menu.Items.Add(new ToolStripMenuItem("Settings…", null, delegate { OpenSettings(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, delegate { ExitThread(); }));
        }

        static void TrySave() { try { Program.Settings.Save(); } catch { } }

        bool settingsOpen;
        bool OpenSettings()
        {
            if (settingsOpen) return false;
            settingsOpen = true;
            hotkeys.UnregisterAll(); // so the hotkey boxes can record the current keys
            try
            {
                using (var f = new SettingsForm())
                {
                    bool ok = f.ShowDialog() == DialogResult.OK;
                    RegisterHotkeys(true);
                    return ok;
                }
            }
            finally { settingsOpen = false; }
        }

        void RegisterHotkeys(bool warn)
        {
            hotkeys.UnregisterAll();
            var s = Program.Settings;
            string failed = "";
            if (!hotkeys.Register(TranslateId, s.TranslateHotkey)) failed += s.TranslateHotkey + "  ";
            if (!hotkeys.Register(RewriteId, s.RewriteHotkey)) failed += s.RewriteHotkey + "  ";
            if (!hotkeys.Register(HistoryId, s.HistoryHotkey)) failed += s.HistoryHotkey;
            if (failed != "" && warn)
                tray.ShowBalloonTip(8000, "Hotkey not available",
                    "Another app is already using " + failed.Trim() + ". Click here to choose a different hotkey.", ToolTipIcon.Warning);
        }

        async void OnHotkey(int id)
        {
            if (settingsOpen) return;
            if (id == HistoryId) { ShowHistory(false); return; }
            if (busy) return;
            busy = true;
            try
            {
                if (id == TranslateId) await TranslateFlow();
                else if (id == RewriteId) await RewriteFlow();
            }
            catch (Exception ex) { Toast.Show(ex.Message, Color.FromArgb(242, 139, 130), 5000, false); }
            finally { busy = false; }
        }

        // ---------- Translate ----------
        async Task TranslateFlow()
        {
            IntPtr target = Native.GetForegroundWindow();
            var backup = ClipBackup.Take();
            string text = await CopySelection(false);
            if (text != null) backup.Restore(); // leave the user's clipboard as it was
            ShowPopup(target, text);
        }

        void ShowPopup(IntPtr target, string text)
        {
            if (popup != null && !popup.IsDisposed) popup.Close();
            if (text != null && text.Length > 8000) text = text.Substring(0, 8000);
            popup = new TranslatePopup(this, target, text);
            popup.Show();
        }

        void ShowHistory(bool fixes)
        {
            if (historyPopup != null && !historyPopup.IsDisposed) historyPopup.Close();
            if (popup != null && !popup.IsDisposed) popup.Close();
            historyPopup = new HistoryPopup(History, fixes);
            historyPopup.Show();
        }

        // ---------- Fix my writing ----------
        async Task RewriteFlow()
        {
            IntPtr target = Native.GetForegroundWindow();
            string text = await CopySelection(true);
            if (string.IsNullOrWhiteSpace(text))
            {
                Toast.Show("Select the text you want to fix first.", Color.White, 2500, false);
                return;
            }
            if (text.Length > 6000)
            {
                Toast.Show("That's too much text to rewrite at once. Select a shorter part.", Color.White, 3500, false);
                return;
            }

            Toast.Show("Fixing your writing", Color.White, 0, true);
            string result;
            try { result = await Gemini.Rewrite(text); }
            catch (Exception ex) { Toast.Show(ex.Message, Color.FromArgb(242, 139, 130), 6000, false); return; }

            History.AddFix(text, result);

            // Keep any spaces/new lines that were around the selection.
            string lead = text.Substring(0, text.Length - text.TrimStart().Length);
            string trail = text.Substring(text.TrimEnd().Length);
            result = lead + result + trail;

            if (Native.GetForegroundWindow() != target)
            {
                Clip.SetText(result);
                Toast.Show("Fixed text copied – press Ctrl+V to paste it.", Ui.Success, 4000, false);
                return;
            }
            await PasteIntoAsync(target, result);
            Toast.Show("✓ Fixed  ·  Ctrl+Z to undo", Ui.Success, 2200, false);
        }

        /// Copies the current selection via Ctrl+C. If selectAllIfEmpty, falls back to Ctrl+A (e.g. a chat box).
        static async Task<string> CopySelection(bool selectAllIfEmpty)
        {
            await Native.WaitForModifiersReleased();
            string text = await TryCopy();
            if (string.IsNullOrEmpty(text) && selectAllIfEmpty)
            {
                Native.SendCtrlPlus(0x41); // A
                await Task.Delay(60);
                text = await TryCopy();
            }
            return string.IsNullOrEmpty(text) ? null : text;
        }

        static async Task<string> TryCopy()
        {
            uint seq = Native.GetClipboardSequenceNumber();
            Native.SendCtrlPlus(0x43); // C
            for (int i = 0; i < 35; i++)
            {
                await Task.Delay(20);
                if (Native.GetClipboardSequenceNumber() != seq) break;
            }
            if (Native.GetClipboardSequenceNumber() == seq) return null; // nothing was selected
            await Task.Delay(40);
            return Clip.GetText();
        }

        public void PasteInto(IntPtr target, string text, bool restoreClipboard)
        {
            var _ = PasteIntoAsync(target, text, restoreClipboard);
        }

        static async Task PasteIntoAsync(IntPtr target, string text, bool restoreClipboard = false)
        {
            var backup = restoreClipboard ? ClipBackup.Take() : null;
            if (target != IntPtr.Zero && Native.GetForegroundWindow() != target)
            {
                Native.SetForegroundWindow(target);
                await Task.Delay(120);
            }
            Clip.SetText(text);
            await Native.WaitForModifiersReleased();
            Native.SendCtrlPlus(0x56); // V
            if (backup != null)
            {
                await Task.Delay(500);
                backup.Restore();
            }
        }

        protected override void ExitThreadCore()
        {
            hotkeys.UnregisterAll();
            hotkeys.DestroyHandle();
            tray.Visible = false;
            tray.Dispose();
            base.ExitThreadCore();
        }
    }

    /// Hidden window that receives global hotkey presses.
    internal class HotkeyWindow : NativeWindow
    {
        public event Action<int> Pressed;
        readonly System.Collections.Generic.List<int> ids = new System.Collections.Generic.List<int>();

        public HotkeyWindow() { CreateHandle(new CreateParams()); }

        public bool Register(int id, string hotkey)
        {
            uint mods; Keys key;
            if (!HotkeyBox.TryParse(hotkey, out mods, out key)) return false;
            if (!Native.RegisterHotKey(Handle, id, mods | Native.MOD_NOREPEAT, (uint)key)) return false;
            ids.Add(id);
            return true;
        }

        public void UnregisterAll()
        {
            foreach (int id in ids) Native.UnregisterHotKey(Handle, id);
            ids.Clear();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }

    internal static class Clip
    {
        public static string GetText()
        {
            for (int i = 0; i < 6; i++)
            {
                try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
                catch { Thread.Sleep(30); }
            }
            return null;
        }

        public static void SetText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, text), true, 10, 40); } catch { }
        }
    }

    /// Saves what was on the clipboard (text, image or files) so it can be put back.
    internal class ClipBackup
    {
        string text; Image image; StringCollection files;

        public static ClipBackup Take()
        {
            var b = new ClipBackup();
            try
            {
                if (Clipboard.ContainsText()) b.text = Clipboard.GetText();
                else if (Clipboard.ContainsImage()) b.image = Clipboard.GetImage();
                else if (Clipboard.ContainsFileDropList()) b.files = Clipboard.GetFileDropList();
            }
            catch { }
            return b;
        }

        public void Restore()
        {
            try
            {
                if (text != null) Clip.SetText(text);
                else if (image != null) Clipboard.SetImage(image);
                else if (files != null) Clipboard.SetFileDropList(files);
                else Clipboard.Clear();
            }
            catch { }
        }
    }
}
