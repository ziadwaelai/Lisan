using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lisan
{
    internal static class Ui
    {
        public static readonly Color Bg = Color.White;
        public static readonly Color Text = Color.FromArgb(32, 33, 36);
        public static readonly Color Muted = Color.FromArgb(95, 99, 104);
        public static readonly Color Border = Color.FromArgb(218, 220, 224);
        public static readonly Color Hover = Color.FromArgb(241, 243, 244);
        public static readonly Color Accent = Color.FromArgb(26, 115, 232);
        public static readonly Color AccentDark = Color.FromArgb(23, 94, 196);
        public static readonly Color Error = Color.FromArgb(197, 34, 31);
        public static readonly Color ToastBg = Color.FromArgb(40, 42, 46);
        public static readonly Color Success = Color.FromArgb(129, 201, 149);

        static float scale = -1;
        public static float Scale
        {
            get
            {
                if (scale < 0)
                    using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
                return scale;
            }
        }
        public static int S(int px) { return (int)Math.Round(px * Scale); }

        public static Font Font(float pt) { return new Font("Segoe UI", pt); }
        public static Font Font(float pt, FontStyle st) { return new Font("Segoe UI", pt, st); }

        public static Button MakeButton(string text, bool primary)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(S(8), S(2), S(8), S(2)),
                Margin = new Padding(0, 0, S(8), 0),
                Font = Font(9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                BackColor = primary ? Accent : Bg,
                ForeColor = primary ? Color.White : Text,
            };
            b.FlatAppearance.BorderColor = primary ? Accent : Border;
            b.FlatAppearance.MouseOverBackColor = primary ? AccentDark : Hover;
            b.FlatAppearance.MouseDownBackColor = primary ? AccentDark : Border;
            return b;
        }

        public static Point PlaceNear(Point anchor, Size size)
        {
            Rectangle wa = Screen.FromPoint(anchor).WorkingArea;
            int x = anchor.X + S(12), y = anchor.Y + S(18);
            if (x + size.Width > wa.Right) x = wa.Right - size.Width - S(8);
            if (y + size.Height > wa.Bottom) y = anchor.Y - size.Height - S(12);
            if (x < wa.Left) x = wa.Left + S(8);
            if (y < wa.Top) y = wa.Top + S(8);
            return new Point(x, y);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// Windows 11 rounded corners for borderless windows (no-op on Windows 10).
        public static void RoundCorners(IntPtr hwnd)
        {
            try { int round = 2; DwmSetWindowAttribute(hwnd, 33, ref round, 4); } catch { }
        }
    }
}
