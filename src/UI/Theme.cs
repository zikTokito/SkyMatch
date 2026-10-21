using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SkyMatch
{
    public static class Theme
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int index);

        public static readonly float DisplayScale = DetectDisplayScale();
        public static readonly Color Bg = Color.FromArgb(10, 13, 19);
        public static readonly Color Sidebar = Color.FromArgb(12, 16, 24);
        public static readonly Color Card = Color.FromArgb(19, 24, 34);
        public static readonly Color CardHover = Color.FromArgb(24, 30, 42);
        public static readonly Color CardSoft = Color.FromArgb(15, 20, 28);
        public static readonly Color Border = Color.FromArgb(35, 44, 58);
        public static readonly Color BorderSoft = Color.FromArgb(27, 35, 46);
        public static readonly Color Text = Color.FromArgb(238, 243, 249);
        public static readonly Color Muted = Color.FromArgb(143, 156, 174);
        public static readonly Color Dim = Color.FromArgb(91, 104, 122);
        public static readonly Color Green = Color.FromArgb(96, 211, 163);
        public static readonly Color Red = Color.FromArgb(255, 111, 117);
        public static readonly Color Yellow = Color.FromArgb(242, 190, 94);
        public static readonly Color White = Color.FromArgb(255, 255, 255);

        private static readonly string FontFamily = PickFont();        public static readonly Font FontTiny = new Font(FontFamily, 8.2f, FontStyle.Regular);
        public static readonly Font FontSmall = new Font(FontFamily, 9.2f, FontStyle.Regular);
        public static readonly Font FontBody = new Font(FontFamily, 10.2f, FontStyle.Regular);
        public static readonly Font FontMedium = new Font(FontFamily, 11.5f, FontStyle.Regular);
        public static readonly Font FontBold = new Font(FontFamily, 10.6f, FontStyle.Bold);
        public static readonly Font FontTitle = new Font(FontFamily, 20f, FontStyle.Bold);
        public static readonly Font FontHuge = new Font(FontFamily, 54f, FontStyle.Regular);
        public static readonly Font FontNumber = new Font(FontFamily, 16f, FontStyle.Bold);

        private static float DetectDisplayScale()
        {
            try
            {
                IntPtr dc = GetDC(IntPtr.Zero);
                if (dc != IntPtr.Zero)
                {
                    int physical = GetDeviceCaps(dc, 118); // DESKTOPHORZRES
                    ReleaseDC(IntPtr.Zero, dc);
                    int logical = Screen.PrimaryScreen == null ? 0 : Screen.PrimaryScreen.Bounds.Width;
                    if (physical > 0 && logical > 0)
                    {
                        float scale = (float)physical / logical;
                        if (scale >= 1.0f && scale <= 4.0f) return scale;
                    }
                }
            }
            catch
            {
            }
            return 1.0f;
        }

        private static string PickFont()
        {
            try
            {
                using (InstalledFontCollection fonts = new InstalledFontCollection())
                {
                    foreach (FontFamily family in fonts.Families)
                    {
                        if (family.Name == "Microsoft YaHei UI") return family.Name;
                    }
                    foreach (FontFamily family in fonts.Families)
                    {
                        if (family.Name == "Microsoft YaHei") return family.Name;
                    }
                }
            }
            catch
            {
            }
            return "Segoe UI";
        }

        public static Color Accent(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.ClearDay: return Color.FromArgb(247, 184, 92);
                case WeatherKind.ClearNight: return Color.FromArgb(148, 160, 255);
                case WeatherKind.Cloudy: return Color.FromArgb(140, 185, 215);
                case WeatherKind.Overcast: return Color.FromArgb(148, 158, 170);
                case WeatherKind.Rain: return Color.FromArgb(101, 174, 226);
                case WeatherKind.Storm: return Color.FromArgb(174, 143, 255);
                case WeatherKind.Snow: return Color.FromArgb(166, 228, 235);
                case WeatherKind.Fog: return Color.FromArgb(181, 191, 196);
                default: return Color.FromArgb(247, 184, 92);
            }
        }

        public static Color AccentDeep(WeatherKind kind)
        {
            Color a = Accent(kind);
            return Color.FromArgb(a.R * 55 / 100, a.G * 55 / 100, a.B * 55 / 100);
        }

        public static Color TimeColor(TimeSlot slot)
        {
            switch (slot)
            {
                case TimeSlot.Dawn: return Color.FromArgb(244, 190, 124);
                case TimeSlot.Day: return Color.FromArgb(126, 200, 244);
                case TimeSlot.Dusk: return Color.FromArgb(242, 146, 112);
                default: return Color.FromArgb(160, 168, 255);
            }
        }

        public static Color Alpha(Color color, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), color);
        }

        public static Color Mix(Color a, Color b, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }

        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Brush brush, RectangleF rect, float radius)
        {
            using (GraphicsPath path = RoundedRect(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRounded(Graphics g, Pen pen, RectangleF rect, float radius)
        {
            using (GraphicsPath path = RoundedRect(rect, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, float x, float y)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.DrawString(text, font, brush, x, y);
            }
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, RectangleF rect,
            StringAlignment align, StringAlignment lineAlign)
        {
            using (StringFormat format = new StringFormat())
            using (SolidBrush brush = new SolidBrush(color))
            {
                format.Alignment = align;
                format.LineAlignment = lineAlign;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, font, brush, rect, format);
            }
        }

        public static void DrawWrapped(Graphics g, string text, Font font, Color color, RectangleF rect,
            int maxLines)
        {
            using (StringFormat format = new StringFormat())
            using (SolidBrush brush = new SolidBrush(color))
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.LineLimit;
                g.DrawString(text, font, brush, rect, format);
            }
        }

        public static string Ellipsis(string text, int max)
        {
            if (String.IsNullOrEmpty(text)) return "";
            if (text.Length <= max) return text;
            return text.Substring(0, Math.Max(0, max - 1)) + "…";
        }

        public static string FitText(Graphics g, string text, Font font, float maxWidth)
        {
            if (String.IsNullOrEmpty(text)) return "";
            if (g.MeasureString(text, font).Width <= maxWidth) return text;
            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid) + "…";
                if (g.MeasureString(candidate, font).Width <= maxWidth) low = mid;
                else high = mid - 1;
            }
            if (low <= 0) return "…";
            return text.Substring(0, low) + "…";
        }

        public static string Clock(DateTime time)
        {
            return time.ToString("HH:mm");
        }

        public static string Temperature(double value)
        {
            return Math.Round(value).ToString("0") + "°";
        }

        public static string Percentage(double value)
        {
            return Math.Round(value).ToString("0") + "%";
        }

        public static string Greeting()
        {
            int hour = DateTime.Now.Hour;
            if (hour < 5) return "It's late at night";
            if (hour < 11) return "Good morning";
            if (hour < 14) return "good afternoon";
            if (hour < 18) return "good afternoon";
            return "Good evening";
        }
    }

    public static class WeatherGlyph
    {
        public static void Draw(Graphics g, WeatherKind kind, RectangleF rect, Color color, float alpha)
        {
            Color c = Color.FromArgb((int)Math.Max(0, Math.Min(255, alpha)), color);
            using (SolidBrush brush = new SolidBrush(c))
            using (Pen pen = new Pen(c, Math.Max(1.4f, rect.Width * 0.055f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;
                float s = Math.Min(rect.Width, rect.Height);
                switch (kind)
                {
                    case WeatherKind.ClearDay:
                        DrawSun(g, brush, pen, cx, cy, s);
                        break;
                    case WeatherKind.ClearNight:
                        DrawMoon(g, brush, cx, cy, s);
                        break;
                    case WeatherKind.Cloudy:
                        DrawSun(g, brush, pen, cx - s * 0.14f, cy - s * 0.16f, s * 0.62f);
                        DrawCloud(g, brush, rect, 0.74f);
                        break;
                    case WeatherKind.Overcast:
                        DrawCloud(g, brush, rect, 0.84f);
                        break;
                    case WeatherKind.Rain:
                        DrawCloud(g, brush, rect, 0.70f);
                        DrawRain(g, pen, rect, 3);
                        break;
                    case WeatherKind.Storm:
                        DrawCloud(g, brush, rect, 0.68f);
                        DrawBolt(g, brush, rect);
                        break;
                    case WeatherKind.Snow:
                        DrawCloud(g, brush, rect, 0.64f);
                        DrawSnow(g, pen, rect);
                        break;
                    case WeatherKind.Fog:
                        DrawFog(g, pen, rect);
                        break;
                }
            }
        }

        private static void DrawSun(Graphics g, SolidBrush brush, Pen pen, float cx, float cy, float s)
        {
            float r = s * 0.18f;
            g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                float x1 = cx + (float)Math.Cos(angle) * r * 1.55f;
                float y1 = cy + (float)Math.Sin(angle) * r * 1.55f;
                float x2 = cx + (float)Math.Cos(angle) * r * 2.2f;
                float y2 = cy + (float)Math.Sin(angle) * r * 2.2f;
                g.DrawLine(pen, x1, y1, x2, y2);
            }
        }

        private static void DrawMoon(Graphics g, SolidBrush brush, float cx, float cy, float s)
        {
            float r = s * 0.24f;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.FillMode = FillMode.Alternate;
                path.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                path.AddEllipse(cx - r * 0.35f, cy - r * 0.95f, r * 1.9f, r * 1.9f);
                g.FillPath(brush, path);
            }
            g.FillEllipse(brush, cx + r * 0.92f, cy - r * 0.92f, s * 0.045f, s * 0.045f);
            g.FillEllipse(brush, cx + r * 0.55f, cy + r * 0.65f, s * 0.032f, s * 0.032f);
        }

        private static void DrawCloud(Graphics g, SolidBrush brush, RectangleF rect, float scale)
        {
            float w = rect.Width * scale;
            float h = rect.Height * scale;
            float x = rect.X + (rect.Width - w) / 2;
            float y = rect.Y + rect.Height * 0.31f;
            g.FillEllipse(brush, x + w * 0.10f, y + h * 0.20f, w * 0.36f, h * 0.52f);
            g.FillEllipse(brush, x + w * 0.30f, y, w * 0.40f, h * 0.68f);
            g.FillEllipse(brush, x + w * 0.53f, y + h * 0.16f, w * 0.36f, h * 0.55f);
            g.FillRectangle(brush, x + w * 0.17f, y + h * 0.36f, w * 0.66f, h * 0.38f);
        }

        private static void DrawRain(Graphics g, Pen pen, RectangleF rect, int count)
        {
            float w = rect.Width;
            float h = rect.Height;
            for (int i = 0; i < count; i++)
            {
                float x = rect.X + w * (0.33f + i * 0.17f);
                float y = rect.Y + h * 0.67f;
                g.DrawLine(pen, x, y, x - w * 0.055f, y + h * 0.16f);
            }
        }

        private static void DrawBolt(Graphics g, SolidBrush brush, RectangleF rect)
        {
            float x = rect.X + rect.Width * 0.50f;
            float y = rect.Y + rect.Height * 0.62f;
            PointF[] points =
            {
                new PointF(x + rect.Width * 0.06f, y),
                new PointF(x - rect.Width * 0.05f, y + rect.Height * 0.15f),
                new PointF(x + rect.Width * 0.01f, y + rect.Height * 0.15f),
                new PointF(x - rect.Width * 0.07f, y + rect.Height * 0.33f),
                new PointF(x + rect.Width * 0.10f, y + rect.Height * 0.12f),
                new PointF(x + rect.Width * 0.03f, y + rect.Height * 0.12f)
            };
            g.FillPolygon(brush, points);
        }

        private static void DrawSnow(Graphics g, Pen pen, RectangleF rect)
        {
            float w = rect.Width;
            float h = rect.Height;
            for (int i = 0; i < 3; i++)
            {
                float x = rect.X + w * (0.33f + i * 0.18f);
                float y = rect.Y + h * (0.79f + (i % 2) * 0.05f);
                float r = w * 0.045f;
                g.DrawLine(pen, x - r, y, x + r, y);
                g.DrawLine(pen, x, y - r, x, y + r);
            }
        }

        private static void DrawFog(Graphics g, Pen pen, RectangleF rect)
        {
            float w = rect.Width;
            float h = rect.Height;
            for (int i = 0; i < 3; i++)
            {
                float y = rect.Y + h * (0.38f + i * 0.16f);
                g.DrawLine(pen, rect.X + w * (0.18f + i * 0.05f), y,
                    rect.X + w * (0.82f - i * 0.04f), y);
            }
        }
    }

    public static class IconPainter
    {
        public static void Nav(Graphics g, int index, RectangleF rect, Color color)
        {
            using (Pen pen = new Pen(color, 1.7f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float cx = rect.X + rect.Width / 2;
                float cy = rect.Y + rect.Height / 2;
                float s = Math.Min(rect.Width, rect.Height) * 0.66f;
                if (index == 0)
                {
                    float r = s * 0.24f;
                    g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(pen, cx + (float)Math.Cos(a) * r * 1.5f,
                            cy + (float)Math.Sin(a) * r * 1.5f,
                            cx + (float)Math.Cos(a) * r * 2.1f,
                            cy + (float)Math.Sin(a) * r * 2.1f);
                    }
                }
                else if (index == 1)
                {
                    float r = s * 0.34f;
                    float gap = s * 0.12f;
                    g.DrawRectangle(pen, cx - r, cy - r, r - gap, r - gap);
                    g.DrawRectangle(pen, cx + gap, cy - r, r - gap, r - gap);
                    g.DrawRectangle(pen, cx - r, cy + gap, r - gap, r - gap);
                    g.DrawRectangle(pen, cx + gap, cy + gap, r - gap, r - gap);
                }
                else if (index == 2)
                {
                    float r = s * 0.38f;
                    g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                    g.DrawLine(pen, cx, cy - r * 1.15f, cx, cy + r * 1.15f);
                    g.DrawLine(pen, cx - r * 1.15f, cy, cx + r * 1.15f, cy);
                    g.DrawLine(pen, cx - r * 0.55f, cy - r * 0.55f, cx + r * 0.55f, cy + r * 0.55f);
                    g.DrawLine(pen, cx + r * 0.55f, cy - r * 0.55f, cx - r * 0.55f, cy + r * 0.55f);
                }
                else
                {
                    float r = s * 0.34f;
                    g.DrawLine(pen, cx - r, cy - r * 0.55f, cx + r, cy - r * 0.55f);
                    g.DrawLine(pen, cx - r, cy + r * 0.25f, cx + r, cy + r * 0.25f);
                    g.DrawLine(pen, cx - r, cy + r * 0.95f, cx + r, cy + r * 0.95f);
                    using (SolidBrush dot = new SolidBrush(color))
                    {
                        g.FillEllipse(dot, cx - r - 2, cy - r * 0.55f - 2, 4, 4);
                        g.FillEllipse(dot, cx + r - 2, cy + r * 0.25f - 2, 4, 4);
                        g.FillEllipse(dot, cx - r - 2, cy + r * 0.95f - 2, 4, 4);
                    }
                }
            }
        }

        public static void Pin(Graphics g, RectangleF rect, Color color)
        {
            using (Pen pen = new Pen(color, 1.5f))
            {
                float cx = rect.X + rect.Width / 2;
                float cy = rect.Y + rect.Height / 2;
                float r = Math.Min(rect.Width, rect.Height) * 0.27f;
                g.DrawEllipse(pen, cx - r, cy - r * 1.25f, r * 2, r * 2);
                g.DrawLine(pen, cx - r * 0.6f, cy + r * 0.55f, cx, cy + r * 1.5f);
                g.DrawLine(pen, cx + r * 0.6f, cy + r * 0.55f, cx, cy + r * 1.5f);
                using (SolidBrush brush = new SolidBrush(color))
                    g.FillEllipse(brush, cx - 1.5f, cy - r * 0.25f, 3, 3);
            }
        }

        public static void Arrow(Graphics g, RectangleF rect, Color color, bool left)
        {
            using (Pen pen = new Pen(color, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float cx = rect.X + rect.Width / 2;
                float cy = rect.Y + rect.Height / 2;
                float s = Math.Min(rect.Width, rect.Height) * 0.22f;
                if (left)
                {
                    g.DrawLine(pen, cx + s, cy - s, cx - s, cy);
                    g.DrawLine(pen, cx - s, cy, cx + s, cy + s);
                }
                else
                {
                    g.DrawLine(pen, cx - s, cy - s, cx + s, cy);
                    g.DrawLine(pen, cx + s, cy, cx - s, cy + s);
                }
            }
        }

        public static void Refresh(Graphics g, RectangleF rect, Color color)
        {
            using (Pen pen = new Pen(color, 1.7f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                RectangleF r = new RectangleF(rect.X + rect.Width * 0.2f, rect.Y + rect.Height * 0.2f,
                    rect.Width * 0.6f, rect.Height * 0.6f);
                g.DrawArc(pen, r, 40, 280);
                float x = r.Right - r.Width * 0.08f;
                float y = r.Top + r.Height * 0.05f;
                g.DrawLine(pen, x, y, x - r.Width * 0.2f, y + r.Height * 0.02f);
                g.DrawLine(pen, x, y, x - r.Width * 0.02f, y + r.Height * 0.22f);
            }
        }
    }
}
