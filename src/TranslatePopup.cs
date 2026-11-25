using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Lisan
{
    /// Popup next to the mouse that shows a translation.
    /// "Input mode" (nothing was selected) lets you type or paste text to translate.
    internal class TranslatePopup : Form
    {
        readonly TrayApp app;
        readonly IntPtr target;
        readonly bool inputMode;
        readonly Label header;
        readonly Label closeBtn;
        readonly TextBox input;
        readonly TextBox output;
        readonly FlowLayoutPanel buttons;
        readonly Button translateBtn, copyBtn, replaceBtn, retryBtn;
        bool showOutput;
        string lastSource;
        string result;
        int requestId;

        int W { get { return Ui.S(440); } }
        int Pad { get { return Ui.S(14); } }

        public TranslatePopup(TrayApp app, IntPtr target, string text)
        {
            this.app = app;
            this.target = target;
            inputMode = string.IsNullOrWhiteSpace(text);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            BackColor = Ui.Bg;
            Font = Ui.Font(9.5f);
            Text = "Lisan";

            header = new Label { AutoSize = false, ForeColor = Ui.Muted, Font = Ui.Font(9f, FontStyle.Bold), Cursor = Cursors.SizeAll, Text = "Translate" };
            header.MouseDown += DragWindow;
            closeBtn = new Label { Text = "✕", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Ui.Muted, Cursor = Cursors.Hand, Font = Ui.Font(10f) };
            closeBtn.Click += delegate { Close(); };
            closeBtn.MouseEnter += delegate { closeBtn.BackColor = Ui.Hover; };
            closeBtn.MouseLeave += delegate { closeBtn.BackColor = Ui.Bg; };

            input = new TextBox
            {
                Multiline = true, BorderStyle = BorderStyle.FixedSingle, Font = Ui.Font(11f),
                BackColor = Color.FromArgb(248, 249, 250), ForeColor = Ui.Text, AcceptsReturn = false,
            };
            input.TextChanged += delegate { input.RightToLeft = Gemini.LooksArabic(input.Text) ? RightToLeft.Yes : RightToLeft.No; Relayout(); };
            input.KeyDown += InputKeyDown;

            output = new TextBox
            {
                Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, Font = Ui.Font(11.5f),
                BackColor = Ui.Bg, ForeColor = Ui.Text, TabStop = false,
            };

            buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Ui.Bg };
            translateBtn = Ui.MakeButton("Translate  (Enter)", true);
            translateBtn.Click += delegate { StartTranslate(input.Text); };
            copyBtn = Ui.MakeButton("Copy", true);
            copyBtn.Click += delegate { CopyAndClose(); };
            replaceBtn = Ui.MakeButton("Replace selection", false);
            replaceBtn.Click += delegate { ReplaceAndClose(); };
            retryBtn = Ui.MakeButton("Try again", false);
            retryBtn.Click += delegate { StartTranslate(lastSource); };
            buttons.Controls.AddRange(new Control[] { translateBtn, copyBtn, replaceBtn, retryBtn });

            Controls.AddRange(new Control[] { header, closeBtn, input, output, buttons });

            input.Visible = inputMode;
            output.Visible = false;
            SetButtons(inputMode, false, false, false);

            if (inputMode)
            {
                header.Text = "Type or paste text to translate";
            }
            else
            {
                StartTranslate(text);
            }
            Relayout();
            Location = Ui.PlaceNear(Cursor.Position, Size);
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } // drop shadow
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Ui.RoundCorners(Handle); }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            if (inputMode) input.Focus(); else buttons.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Ui.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        // Click anywhere else and the popup goes away (except while you're typing in input mode).
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!inputMode && !IsDisposed) BeginInvoke(new Action(() => { if (!IsDisposed) Close(); }));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.C && result != null && output.SelectionLength == 0 && !input.Focused)
            { CopyAndClose(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        void InputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                StartTranslate(input.Text);
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                input.SelectAll(); e.SuppressKeyPress = true;
            }
        }

        async void StartTranslate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            lastSource = text;
            int id = ++requestId;
            result = null;
            bool toEnglish = Gemini.LooksArabic(text);
            header.Text = toEnglish ? "Arabic  →  English" : "English  →  Arabic (" + StyleName() + ")";
            showOutput = true; output.Visible = true;
            output.ForeColor = Ui.Muted;
            output.RightToLeft = RightToLeft.No;
            output.Text = "Translating…";
            SetButtons(inputMode, false, false, false);
            Relayout();

            string res = null, err = null;
            try { res = await app.Gemini.Translate(text); }
            catch (Exception ex) { err = ex.Message; }
            if (IsDisposed || id != requestId) return;
            ShowResult(res, err);
        }

        internal void ShowResult(string res, string err)
        {
            showOutput = true; output.Visible = true;
            if (err != null)
            {
                output.ForeColor = Ui.Error;
                output.Text = err;
                SetButtons(inputMode, false, false, true);
            }
            else
            {
                result = res;
                output.ForeColor = Ui.Text;
                output.RightToLeft = Gemini.LooksArabic(res) ? RightToLeft.Yes : RightToLeft.No;
                output.Text = res.Replace("\r\n", "\n").Replace("\n", "\r\n");
                output.Select(0, 0);
                SetButtons(inputMode, true, !inputMode && target != IntPtr.Zero, false);
            }
            Relayout();
        }

        int buttonCount;
        void SetButtons(bool translate, bool copy, bool replace, bool retry)
        {
            translateBtn.Visible = translate; copyBtn.Visible = copy; replaceBtn.Visible = replace; retryBtn.Visible = retry;
            buttonCount = (translate ? 1 : 0) + (copy ? 1 : 0) + (replace ? 1 : 0) + (retry ? 1 : 0);
            // When translating typed text, keep the Translate button but make Copy the main action once done.
            if (copy && translate) translateBtn.Text = "Translate again"; else translateBtn.Text = "Translate  (Enter)";
        }

        static string StyleName()
        {
            string s = Program.Settings.ArabicStyle;
            return s == "Standard" ? "Standard" : s == "Gulf" ? "Gulf" : "Egyptian";
        }

        int MeasureHeight(TextBox box, int min, int max)
        {
            string t = box.Text.Length == 0 ? "X" : box.Text;
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
            int h = TextRenderer.MeasureText(t, box.Font, new Size(box.Width - Ui.S(8), int.MaxValue), flags).Height + Ui.S(8);
            box.ScrollBars = h > max ? ScrollBars.Vertical : ScrollBars.None;
            return Math.Max(min, Math.Min(max, h));
        }

        void Relayout()
        {
            int w = W, pad = Pad, inner = w - pad * 2;
            int y = pad - Ui.S(2);
            header.SetBounds(pad, y, inner - Ui.S(30), Ui.S(22));
            closeBtn.SetBounds(w - pad - Ui.S(24), y, Ui.S(24), Ui.S(22));
            y += Ui.S(28);

            if (inputMode)
            {
                input.Width = inner;
                int h = MeasureHeight(input, Ui.S(60), Ui.S(160));
                input.SetBounds(pad, y, inner, h);
                y += h + Ui.S(10);
            }
            if (showOutput)
            {
                output.Width = inner;
                int h = MeasureHeight(output, Ui.S(26), Ui.S(320));
                output.SetBounds(pad, y, inner, h);
                y += h + Ui.S(12);
            }
            buttons.Location = new Point(pad, y);
            bool anyButton = buttonCount > 0;
            if (anyButton) y += buttons.PreferredSize.Height;
            buttons.Visible = anyButton;
            y += pad;

            var old = Bounds;
            ClientSize = new Size(w, y);
            // Keep the popup on screen as it grows.
            Rectangle wa = Screen.FromPoint(old.Location).WorkingArea;
            if (Bottom > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - Height - Ui.S(8));
            Invalidate();
        }

        void CopyAndClose()
        {
            if (result == null) return;
            Clip.SetText(result);
            copyBtn.Text = "Copied ✓";
            var t = new Timer { Interval = 450 };
            t.Tick += delegate { t.Stop(); t.Dispose(); Close(); };
            t.Start();
        }

        void ReplaceAndClose()
        {
            if (result == null) return;
            string r = result;
            Hide();
            app.PasteInto(target, r, true);
            Close();
        }

        // Drag the popup by its header.
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }
    }
}
