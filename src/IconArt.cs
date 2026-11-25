using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace Lisan
{
    /// Draws the app icon in code (also used by build.ps1 to write Lisan.ico).
    public static class IconArt
    {
        public static Bitmap Draw(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                float r = size * 0.24f;
                var rect = new RectangleF(0, 0, size - 0.5f, size - 0.5f);
                using (var path = Rounded(rect, r))
                using (var brush = new LinearGradientBrush(rect, Color.FromArgb(26, 115, 232), Color.FromArgb(18, 168, 150), 45f))
                    g.FillPath(brush, path);

                using (var font = new Font("Segoe UI", size * 0.56f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("ع", font, Brushes.White, new RectangleF(0, -size * 0.06f, size, size), sf);
            }
            return bmp;
        }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Icon MakeIcon(int size)
        {
            using (var bmp = Draw(size))
            {
                IntPtr h = bmp.GetHicon();
                var icon = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return icon;
            }
        }

        /// Writes a multi-size .ico (BMP entries for small sizes, PNG for 256).
        public static void SaveIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
            var images = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            {
                using (var bmp = Draw(sizes[i]))
                    images[i] = sizes[i] == 256 ? Png(bmp) : Dib(bmp);
            }
            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0);
                    w.Write((ushort)1); w.Write((ushort)32);
                    w.Write(images[i].Length); w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var img in images) w.Write(img);
            }
        }

        static byte[] Png(Bitmap bmp)
        {
            using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); return ms.ToArray(); }
        }

        static byte[] Dib(Bitmap bmp)
        {
            int s = bmp.Width;
            int maskRow = ((s + 31) / 32) * 4;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(40); w.Write(s); w.Write(s * 2); w.Write((ushort)1); w.Write((ushort)32);
                w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                for (int y = s - 1; y >= 0; y--)
                    for (int x = 0; x < s; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
                    }
                w.Write(new byte[maskRow * s]);
                return ms.ToArray();
            }
        }
    }
}
