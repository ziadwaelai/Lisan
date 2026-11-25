using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lisan
{
    /// Popup with two tabs (Translations / Fixes). Click an item to see it in full; double-click to copy and close.
    internal class HistoryPopup : Form
    {
        readonly History history;
        readonly Label tabT, tabR, closeBtn, empty;
        readonly ListBox list;
        readonly Label detailSource;
        readonly TextBox detailResult;
        readonly FlowLayoutPanel buttons;
        readonly Button copyBtn, copySourceBtn, clearBtn;
        bool showFixes;

        int W { get { return Ui.S(480); } }
        int Pad { get { return Ui.S(14); } }

        public HistoryPopup(History history, bool startOnFixes)
        {
            this.history = history;
            showFixes = startOnFixes;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            BackColor = Ui.Bg;
            Font = Ui.Font(9.5f);
            Text = "Lisan history";

            tabT = MakeTab(); tabT.Click += delegate { SwitchTo(false); };
            tabR = MakeTab(); tabR.Click += delegate { SwitchTo(true); };
            closeBtn = new Label { Text = "✕", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Ui.Muted, Cursor = Cursors.Hand, Font = Ui.Font(10f) };
            closeBtn.Click += delegate { Close(); };
            closeBtn.MouseEnter += delegate { closeBtn.BackColor = Ui.Hover; };
            closeBtn.MouseLeave += delegate { closeBtn.BackColor = Ui.Bg; };

            list = new ListBox
            {
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Ui.S(50), BorderStyle = BorderStyle.None,
                IntegralHeight = false, BackColor = Ui.Bg,
            };
            list.DrawItem += DrawItem;
            list.SelectedIndexChanged += delegate { ShowDetail(); };
            list.DoubleClick += delegate { CopyAndClose(); };
            list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { CopyAndClose(); e.Handled = true; } };

            empty = new Label { ForeColor = Ui.Muted, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false };

            detailSource = new Label { ForeColor = Ui.Muted, AutoSize = false, Font = Ui.Font(9f), AutoEllipsis = true };
            detailResult = new TextBox
            {
                Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Ui.Text, Font = Ui.Font(11f), ScrollBars = ScrollBars.Vertical, TabStop = false,
            };

            buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Ui.Bg };
            copyBtn = Ui.MakeButton("Copy result", true); copyBtn.Click += delegate { CopyAndClose(); };
            copySourceBtn = Ui.MakeButton("Copy original", false); copySourceBtn.Click += delegate { CopySource(); };
            clearBtn = Ui.MakeButton("Clear history", false); clearBtn.Click += delegate { ClearAll(); };
            buttons.Controls.AddRange(new Control[] { copyBtn, copySourceBtn, clearBtn });

            Controls.AddRange(new Control[] { tabT, tabR, closeBtn, list, empty, detailSource, detailResult, buttons });
            SwitchTo(showFixes);
            Location = Ui.PlaceNear(Cursor.Position, Size);
        }

        Label MakeTab()
        {
            var l = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, Font = Ui.Font(9.5f, FontStyle.Bold) };
            l.Paint += (s, e) =>
            {
                bool active = (l == tabR) == showFixes;
                if (active) using (var b = new SolidBrush(Ui.Accent)) e.Graphics.FillRectangle(b, 0, l.Height - Ui.S(3), l.Width, Ui.S(3));
            };
            return l;
        }

        List<HistoryItem> Items { get { return showFixes ? history.Fixes : history.Translations; } }

        void SwitchTo(bool fixes)
        {
            showFixes = fixes;
            tabT.Text = "Translations (" + history.Translations.Count + ")";
            tabR.Text = "Fixes (" + history.Fixes.Count + ")";
            tabT.ForeColor = fixes ? Ui.Muted : Ui.Accent;
            tabR.ForeColor = fixes ? Ui.Accent : Ui.Muted;
            tabT.Invalidate(); tabR.Invalidate();

            list.BeginUpdate();
            list.Items.Clear();
            foreach (var i in Items) list.Items.Add(i);
            list.EndUpdate();

            empty.Text = fixes
                ? "No fixes yet.\nClick in a message you wrote and press " + Program.Settings.RewriteHotkey + "."
                : "No translations yet.\nSelect some text and press " + Program.Settings.TranslateHotkey + ".";
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            ShowDetail();
            Relayout();
        }

        HistoryItem Selected { get { return list.SelectedItem as HistoryItem; } }

        void ShowDetail()
        {
            var i = Selected;
            if (i == null) { detailSource.Text = ""; detailResult.Text = ""; return; }
            detailSource.Text = "Original:  " + OneLine(i.Source);
            detailResult.RightToLeft = Gemini.LooksArabic(i.Result) ? RightToLeft.Yes : RightToLeft.No;
            detailResult.Text = i.Result.Replace("\r\n", "\n").Replace("\n", "\r\n");
            // Scrollbar only when the text doesn't fit.
            int textH = TextRenderer.MeasureText(detailResult.Text, detailResult.Font, new Size(W - Pad * 2 - Ui.S(8), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
            detailResult.ScrollBars = textH > Ui.S(84) ? ScrollBars.Vertical : ScrollBars.None;
        }

        static string OneLine(string s) { return s.Replace("\r", " ").Replace("\n", " "); }

        static string Ago(DateTime t)
        {
            if (t.Kind == DateTimeKind.Utc) t = t.ToLocalTime(); // saved times come back as UTC
            var d = DateTime.Now - t;
            if (d.TotalMinutes < 1) return "just now";
            if (d.TotalHours < 1) return (int)d.TotalMinutes + " min ago";
            if (d.TotalDays < 1) return (int)d.TotalHours + " h ago";
            if (d.TotalDays < 7) return (int)d.TotalDays + " d ago";
            return t.ToString("d MMM");
        }

        void DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var item = (HistoryItem)list.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(sel ? Color.FromArgb(232, 240, 254) : Ui.Bg)) e.Graphics.FillRectangle(bg, e.Bounds);
            if (sel) using (var bar = new SolidBrush(Ui.Accent)) e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y, Ui.S(3), e.Bounds.Height);

            int x = e.Bounds.X + Ui.S(10), w = e.Bounds.Width - Ui.S(20);
            var r1 = new Rectangle(x, e.Bounds.Y + Ui.S(5), w, Ui.S(22));
            var r2 = new Rectangle(x, e.Bounds.Y + Ui.S(27), w, Ui.S(18));
            var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

            var f1 = Gemini.LooksArabic(item.Result) ? flags | TextFormatFlags.Right | TextFormatFlags.RightToLeft : flags;
            using (var font = Ui.Font(10.5f)) TextRenderer.DrawText(e.Graphics, OneLine(item.Result), font, r1, Ui.Text, f1);

            string meta = Ago(item.Time) + "  ·  " + OneLine(item.Source);
            using (var font = Ui.Font(8.5f)) TextRenderer.DrawText(e.Graphics, meta, font, r2, Ui.Muted, flags);

            using (var pen = new Pen(Color.FromArgb(241, 243, 244))) e.Graphics.DrawLine(pen, x, e.Bounds.Bottom - 1, e.Bounds.Right - Ui.S(10), e.Bounds.Bottom - 1);
        }

        void Relayout()
        {
            int w = W, pad = Pad, inner = w - pad * 2;
            int y = Ui.S(8);
            int tabW = (inner - Ui.S(30)) / 2;
            tabT.SetBounds(pad, y, tabW, Ui.S(34));
            tabR.SetBounds(pad + tabW, y, tabW, Ui.S(34));
            closeBtn.SetBounds(w - pad - Ui.S(24), y + Ui.S(6), Ui.S(24), Ui.S(22));
            y += Ui.S(40);

            bool hasItems = Items.Count > 0;
            int listH = Ui.S(50) * Math.Max(1, Math.Min(5, Items.Count));
            list.SetBounds(pad, y, inner, listH);
            empty.SetBounds(pad, y, inner, Ui.S(80));
            list.Visible = hasItems;
            empty.Visible = !hasItems;
            y += (hasItems ? listH : Ui.S(80)) + Ui.S(10);

            detailSource.Visible = detailResult.Visible = copyBtn.Visible = copySourceBtn.Visible = hasItems;
            if (hasItems)
            {
                detailSource.SetBounds(pad, y, inner, Ui.S(20)); y += Ui.S(22);
                detailResult.SetBounds(pad, y, inner, Ui.S(84)); y += Ui.S(84) + Ui.S(12);
            }
            // Use plain bools: .Visible reads false until the window is on screen.
            bool anyHistory = history.Translations.Count + history.Fixes.Count > 0;
            bool showButtons = hasItems || anyHistory;
            clearBtn.Visible = anyHistory;
            buttons.Visible = showButtons;
            buttons.Location = new Point(pad, y);
            if (showButtons) y += copyBtn.PreferredSize.Height + copyBtn.Margin.Vertical;
            y += pad;
            ClientSize = new Size(w, y);

            Rectangle wa = Screen.FromPoint(Location).WorkingArea;
            if (Bottom > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - Height - Ui.S(8));
            Invalidate();
        }

        void CopyAndClose()
        {
            var i = Selected;
            if (i == null) return;
            Clip.SetText(i.Result);
            Toast.Show("✓ Copied", Ui.Success, 1200, false);
            Close();
        }

        void CopySource()
        {
            var i = Selected;
            if (i == null) return;
            Clip.SetText(i.Source);
            copySourceBtn.Text = "Copied ✓";
        }

        void ClearAll()
        {
            dialogOpen = true;
            DialogResult answer;
            try { answer = MessageBox.Show(this, "Delete all saved translations and fixes?", "Lisan", MessageBoxButtons.YesNo, MessageBoxIcon.Question); }
            finally { dialogOpen = false; }
            Activate();
            if (answer != DialogResult.Yes) return;
            history.Clear();
            SwitchTo(showFixes);
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Ui.RoundCorners(Handle); }

        protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); list.Focus(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Ui.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using (var pen = new Pen(Ui.Border)) e.Graphics.DrawLine(pen, Pad, Ui.S(42), Width - Pad, Ui.S(42));
        }

        bool dialogOpen;
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (dialogOpen) return;
            BeginInvoke(new Action(() => { if (!IsDisposed && !ContainsFocus && Form.ActiveForm != this && !dialogOpen) Close(); }));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; return; }
            if (e.KeyCode == Keys.Tab || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) { SwitchTo(!showFixes); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (e.Control && e.KeyCode == Keys.C && detailResult.SelectionLength == 0) { CopyAndClose(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Tab) { SwitchTo(!showFixes); return true; }
            return base.ProcessDialogKey(keyData);
        }
    }
}
