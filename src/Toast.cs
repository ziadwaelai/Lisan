using System;
using System.Drawing;
using System.Windows.Forms;

namespace Lisan
{
    /// Small dark bubble near the mouse. Never takes keyboard focus away from the app you're typing in.
    internal class Toast : Form
    {
        static Toast current;
        readonly Label label;
        readonly Timer closeTimer = new Timer();
        readonly Timer dotsTimer = new Timer { Interval = 350 };
        string baseText;
        int dots;

        Toast()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Ui.ToastBg;
            Padding = new Padding(Ui.S(14), Ui.S(9), Ui.S(14), Ui.S(9));
            label = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Font = Ui.Font(10f),
                MaximumSize = new Size(Ui.S(380), 0),
                Location = new Point(Padding.Left, Padding.Top),
            };
            Controls.Add(label);
            closeTimer.Tick += delegate { Close(); };
            dotsTimer.Tick += delegate { dots = (dots + 1) % 4; label.Text = baseText + new string('.', dots); };
            label.Click += delegate { Close(); };
            Click += delegate { Close(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                cp.ClassStyle |= 0x20000; // drop shadow
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Ui.RoundCorners(Handle); }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            closeTimer.Stop(); dotsTimer.Stop();
            if (current == this) current = null;
            base.OnFormClosed(e);
        }

        /// Show a message. ms = 0 keeps it until replaced; working = animated dots.
        public static void Show(string text, Color color, int ms, bool working)
        {
            if (current == null || current.IsDisposed)
            {
                current = new Toast();
                current.Apply(text, color, ms, working);
                current.Location = Ui.PlaceNear(Cursor.Position, current.Size);
                current.Show();
            }
            else
            {
                current.Apply(text, color, ms, working);
            }
        }

        void Apply(string text, Color color, int ms, bool working)
        {
            baseText = text; dots = 0;
            label.Text = text;
            label.ForeColor = color;
            ClientSize = new Size(label.PreferredSize.Width + Padding.Horizontal, label.PreferredSize.Height + Padding.Vertical);
            if (working) dotsTimer.Start(); else dotsTimer.Stop();
            closeTimer.Stop();
            if (ms > 0) { closeTimer.Interval = ms; closeTimer.Start(); }
        }
    }
}
