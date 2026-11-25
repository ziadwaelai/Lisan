using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Lisan
{
    internal class SettingsForm : Form
    {
        readonly TextBox apiKey, model, arabicModel;
        readonly HotkeyBox translateKey, rewriteKey;
        readonly ComboBox arabicStyle, rewriteStyle;
        readonly CheckBox startup;

        static readonly string[] ArabicValues = { "Egyptian", "Standard", "Gulf" };
        static readonly string[] ArabicLabels = { "Egyptian (مصري)", "Modern Standard (فصحى)", "Gulf / Saudi (خليجي)" };
        public static readonly string[] RewriteValues = { "Natural", "Professional", "Casual", "Shorter" };

        public SettingsForm()
        {
            Text = "Lisan settings";
            Font = Ui.Font(9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.Bg;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Ui.S(18));
            Icon = IconArt.MakeIcon(32);
            TopMost = true;

            var s = Program.Settings;
            var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Ui.S(330)));

            var title = new Label { Text = "Translate & fix text anywhere", Font = Ui.Font(13f, FontStyle.Bold), AutoSize = true, ForeColor = Ui.Text, UseMnemonic = false, Margin = new Padding(0, 0, 0, Ui.S(2)) };
            grid.Controls.Add(title, 0, 0); grid.SetColumnSpan(title, 2);
            var sub = new Label
            {
                Text = "Select text in any app, then press a hotkey. Lisan uses Google Gemini (free).",
                AutoSize = true, ForeColor = Ui.Muted, MaximumSize = new Size(Ui.S(480), 0), Margin = new Padding(0, 0, 0, Ui.S(14)),
            };
            grid.Controls.Add(sub, 0, 1); grid.SetColumnSpan(sub, 2);
            grid.RowCount = 2;

            apiKey = new TextBox { Text = s.ApiKey, Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            AddRow(grid, "Gemini API key", apiKey);
            var link = new LinkLabel { Text = "Get a free key from Google AI Studio  ↗", AutoSize = true, Margin = new Padding(Ui.S(3), 0, 0, Ui.S(10)), LinkColor = Ui.Accent };
            link.LinkClicked += delegate { Process.Start("https://aistudio.google.com/apikey"); };
            var show = new CheckBox { Text = "Show key", AutoSize = true, Margin = new Padding(Ui.S(12), 0, 0, 0), ForeColor = Ui.Muted };
            show.CheckedChanged += delegate { apiKey.UseSystemPasswordChar = !show.Checked; };
            var linkRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            linkRow.Controls.Add(link); linkRow.Controls.Add(show);
            AddRow(grid, "", linkRow);

            translateKey = new HotkeyBox { Text = s.TranslateHotkey, Dock = DockStyle.Fill };
            AddRow(grid, "Translate hotkey", translateKey);
            rewriteKey = new HotkeyBox { Text = s.RewriteHotkey, Dock = DockStyle.Fill };
            AddRow(grid, "Fix my writing hotkey", rewriteKey);
            var hint = new Label { Text = "Click a box and press the keys you want.", AutoSize = true, ForeColor = Ui.Muted, Margin = new Padding(Ui.S(3), 0, 0, Ui.S(10)) };
            AddRow(grid, "", hint);

            arabicStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            arabicStyle.Items.AddRange(ArabicLabels);
            arabicStyle.SelectedIndex = Math.Max(0, Array.IndexOf(ArabicValues, s.ArabicStyle));
            AddRow(grid, "Translate to Arabic as", arabicStyle);

            rewriteStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            rewriteStyle.Items.AddRange(RewriteValues);
            rewriteStyle.SelectedIndex = Math.Max(0, Array.IndexOf(RewriteValues, s.RewriteStyle));
            AddRow(grid, "Rewrite style", rewriteStyle);

            model = new TextBox { Text = s.Model, Dock = DockStyle.Fill };
            AddRow(grid, "Main model", model);
            arabicModel = new TextBox { Text = s.ArabicModel, Dock = DockStyle.Fill };
            AddRow(grid, "Model for Arabic", arabicModel);

            startup = new CheckBox { Text = "Start Lisan when Windows starts", AutoSize = true, Checked = Settings.StartWithWindows, Margin = new Padding(Ui.S(3), Ui.S(8), 0, Ui.S(8)) };
            AddRow(grid, "", startup);

            var note = new Label
            {
                Text = "Privacy: the text you translate or fix is sent to Google. On the free tier Google may use it to improve its products, so avoid secrets, passwords and customer data.",
                AutoSize = true, ForeColor = Ui.Muted, Font = Ui.Font(8.5f), MaximumSize = new Size(Ui.S(480), 0), Margin = new Padding(0, Ui.S(4), 0, Ui.S(14)),
            };
            int r = grid.RowCount++;
            grid.Controls.Add(note, 0, r); grid.SetColumnSpan(note, 2);

            var save = Ui.MakeButton("Save", true);
            var cancel = Ui.MakeButton("Cancel", false);
            save.Click += delegate { SaveAndClose(); };
            cancel.Click += delegate { Close(); };
            AcceptButton = save; CancelButton = cancel;
            var btnRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = Padding.Empty };
            cancel.Margin = Padding.Empty;
            btnRow.Controls.Add(cancel); btnRow.Controls.Add(save);
            r = grid.RowCount++;
            grid.Controls.Add(btnRow, 0, r); grid.SetColumnSpan(btnRow, 2);

            Controls.Add(grid);
        }

        static void AddRow(TableLayoutPanel grid, string label, Control c)
        {
            int r = grid.RowCount++;
            if (label != "")
                grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Ui.Text, Margin = new Padding(0, Ui.S(4), Ui.S(14), Ui.S(4)) }, 0, r);
            if (c.Margin == new Padding(3)) c.Margin = new Padding(Ui.S(3), Ui.S(4), 0, Ui.S(4));
            grid.Controls.Add(c, 1, r);
        }

        void SaveAndClose()
        {
            uint m; Keys k;
            if (!HotkeyBox.TryParse(translateKey.Text, out m, out k) || !HotkeyBox.TryParse(rewriteKey.Text, out m, out k))
            {
                MessageBox.Show(this, "Each hotkey needs at least one of Ctrl / Alt / Win plus a key.", "Lisan");
                return;
            }
            if (translateKey.Text == rewriteKey.Text)
            {
                MessageBox.Show(this, "The two hotkeys must be different.", "Lisan");
                return;
            }
            var s = Program.Settings;
            s.ApiKey = apiKey.Text.Trim();
            s.Model = model.Text.Trim() == "" ? "gemini-3.5-flash-lite" : model.Text.Trim();
            s.ArabicModel = arabicModel.Text.Trim() == "" ? "gemini-3.1-flash-lite" : arabicModel.Text.Trim();
            s.TranslateHotkey = translateKey.Text;
            s.RewriteHotkey = rewriteKey.Text;
            s.ArabicStyle = ArabicValues[arabicStyle.SelectedIndex];
            s.RewriteStyle = RewriteValues[rewriteStyle.SelectedIndex];
            try { s.Save(); Settings.StartWithWindows = startup.Checked; }
            catch (Exception ex) { MessageBox.Show(this, "Could not save settings: " + ex.Message, "Lisan"); return; }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// A textbox that records a key combination when you press it.
    internal class HotkeyBox : TextBox
    {
        public HotkeyBox() { ReadOnly = true; BackColor = Color.White; Cursor = Cursors.Hand; ShortcutsEnabled = false; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Tab || key == Keys.Escape || key == Keys.Enter) return base.ProcessCmdKey(ref msg, keyData);
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin) return true;
            bool ctrl = (keyData & Keys.Control) != 0, alt = (keyData & Keys.Alt) != 0, shift = (keyData & Keys.Shift) != 0;
            if (!ctrl && !alt) return true; // require a real modifier
            string s = (ctrl ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") + KeyName(key);
            Text = s;
            return true;
        }

        static string KeyName(Keys k)
        {
            if (k >= Keys.D0 && k <= Keys.D9) return ((int)(k - Keys.D0)).ToString();
            return k.ToString();
        }

        public static bool TryParse(string text, out uint mods, out Keys key)
        {
            mods = 0; key = Keys.None;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (i < parts.Length - 1)
                {
                    switch (p.ToLowerInvariant())
                    {
                        case "ctrl": case "control": mods |= Native.MOD_CONTROL; break;
                        case "alt": mods |= Native.MOD_ALT; break;
                        case "shift": mods |= Native.MOD_SHIFT; break;
                        case "win": mods |= Native.MOD_WIN; break;
                        default: return false;
                    }
                }
                else
                {
                    if (p.Length == 1 && char.IsDigit(p[0])) p = "D" + p;
                    try { key = (Keys)Enum.Parse(typeof(Keys), p, true); }
                    catch { return false; }
                }
            }
            return key != Keys.None && (mods & (Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_WIN)) != 0;
        }
    }
}
