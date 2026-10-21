using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SkyMatch
{
    public class WeatherHero : Control
    {
        private WeatherData _weather;
        private WallpaperItem _wallpaper;

        public WeatherHero()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
        }

        public WeatherData Weather
        {
            get { return _weather; }
            set { _weather = value; Invalidate(); }
        }

        public WallpaperItem Wallpaper
        {
            get { return _wallpaper; }
            set { _wallpaper = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF rect = new RectangleF(0, 0, Width, Height);
            using (GraphicsPath clip = Theme.RoundedRect(rect, 20))
            {
                e.Graphics.SetClip(clip);
                DrawBackground(e.Graphics, rect);
                if (_weather == null)
                {
                    DrawLoading(e.Graphics, rect);
                }
                else
                {
                    DrawWeather(e.Graphics, rect);
                }
                e.Graphics.ResetClip();
            }
        }

        private void DrawBackground(Graphics g, RectangleF rect)
        {
            WeatherKind kind = _weather == null ? WeatherKind.Cloudy : _weather.Current.Kind;
            Color accent = Theme.Accent(kind);
            Bitmap image = _wallpaper == null ? null : AppImages.GetWallpaper(_wallpaper,
                Math.Max(1, (int)rect.Width), Math.Max(1, (int)rect.Height));
            if (image != null)
            {
                g.DrawImage(image, rect);
                using (LinearGradientBrush shade = new LinearGradientBrush(rect,
                    Color.FromArgb(150, 6, 10, 16), Color.FromArgb(235, 8, 12, 18), 35f))
                    g.FillRectangle(shade, rect);
                using (LinearGradientBrush glow = new LinearGradientBrush(rect,
                    Color.FromArgb(0, accent), Color.FromArgb(55, accent), 35f))
                    g.FillRectangle(glow, rect);
                return;
            }
            Color top = Theme.Mix(Color.FromArgb(23, 31, 42), accent, 0.18);
            Color bottom = Theme.Mix(Color.FromArgb(10, 15, 23), accent, 0.33);
            using (LinearGradientBrush brush = new LinearGradientBrush(rect, top, bottom, 35f))
                g.FillRectangle(brush, rect);
            using (LinearGradientBrush glow = new LinearGradientBrush(
                new RectangleF(rect.Width * 0.45f, -rect.Height * 0.2f, rect.Width * 0.8f, rect.Height * 1.4f),
                Theme.Alpha(accent, 60), Color.FromArgb(0, accent), 0f))
            {
                g.FillEllipse(glow, rect.Width * 0.55f, -rect.Height * 0.2f, rect.Width * 0.6f, rect.Height * 1.1f);
            }
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
            {
                g.FillEllipse(brush, rect.Width * 0.55f, rect.Height * 0.15f, rect.Width * 0.5f, rect.Width * 0.5f);
                g.FillEllipse(brush, rect.Width * 0.72f, rect.Height * 0.02f, rect.Width * 0.24f, rect.Width * 0.24f);
            }
            for (int i = 0; i < 8; i++)
            {
                float x = rect.Width * (0.45f + (i * 37 % 50) / 100f);
                float y = rect.Height * (0.12f + (i * 23 % 70) / 100f);
                using (SolidBrush dot = new SolidBrush(Theme.Alpha(accent, 30 + (i % 3) * 12)))
                    g.FillEllipse(dot, x, y, 2, 2);
            }
        }

        private void DrawLoading(Graphics g, RectangleF rect)
        {
            Theme.DrawText(g, "正在读取此刻的天气…", Theme.FontMedium, Theme.Alpha(Theme.Text, 190), 30, 30);
            Theme.DrawText(g, "定位 · 云端天气 · 壁纸匹配", Theme.FontSmall, Theme.Muted, 31, 62);
            using (SolidBrush brush = new SolidBrush(Theme.Alpha(Theme.White, 20)))
            {
                g.FillRectangle(brush, 30, rect.Height - 84, 110, 12);
                g.FillRectangle(brush, 30, rect.Height - 58, 180, 8);
            }
        }

        private void DrawWeather(Graphics g, RectangleF rect)
        {
            WeatherData data = _weather;
            CurrentWeather current = data.Current;
            WeatherKind kind = current.Kind;
            Color accent = Theme.Accent(kind);

            DrawArt(g, rect, kind, accent);

            float x = 28;
            float y = 24;
            IconPainter.Pin(g, new RectangleF(x, y + 1, 14, 14), Theme.Alpha(accent, 230));
            Theme.DrawText(g, data.Location.CityDistrict, Theme.FontBold, Theme.Text, x + 20, y);
            string sub = DateTime.Now.ToString("M月d日 HH:mm") + " · " + current.Description
                + " · " + TimeSlotInfo.Name(TimeSlotInfo.Current());
            Theme.DrawText(g, sub, Theme.FontSmall, Theme.Alpha(Theme.Muted, 220), x + 20, y + 21);

            if (data.Location.IsApproximate)
            {
                RectangleF badge = new RectangleF(x + 20, y + 47, 64, 20);
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(accent, 28)))
                    Theme.FillRounded(g, brush, badge, 10);
                Theme.DrawText(g, "IP 定位", Theme.FontTiny, Theme.Alpha(accent, 225), badge.X, badge.Y + 3);
            }

            string temp = Theme.Temperature(current.Temperature);
            float tempY = 72;
            using (SolidBrush brush = new SolidBrush(Theme.Text))
                g.DrawString(temp, Theme.FontHuge, brush, x - 3, tempY);
            SizeF tempSize = g.MeasureString(temp, Theme.FontHuge);
            Theme.DrawText(g, current.Description, Theme.FontMedium, Theme.Text,
                x + tempSize.Width + 6, tempY + 29);
            Theme.DrawText(g, "体感 " + Theme.Temperature(current.ApparentTemperature),
                Theme.FontSmall, Theme.Alpha(Theme.Muted, 235), x + tempSize.Width + 8, tempY + 58);
            Theme.DrawText(g, "最高 " + Theme.Temperature(current.High) + "  ·  最低 "
                + Theme.Temperature(current.Low), Theme.FontSmall, Theme.Alpha(Theme.Muted, 235),
                x + tempSize.Width + 8, tempY + 79);

            float statsY = rect.Height - 58;
            using (Pen pen = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
                g.DrawLine(pen, 28, statsY - 11, rect.Width - 28, statsY - 11);
            float available = rect.Width - 56;
            int statCount = rect.Width >= 420 ? 7 : 5;
            float statW = available / statCount;
            DrawStat(g, x, statsY, "湿度", Theme.Percentage(current.Humidity), statW);
            DrawStat(g, x + statW, statsY, "风速", current.WindSpeed.ToString("0.0") + " km/h", statW);
            DrawStat(g, x + statW * 2, statsY, "降雨概率", Theme.Percentage(current.PrecipitationProbability), statW);
            if (statCount == 7)
            {
                DrawStat(g, x + statW * 3, statsY, "云量", Theme.Percentage(current.CloudCover), statW);
                DrawStat(g, x + statW * 4, statsY, "降水量", current.Precipitation.ToString("0.0") + " mm", statW);
                DrawStat(g, x + statW * 5, statsY, "日出", current.Sunrise.HasValue ? Theme.Clock(current.Sunrise.Value) : "--:--", statW);
                DrawStat(g, x + statW * 6, statsY, "日落", current.Sunset.HasValue ? Theme.Clock(current.Sunset.Value) : "--:--", statW);
            }
            else
            {
                DrawStat(g, x + statW * 3, statsY, "日出", current.Sunrise.HasValue ? Theme.Clock(current.Sunrise.Value) : "--:--", statW);
                DrawStat(g, x + statW * 4, statsY, "日落", current.Sunset.HasValue ? Theme.Clock(current.Sunset.Value) : "--:--", statW);
            }
        }

        private void DrawStat(Graphics g, float x, float y, string label, string value, float width)
        {
            Theme.DrawText(g, value, Theme.FontBold, Theme.Text, x, y);
            Theme.DrawText(g, label, Theme.FontTiny, Theme.Muted, x, y + 20);
        }

        private void DrawArt(Graphics g, RectangleF rect, WeatherKind kind, Color accent)
        {
            float size = Math.Min(rect.Width * 0.38f, rect.Height * 0.98f);
            RectangleF art = new RectangleF(rect.Right - size - rect.Width * 0.05f,
                rect.Height * 0.08f, size, size);
            using (SolidBrush glow = new SolidBrush(Theme.Alpha(accent, 20)))
                g.FillEllipse(glow, art.X - art.Width * 0.1f, art.Y, art.Width * 1.2f, art.Height * 1.2f);
            WeatherGlyph.Draw(g, kind, art, accent, 215);

            if (kind == WeatherKind.Rain || kind == WeatherKind.Storm)
            {
                using (Pen pen = new Pen(Theme.Alpha(accent, 75), 1.2f))
                {
                    for (int i = 0; i < 9; i++)
                    {
                        float rx = rect.Right - size * 1.08f + (i * 53 % (int)(size * 1.1f));
                        float ry = rect.Height * 0.16f + (i * 31 % (int)(rect.Height * 0.65f));
                        g.DrawLine(pen, rx, ry, rx - 8, ry + 24);
                    }
                }
            }
            else if (kind == WeatherKind.ClearNight)
            {
                using (SolidBrush star = new SolidBrush(Theme.Alpha(accent, 145)))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        float sx = rect.Right - size * 1.1f + (i * 47 % (int)(size * 1.1f));
                        float sy = rect.Height * 0.1f + (i * 37 % (int)(rect.Height * 0.55f));
                        float r = i % 3 == 0 ? 2.2f : 1.2f;
                        g.FillEllipse(star, sx, sy, r, r);
                    }
                }
            }
        }
    }

    public class MatchCard : Control
    {
        private WallpaperItem _wallpaper;
        private WeatherData _weather;
        private bool _hover;
        private bool _fitWallpaper;
        private readonly ActionButton _applyButton;
        private readonly ActionButton _detailButton;
        private readonly ActionButton _prevButton;
        private readonly ActionButton _nextButton;
        private bool _canCycle;

        public event EventHandler ApplyClicked;
        public event EventHandler DetailsClicked;
        public event EventHandler PreviousClicked;
        public event EventHandler NextClicked;

        public MatchCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
            _applyButton = new ActionButton();
            _applyButton.Text = "立即切换";
            _applyButton.Primary = true;
            _applyButton.Pill = true;
            _applyButton.Size = new Size(106, 34);
            _applyButton.Font = Theme.FontBold;
            _applyButton.Click += delegate
            {
                EventHandler handler = ApplyClicked;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            Controls.Add(_applyButton);

            _detailButton = new ActionButton();
            _detailButton.Text = "详情";
            _detailButton.Size = new Size(62, 34);
            _detailButton.Pill = true;
            _detailButton.Font = Theme.FontSmall;
            _detailButton.Click += delegate
            {
                EventHandler handler = DetailsClicked;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            Controls.Add(_detailButton);

            _prevButton = new ActionButton();
            _prevButton.Text = "<";
            _prevButton.Pill = true;
            _prevButton.Size = new Size(30, 30);
            _prevButton.Font = Theme.FontBold;
            _prevButton.AccentColor = Theme.Muted;
            _prevButton.Click += delegate
            {
                EventHandler handler = PreviousClicked;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            Controls.Add(_prevButton);

            _nextButton = new ActionButton();
            _nextButton.Text = ">";
            _nextButton.Pill = true;
            _nextButton.Size = new Size(30, 30);
            _nextButton.Font = Theme.FontBold;
            _nextButton.AccentColor = Theme.Muted;
            _nextButton.Click += delegate
            {
                EventHandler handler = NextClicked;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            Controls.Add(_nextButton);
            _prevButton.Visible = false;
            _nextButton.Visible = false;
        }

        public WallpaperItem Wallpaper
        {
            get { return _wallpaper; }
            set
            {
                _wallpaper = value;
                if (value != null)
                {
                    _applyButton.AccentColor = Theme.Accent(value.MatchWeather);
                    _applyButton.Text = value.IsActive ? "已匹配" : "立即切换";
                    _applyButton.Enabled = !value.IsActive;
                }
                _detailButton.Visible = value != null;
                _applyButton.Visible = value != null;
                _prevButton.Visible = value != null && _canCycle;
                _nextButton.Visible = value != null && _canCycle;
                Invalidate();
            }
        }

        public void SyncActiveState()
        {
            if (_wallpaper == null) return;
            _applyButton.Text = _wallpaper.IsActive ? "已匹配" : "立即切换";
            _applyButton.Enabled = !_wallpaper.IsActive;
            _applyButton.Invalidate();
        }

        public bool FitWallpaper
        {            get { return _fitWallpaper; }
            set { _fitWallpaper = value; Invalidate(); }
        }

        public bool CanCycle
        {
            get { return _canCycle; }
            set
            {
                _canCycle = value;
                _prevButton.Visible = value && _wallpaper != null;
                _nextButton.Visible = value && _wallpaper != null;
            }
        }

        public WeatherData Weather
        {
            get { return _weather; }
            set { _weather = value; Invalidate(); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_applyButton == null) return;
            _applyButton.Location = new Point(Width - _applyButton.Width - 20, Height - 54);
            _detailButton.Location = new Point(Width - _applyButton.Width - _detailButton.Width - 30, Height - 54);
            int arrowY = Math.Max(68, Height / 2 - 34);
            _prevButton.Location = new Point(Width - 44, arrowY);
            _nextButton.Location = new Point(Width - 44, arrowY + 36);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                EventHandler handler = DetailsClicked;
                if (handler != null) handler(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF rect = new RectangleF(0, 0, Width, Height);
            using (GraphicsPath clip = Theme.RoundedRect(rect, 20))
            {
                e.Graphics.SetClip(clip);
                DrawBackdrop(e.Graphics, rect);
                DrawForeground(e.Graphics, rect);
                e.Graphics.ResetClip();
            }
            RectangleF border = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (Pen pen = new Pen(_hover ? Color.FromArgb(68, 79, 94) : Theme.BorderSoft, 1f))
                Theme.DrawRounded(e.Graphics, pen, border, 20);
        }

        private void DrawBackdrop(Graphics g, RectangleF rect)
        {
            Color accent = _wallpaper != null ? Theme.Accent(_wallpaper.MatchWeather)
                : Theme.Accent(_weather != null ? _weather.Current.Kind : WeatherKind.Cloudy);
            using (LinearGradientBrush brush = new LinearGradientBrush(rect,
                Color.FromArgb(28, 36, 48), Color.FromArgb(12, 17, 25), 90f))
                g.FillRectangle(brush, rect);
            Bitmap image = _wallpaper == null ? null : AppImages.GetWallpaper(_wallpaper,
                Math.Max(300, Width), Math.Max(200, Height), _fitWallpaper);
            if (_fitWallpaper && _wallpaper != null)
            {
                Bitmap backdrop = AppImages.GetWallpaper(_wallpaper,
                    Math.Max(300, Width), Math.Max(200, Height), false);
                if (backdrop != null)
                {
                    g.DrawImage(backdrop, rect);
                    using (SolidBrush backdropShade = new SolidBrush(Color.FromArgb(130, 6, 9, 14)))
                        g.FillRectangle(backdropShade, rect);
                }
            }
            if (image != null)
            {
                g.DrawImage(image, rect);
                using (LinearGradientBrush shade = new LinearGradientBrush(
                    new RectangleF(0, 0, rect.Width, rect.Height),
                    Color.FromArgb(30, 6, 9, 14), Color.FromArgb(238, 8, 12, 18), 90f))
                {
                    shade.Blend = new Blend();
                    shade.Blend.Factors = new[] { 0f, 0.35f, 0.96f, 1f };
                    shade.Blend.Positions = new[] { 0f, 0.36f, 0.78f, 1f };
                    g.FillRectangle(shade, rect);
                }
            }
            using (SolidBrush soft = new SolidBrush(Theme.Alpha(accent, 32)))
                g.FillEllipse(soft, rect.Width * 0.55f, -rect.Height * 0.2f, rect.Width * 0.7f, rect.Width * 0.7f);
        }

        private void DrawForeground(Graphics g, RectangleF rect)
        {
            Color accent = _wallpaper != null ? Theme.Accent(_wallpaper.MatchWeather)
                : Theme.Accent(_weather != null ? _weather.Current.Kind : WeatherKind.Cloudy);
            bool weatherApproximate = _wallpaper != null && _weather != null
                && _wallpaper.MatchWeather != _weather.Current.Kind;
            bool timeApproximate = _wallpaper != null && _weather != null
                && _wallpaper.PrimaryTime != TimeSlotInfo.Current();
            bool approximate = weatherApproximate || timeApproximate;
            string labelText = approximate ? "此刻推荐 · 近似" : "此刻推荐";
            float labelWidth = approximate ? 112 : 84;
            RectangleF label = new RectangleF(20, 18, labelWidth, 22);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(34, 255, 255, 255)))
                Theme.FillRounded(g, brush, label, 11);
            using (SolidBrush dot = new SolidBrush(accent))
                g.FillEllipse(dot, label.X + 10, label.Y + 8, 6, 6);
            Theme.DrawText(g, labelText, Theme.FontTiny, Theme.Alpha(Theme.Text, 235), label.X + 22, label.Y + 3);

            WeatherKind displayKind = _weather != null ? _weather.Current.Kind
                : (_wallpaper != null ? _wallpaper.MatchWeather : WeatherKind.Cloudy);
            TimeSlot displayTime = _weather != null ? TimeSlotInfo.Current()
                : (_wallpaper != null ? _wallpaper.PrimaryTime : TimeSlot.Day);
            Color currentAccent = Theme.Accent(displayKind);
            string kindName = WeatherKindInfo.Name(displayKind) + " · " + TimeSlotInfo.Name(displayTime);
            SizeF kindSize = g.MeasureString(kindName, Theme.FontTiny);
            RectangleF badge = new RectangleF(rect.Right - kindSize.Width - 38, 18, kindSize.Width + 20, 22);
            using (SolidBrush brush = new SolidBrush(Theme.Alpha(currentAccent, 45)))
                Theme.FillRounded(g, brush, badge, 11);
            WeatherGlyph.Draw(g, displayKind,
                new RectangleF(badge.X + 8, badge.Y + 5, 12, 12), currentAccent, 235);
            Theme.DrawText(g, kindName, Theme.FontTiny, currentAccent, badge.X + 24, badge.Y + 3);

            if (_wallpaper == null)
            {
                Theme.DrawText(g, "正在为此刻寻找合适的壁纸…", Theme.FontBold, Theme.Text, 22, rect.Height - 118);
                Theme.DrawText(g, "基于画面亮度、色彩与文字线索自动挑选", Theme.FontSmall, Theme.Muted,
                    22, rect.Height - 92);
                _applyButton.Visible = false;
                _detailButton.Visible = false;
                return;
            }

            string title = Theme.FitText(g, _wallpaper.Title, Theme.FontBold, rect.Width - 44);
            Theme.DrawText(g, title, Theme.FontBold, Theme.Text, 22, rect.Height - 118);
            string reason = _wallpaper.IsManual
                ? "已手动锁定为「" + WeatherKindInfo.Name(_wallpaper.MatchWeather) + "」"
                : _wallpaper.Reason;
            if (weatherApproximate)
                reason = "暂无「" + WeatherKindInfo.Name(_weather.Current.Kind) + "」专属壁纸，选用相近风格";
            else if (timeApproximate)
                reason = "暂无「" + TimeSlotInfo.Name(TimeSlotInfo.Current()) + "」专属壁纸，选用相近时段";
            Theme.DrawText(g, Theme.FitText(g, reason, Theme.FontSmall, rect.Width - 44),
                Theme.FontSmall, Theme.Alpha(Theme.Text, 190), 22, rect.Height - 94);
            string meta = _wallpaper.TypeLabel + " · " + TimeSlotInfo.Name(_wallpaper.PrimaryTime)
                + (approximate ? " · 近似推荐" : " · 匹配 "
                + Math.Round(_wallpaper.Confidence).ToString("0") + "%");
            Theme.DrawText(g, meta, Theme.FontTiny, Theme.Alpha(accent, 225), 22, rect.Height - 72);

            _applyButton.AccentColor = accent;
            _applyButton.Text = _wallpaper.IsActive ? "已匹配" : "立即切换";
            _applyButton.Enabled = !_wallpaper.IsActive;
            _detailButton.Visible = true;
            _applyButton.Visible = true;
        }
    }

    public class HourlyStrip : Control
    {
        private WeatherData _weather;
        private readonly Timer _timer;

        public HourlyStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
            _timer = new Timer();
            _timer.Interval = 30000;
            _timer.Tick += delegate
            {
                if (Visible && _weather != null) Invalidate();
            };
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Dispose();
            base.Dispose(disposing);
        }

        public WeatherData Weather
        {
            get { return _weather; }
            set { _weather = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (SolidBrush brush = new SolidBrush(Theme.Card))
                Theme.FillRounded(e.Graphics, brush, rect, 16);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                Theme.DrawRounded(e.Graphics, pen, rect, 16);
            int effectiveWidth = (int)(Width * Theme.DisplayScale);
            int maxHours = effectiveWidth >= 1800 ? 24 : (effectiveWidth >= 1350 ? 18 : 12);
            DateTime now = DateTime.Now;
            DateTime currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            int startIndex = 0;
            if (_weather != null)
            {
                while (startIndex < _weather.Hourly.Count && _weather.Hourly[startIndex].Time < currentHour)
                    startIndex++;
                if (startIndex >= _weather.Hourly.Count)
                    startIndex = Math.Max(0, _weather.Hourly.Count - 1);
            }
            int count = Math.Min(maxHours, _weather == null ? 0 : _weather.Hourly.Count - startIndex);
            string hourlyTitle = "接下来 " + count + " 小时";
            Theme.DrawText(e.Graphics, hourlyTitle, Theme.FontBold, Theme.Text, 20, 14);
            Theme.DrawText(e.Graphics, "逐小时预报", Theme.FontTiny, Theme.Dim,
                20 + e.Graphics.MeasureString(hourlyTitle, Theme.FontBold).Width + 12, 17);
            if (_weather == null || _weather.Hourly.Count == 0)
            {
                Theme.DrawText(e.Graphics, "正在载入逐小时天气…", Theme.FontSmall, Theme.Dim, 20, 48);
                return;
            }
            float left = 16;
            float top = 32;
            float cell = (Width - left * 2) / Math.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                HourlyForecast h = _weather.Hourly[startIndex + i];
                float x = left + cell * i;
                if (i > 0)
                {
                    using (Pen pen = new Pen(Color.FromArgb(22, 255, 255, 255), 1f))
                        e.Graphics.DrawLine(pen, x, top + 2, x, Height - 12);
                }
                string time = h.Time.Hour == now.Hour && h.Time.Date == now.Date ? "现在" : Theme.Clock(h.Time);
                Theme.DrawText(e.Graphics, time, Theme.FontTiny, i == 0 ? Theme.Text : Theme.Muted,
                    x + (cell - e.Graphics.MeasureString(time, Theme.FontTiny).Width) / 2f, top);
                RectangleF icon = new RectangleF(x + cell / 2f - 9, top + 16, 18, 18);
                WeatherGlyph.Draw(e.Graphics, h.Kind, icon, Theme.Accent(h.Kind), 225);
                string temp = Theme.Temperature(h.Temperature);
                SizeF ts = e.Graphics.MeasureString(temp, Theme.FontBold);
                Theme.DrawText(e.Graphics, temp, Theme.FontBold, Theme.Text, x + (cell - ts.Width) / 2f, top + 37);
                if (h.PrecipitationProbability >= 10)
                {
                    string rain = Theme.Percentage(h.PrecipitationProbability);
                    SizeF rs = e.Graphics.MeasureString(rain, Theme.FontTiny);
                    Theme.DrawText(e.Graphics, rain, Theme.FontTiny, Theme.Alpha(Theme.Accent(WeatherKind.Rain), 230),
                        x + (cell - rs.Width) / 2f, top + 56);
                }
            }
        }
    }

    public class ForecastPanel : Control
    {
        private WeatherData _weather;
        private Dictionary<WeatherKind, WallpaperItem> _representatives =
            new Dictionary<WeatherKind, WallpaperItem>();
        private List<WallpaperItem> _dayRepresentatives = new List<WallpaperItem>();
        private readonly List<RectangleF> _tiles = new List<RectangleF>();
        private readonly List<WallpaperItem> _tileItems = new List<WallpaperItem>();

        public event Action<WallpaperItem> DayClicked;

        public ForecastPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
        }

        public WeatherData Weather
        {
            get { return _weather; }
            set { _weather = value; Invalidate(); }
        }

        public void SetRepresentatives(Dictionary<WeatherKind, WallpaperItem> representatives)
        {
            _representatives = representatives ?? new Dictionary<WeatherKind, WallpaperItem>();
            Invalidate();
        }

        public void SetDayRepresentatives(List<WallpaperItem> representatives)
        {
            _dayRepresentatives = representatives ?? new List<WallpaperItem>();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hit = false;
            for (int i = 0; i < _tiles.Count; i++)
                if (_tiles[i].Contains(e.Location) && i < _tileItems.Count && _tileItems[i] != null)
                { hit = true; break; }
            Cursor = hit ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < _tiles.Count; i++)
                {
                    if (!_tiles[i].Contains(e.Location)) continue;
                    if (i >= _tileItems.Count || _tileItems[i] == null) continue;
                    Action<WallpaperItem> handler = DayClicked;
                    if (handler != null) handler(_tileItems[i]);
                    break;
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (SolidBrush brush = new SolidBrush(Theme.Card))
                Theme.FillRounded(e.Graphics, brush, rect, 16);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                Theme.DrawRounded(e.Graphics, pen, rect, 16);
            int effectiveWidth = (int)(Width * Theme.DisplayScale);
            Form host = FindForm();
            bool fullscreen = host != null && host.WindowState == FormWindowState.Maximized;
            int maxDays = fullscreen ? (effectiveWidth >= 1500 ? 7 : 5) : 8;
            int count = Math.Min(maxDays, _weather == null ? 0 : _weather.Daily.Count);
            Theme.DrawText(e.Graphics, "未来 " + count + " 天", Theme.FontBold, Theme.Text, 20, 16);
            Theme.DrawText(e.Graphics, fullscreen ? "把一周的天空提前铺开" : "四列两天，提前安排接下来的天气",
                Theme.FontTiny, Theme.Dim, 20, 38);
            if (_weather == null || _weather.Daily.Count == 0)
            {
                Theme.DrawText(e.Graphics, "正在载入预报…", Theme.FontSmall, Theme.Dim, 20, 74);
                return;
            }
            float left = 14;
            float top = 52;
            int columns = fullscreen
                ? (Height >= 300 ? 4 : count)
                : Math.Min(4, count);
            if (columns > count) columns = count;
            int rows = (count + columns - 1) / columns;
            float gapX = 8;
            float gapY = 8;
            float cell = (Width - left * 2 - gapX * (columns - 1)) / Math.Max(1, columns);
            float tileHeight = (Height - top - 14 - gapY * (rows - 1)) / Math.Max(1, rows);
            _tiles.Clear();
            _tileItems.Clear();
            for (int i = 0; i < count; i++)
            {
                DailyForecast day = _weather.Daily[i];
                int column = i % columns;
                int row = i / columns;
                float x = left + column * (cell + gapX);
                RectangleF tile = new RectangleF(x, top + row * (tileHeight + gapY), cell, tileHeight);
                WallpaperItem representative = null;
                Bitmap image = null;
                if (i < _dayRepresentatives.Count)
                    representative = _dayRepresentatives[i];
                else
                    _representatives.TryGetValue(day.Kind, out representative);
                if (representative != null)
                    image = AppImages.GetWallpaper(representative,
                        Math.Max(1, (int)tile.Width), Math.Max(1, (int)tile.Height));
                _tiles.Add(tile);
                _tileItems.Add(representative);
                if (image != null)
                {
                    using (GraphicsPath clip = Theme.RoundedRect(tile, 10))
                    {
                        GraphicsState state = e.Graphics.Save();
                        e.Graphics.SetClip(clip);
                        e.Graphics.DrawImage(image, tile);
                        e.Graphics.Restore(state);
                    }
                    using (LinearGradientBrush shade = new LinearGradientBrush(tile,
                        Color.FromArgb(100, 6, 9, 14), Color.FromArgb(215, 6, 9, 14), 90f))
                        e.Graphics.FillRectangle(shade, tile);
                }
                else
                {
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(22, 28, 38)))
                        Theme.FillRounded(e.Graphics, fill, tile, 10);
                    using (SolidBrush accent = new SolidBrush(Theme.Alpha(Theme.Accent(day.Kind), 36)))
                        Theme.FillRounded(e.Graphics, accent, tile, 10);
                }
                using (Pen border = new Pen(Color.FromArgb(38, 255, 255, 255), 1f))
                    Theme.DrawRounded(e.Graphics, border, tile, 10);

                string label = i == 0 ? "今天" : day.Weekday;
                Theme.DrawText(e.Graphics, label, Theme.FontBold, Theme.Text, tile.X + 10, tile.Y + 8);
                Theme.DrawText(e.Graphics, day.Date.ToString("M/d"), Theme.FontTiny,
                    Theme.Alpha(Theme.Text, 175), tile.X + 10, tile.Y + 28);
                bool compact = tile.Height < 150;
                WeatherGlyph.Draw(e.Graphics, day.Kind,
                    new RectangleF(tile.X + tile.Width / 2f - 14, tile.Y + (compact ? 40 : 48), 28, 28),
                    Theme.Accent(day.Kind), 235);
                string high = Theme.Temperature(day.Max);
                SizeF hs = e.Graphics.MeasureString(high, Theme.FontBold);
                Theme.DrawText(e.Graphics, high, Theme.FontBold, Theme.Text,
                    tile.X + (tile.Width - hs.Width) / 2f, tile.Y + (compact ? 70 : 84));
                string low = "低 " + Theme.Temperature(day.Min);
                SizeF ls = e.Graphics.MeasureString(low, Theme.FontTiny);
                Theme.DrawText(e.Graphics, low, Theme.FontTiny, Theme.Alpha(Theme.Text, 210),
                    tile.X + (tile.Width - ls.Width) / 2f, tile.Y + (compact ? 90 : 105));
                if (!compact && day.PrecipitationProbability >= 10)
                {
                    string rain = "降水 " + Theme.Percentage(day.PrecipitationProbability);
                    SizeF rs = e.Graphics.MeasureString(rain, Theme.FontTiny);
                    Theme.DrawText(e.Graphics, rain, Theme.FontTiny,
                        Theme.Alpha(Theme.Accent(WeatherKind.Rain), 240),
                        tile.X + (tile.Width - rs.Width) / 2f, tile.Y + 124);
                }
                if (!compact)
                {
                    Theme.DrawText(e.Graphics, day.Description, Theme.FontTiny, Theme.Alpha(Theme.Text, 190),
                        tile.X + (tile.Width - e.Graphics.MeasureString(day.Description, Theme.FontTiny).Width) / 2f,
                        tile.Y + 145);
                }
            }
        }
    }

    public class CoveragePanel : Control
    {
        private Dictionary<WeatherKind, int> _counts;
        private readonly Dictionary<WeatherKind, WallpaperItem> _representatives;
        private readonly Dictionary<WeatherKind, List<WallpaperItem>> _candidateLists;
        private readonly Dictionary<WeatherKind, int> _candidateIndexes;
        private WeatherKind _current;
        private int _hover = -1;
        private readonly List<RectangleF> _cells = new List<RectangleF>();

        public event Action<WeatherKind> ConditionClicked;
        public event Action<WeatherKind, WallpaperItem> RepresentativeClicked;
        public event Action<WeatherKind, WallpaperItem> RepresentativeChanged;

        public CoveragePanel()
        {
            _representatives = new Dictionary<WeatherKind, WallpaperItem>();
            _candidateLists = new Dictionary<WeatherKind, List<WallpaperItem>>();
            _candidateIndexes = new Dictionary<WeatherKind, int>();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
        }

        public void SetData(Dictionary<WeatherKind, int> counts, WeatherKind current)
        {
            SetData(counts, current, null);
        }

        public void SetData(Dictionary<WeatherKind, int> counts, WeatherKind current,
            Dictionary<WeatherKind, List<WallpaperItem>> candidates)
        {
            _counts = counts;
            _current = current;
            _representatives.Clear();
            _candidateLists.Clear();
            _candidateIndexes.Clear();
            if (candidates != null)
            {
                foreach (WeatherKind kind in WeatherKindInfo.All)
                {
                    List<WallpaperItem> list;
                    if (!candidates.TryGetValue(kind, out list) || list == null || list.Count == 0) continue;
                    _candidateLists[kind] = list;
                    _candidateIndexes[kind] = 0;
                    _representatives[kind] = list[0];
                }
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int found = -1;
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i].Contains(e.Location)) { found = i; break; }
            if (found != _hover)
            {
                _hover = found;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _hover >= 0 && _hover < WeatherKindInfo.All.Length)
            {
                WeatherKind kind = WeatherKindInfo.All[_hover];
                RectangleF cell = _cells[_hover];
                if (e.Y >= cell.Bottom - 30 && e.X <= cell.Left + 30)
                {
                    Cycle(kind, -1);
                }
                else if (e.Y >= cell.Bottom - 30 && e.X >= cell.Right - 30)
                {
                    Cycle(kind, 1);
                }
                else if (e.Y <= cell.Top + 30)
                {
                    Action<WeatherKind> handler = ConditionClicked;
                    if (handler != null) handler(kind);
                }
                else
                {
                    WallpaperItem item;
                    if (_representatives.TryGetValue(kind, out item) && item != null)
                    {
                        Action<WeatherKind, WallpaperItem> handler = RepresentativeClicked;
                        if (handler != null) handler(kind, item);
                    }
                }
            }
            base.OnMouseUp(e);
        }

        private void Cycle(WeatherKind kind, int delta)
        {
            List<WallpaperItem> list;
            if (!_candidateLists.TryGetValue(kind, out list) || list == null || list.Count == 0) return;
            int index;
            _candidateIndexes.TryGetValue(kind, out index);
            index = (index + delta + list.Count) % list.Count;
            _candidateIndexes[kind] = index;
            WallpaperItem item = list[index];
            _representatives[kind] = item;
            Invalidate();
            Action<WeatherKind, WallpaperItem> handler = RepresentativeChanged;
            if (handler != null) handler(kind, item);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (SolidBrush brush = new SolidBrush(Theme.Card))
                Theme.FillRounded(e.Graphics, brush, rect, 16);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                Theme.DrawRounded(e.Graphics, pen, rect, 16);
            Theme.DrawText(e.Graphics, "壁纸天气图谱", Theme.FontBold, Theme.Text, 20, 16);
            int total = 0;
            if (_counts != null) foreach (int n in _counts.Values) total += n;
            Theme.DrawText(e.Graphics, total + " 张壁纸 · 左右切换 · 点图看大图", Theme.FontTiny, Theme.Dim, 20, 38);

            _cells.Clear();
            float left = 16;
            float top = 60;
            float gap = 6;
            float cellW = (Width - left * 2 - gap * 3) / 4f;
            float cellH = Math.Max(48, (Height - top - 18 - gap) / 2f);
            for (int i = 0; i < 8; i++)
            {
                WeatherKind kind = WeatherKindInfo.All[i];
                int col = i % 4;
                int row = i / 4;
                RectangleF cell = new RectangleF(left + col * (cellW + gap), top + row * (cellH + gap), cellW, cellH);
                _cells.Add(cell);
                int count = 0;
                if (_counts != null) _counts.TryGetValue(kind, out count);
                Color accent = Theme.Accent(kind);
                bool active = kind == _current;
                WallpaperItem representative = null;
                _representatives.TryGetValue(kind, out representative);
                Bitmap image = representative == null ? null : AppImages.GetWallpaper(representative,
                    Math.Max(1, (int)cell.Width), Math.Max(1, (int)cell.Height));
                if (image != null)
                {
                    using (GraphicsPath clip = Theme.RoundedRect(cell, 9))
                    {
                        GraphicsState state = e.Graphics.Save();
                        e.Graphics.SetClip(clip);
                        e.Graphics.DrawImage(image, cell);
                        e.Graphics.Restore(state);
                    }
                    using (SolidBrush shade = new SolidBrush(Color.FromArgb(active ? 125 : 155, 8, 12, 18)))
                        Theme.FillRounded(e.Graphics, shade, cell, 9);
                }
                else
                {
                    using (SolidBrush fill = new SolidBrush(active ? Theme.Alpha(accent, 34)
                        : (i == _hover ? Color.FromArgb(29, 36, 47) : Color.FromArgb(22, 28, 38))))
                        Theme.FillRounded(e.Graphics, fill, cell, 9);
                }
                if (active || i == _hover)
                {
                    using (Pen p = new Pen(active ? Theme.Alpha(accent, 210) : Theme.Border, 1f))
                        Theme.DrawRounded(e.Graphics, p, cell, 9);
                }
                WeatherGlyph.Draw(e.Graphics, kind, new RectangleF(cell.X + 8, cell.Y + 8, 16, 16), accent, 235);
                Theme.DrawText(e.Graphics, WeatherKindInfo.Name(kind), Theme.FontTiny,
                    active ? Theme.Text : Theme.Alpha(Theme.Text, 225), cell.X + 29, cell.Y + 9);
                string countText = count.ToString();
                SizeF cs = e.Graphics.MeasureString(countText, Theme.FontNumber);
                Theme.DrawText(e.Graphics, countText, Theme.FontNumber, active ? accent : Theme.Alpha(Theme.Text, 235),
                    cell.X + (cell.Width - cs.Width) / 2f, cell.Bottom - cs.Height - 4);
                DrawArrowButton(e.Graphics, new RectangleF(cell.Left + 6, cell.Bottom - 28, 22, 22), "<",
                    cell.Width >= 90);
                DrawArrowButton(e.Graphics, new RectangleF(cell.Right - 28, cell.Bottom - 28, 22, 22), ">",
                    cell.Width >= 90);
            }
        }

        private void DrawArrowButton(Graphics g, RectangleF rect, string text, bool enabled)
        {
            using (SolidBrush fill = new SolidBrush(enabled ? Color.FromArgb(145, 8, 12, 18)
                : Color.FromArgb(80, 8, 12, 18)))
                Theme.FillRounded(g, fill, rect, 11);
            using (Pen pen = new Pen(enabled ? Color.FromArgb(95, 255, 255, 255)
                : Color.FromArgb(35, 255, 255, 255), 1f))
                Theme.DrawRounded(g, pen, rect, 11);
            SizeF size = g.MeasureString(text, Theme.FontBold);
            Theme.DrawText(g, text, Theme.FontBold, enabled ? Theme.Text : Theme.Dim,
                rect.X + (rect.Width - size.Width) / 2f, rect.Y + (rect.Height - size.Height) / 2f);
        }
    }

    public class DashboardPage : UserControl
    {
        private readonly AppController _controller;
        private readonly WeatherHero _hero;
        private readonly MatchCard _match;
        private readonly HourlyStrip _hourly;
        private readonly ForecastPanel _forecast;
        private readonly CoveragePanel _coverage;
        private readonly ActionButton _refresh;
        private readonly ActionButton _matchNow;
        private readonly ToggleSwitch _rotateToggle;
        private readonly Label _rotateLabel;
        private readonly MinutesInlineEditor _rotateEditor;
        private bool _syncingRotate;
        private bool _rotationShown;        private bool _needsRefresh;
        private List<WallpaperItem> _recommendationOptions = new List<WallpaperItem>();
        private int _recommendationIndex;

        public event Action<WeatherKind> ConditionClicked;
        public event Action<WallpaperItem> DetailsRequested;
        public event Action<WallpaperItem> SimpleDetailsRequested;

        public DashboardPage(AppController controller)
        {
            _controller = controller;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;

            _hero = new WeatherHero();
            _match = new MatchCard();
            _hourly = new HourlyStrip();
            _forecast = new ForecastPanel();
            _coverage = new CoveragePanel();
            _refresh = new ActionButton();
            _refresh.Text = "刷新天气";
            _refresh.Icon = ActionButton.IconKind.Refresh;
            _refresh.Size = new Size(112, 34);
            _refresh.Pill = true;
            _refresh.AccentColor = Theme.Yellow;
            _refresh.Click += async delegate { await _controller.RefreshWeatherAsync(true); };

            _rotateToggle = new ToggleSwitch();
            _rotateToggle.Size = new Size(44, 24);
            _rotateToggle.Checked = _controller.Settings.RecommendationRotateEnabled;
            _rotateLabel = new Label();
            _rotateLabel.AutoSize = true;
            _rotateLabel.Text = "自动更换壁纸";
            _rotateLabel.Font = Theme.FontSmall;
            _rotateLabel.ForeColor = Theme.Text;
            _rotateLabel.BackColor = Color.Transparent;
            _rotateEditor = new MinutesInlineEditor();
            _rotateEditor.Value = _controller.Settings.RecommendationRotateMinutes > 0
                ? _controller.Settings.RecommendationRotateMinutes.ToString() : "";
            _rotateEditor.SnapTo(_controller.Settings.RecommendationRotateEnabled);
            _rotationShown = _controller.Settings.RecommendationRotateEnabled;
            _rotateToggle.CheckedChanged += delegate
            {
                if (_syncingRotate) return;
                _controller.SetRecommendationRotateEnabled(_rotateToggle.Checked);
                _rotationShown = _rotateToggle.Checked;
                if (_rotateToggle.Checked) _rotateEditor.FadeIn();
                else _rotateEditor.FadeOut();
                DoLayout();
            };
            _rotateEditor.ValueChanged += delegate
            {
                int minutes;
                if (!Int32.TryParse(_rotateEditor.Value, out minutes)) minutes = 0;
                _controller.SetRecommendationRotateMinutes(minutes);
            };

            _matchNow = new ActionButton();
            _matchNow.Text = "按天气匹配";
            _matchNow.Icon = ActionButton.IconKind.Play;
            _matchNow.Size = new Size(116, 32);
            _matchNow.Pill = true;
            _matchNow.AccentColor = Theme.Yellow;
            _matchNow.Visible = false;

            Controls.Add(_hero);
            Controls.Add(_match);
            Controls.Add(_hourly);
            Controls.Add(_forecast);
            Controls.Add(_coverage);
            Controls.Add(_refresh);
            Controls.Add(_rotateToggle);
            Controls.Add(_rotateLabel);
            Controls.Add(_rotateEditor);
            Controls.Add(_matchNow);

            _match.ApplyClicked += async delegate
            {
                if (_match.Wallpaper != null)
                    await _controller.ApplyWallpaperAsync(_match.Wallpaper, false, "从此刻推荐应用");
            };
            _match.DetailsClicked += delegate
            {
                WallpaperItem item = _match.Wallpaper;
                if (item != null)
                {
                    Action<WallpaperItem> handler = DetailsRequested;
                    if (handler != null) handler(item);
                }
            };
            _match.PreviousClicked += delegate { CycleRecommendation(-1); };
            _match.NextClicked += delegate { CycleRecommendation(1); };
            _forecast.DayClicked += delegate(WallpaperItem item)
            {
                Action<WallpaperItem> handler = SimpleDetailsRequested;
                if (handler != null && item != null) handler(item);
            };
            _coverage.ConditionClicked += delegate(WeatherKind kind)
            {
                Action<WeatherKind> handler = ConditionClicked;
                if (handler != null) handler(kind);
            };
            _coverage.RepresentativeClicked += delegate(WeatherKind kind, WallpaperItem item)
            {
                Action<WallpaperItem> handler = SimpleDetailsRequested;
                if (handler != null) handler(item);
            };
            _coverage.RepresentativeChanged += delegate(WeatherKind kind, WallpaperItem item)
            {
                if (_controller.Weather != null && _controller.Weather.Current.Kind == kind)
                    _hero.Wallpaper = item;
            };

            _controller.WeatherUpdated += delegate { if (Visible) RefreshData(); else _needsRefresh = true; };
            _controller.WallpapersUpdated += delegate { if (Visible) RefreshData(); else _needsRefresh = true; };
            _controller.WorkshopUpdated += delegate
            {
                if (Visible && !_controller.WorkshopBusy) RefreshData(); else _needsRefresh = true;
            };
            _controller.StateChanged += delegate { RefreshState(); };
            RefreshData();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && _needsRefresh)
            {
                _needsRefresh = false;
                RefreshData();
            }
        }

        public void RefreshAppliedState()
        {
            _match.SyncActiveState();
        }

        public void RefreshData()
        {
            _hero.Weather = _controller.Weather;
            _hourly.Weather = _controller.Weather;
            _forecast.Weather = _controller.Weather;
            _match.Weather = _controller.Weather;
            string selectedId = _match.Wallpaper == null ? "" : _match.Wallpaper.Id;
            List<WallpaperItem> options = _controller.GetRecommendationOptions();
            if (options.Count == 0 && _controller.Recommended != null) options.Add(_controller.Recommended);
            _recommendationOptions = options;
            int selectedIndex = 0;
            if (_recommendationOptions.Count > 1 && selectedId.Length > 0)
            {
                for (int i = 0; i < _recommendationOptions.Count; i++)
                {
                    if (_recommendationOptions[i].Id == selectedId) { selectedIndex = i; break; }
                }
            }
            _recommendationIndex = selectedIndex;
            _match.CanCycle = _recommendationOptions.Count > 1;
            _match.Wallpaper = _recommendationOptions.Count > 0
                ? _recommendationOptions[_recommendationIndex] : null;
            WeatherKind current = _controller.Weather == null ? WeatherKind.Cloudy
                : _controller.Weather.Current.Kind;
            Dictionary<WeatherKind, List<WallpaperItem>> representatives =
                new Dictionary<WeatherKind, List<WallpaperItem>>();
            Dictionary<WeatherKind, WallpaperItem> forecastRepresentatives =
                new Dictionary<WeatherKind, WallpaperItem>();
            foreach (WeatherKind kind in WeatherKindInfo.All)
            {
                List<WallpaperItem> list = _controller.GetRepresentativeWallpapers(kind);
                representatives[kind] = list;
                if (list.Count > 0) forecastRepresentatives[kind] = list[0];
            }
            _forecast.SetRepresentatives(forecastRepresentatives);
            List<WallpaperItem> dayRepresentatives = new List<WallpaperItem>();
            HashSet<string> usedDayImages = new HashSet<string>(StringComparer.Ordinal);
            if (_controller.Weather != null)
            {
                foreach (DailyForecast day in _controller.Weather.Daily)
                {
                    List<WallpaperItem> dayList;
                    WallpaperItem chosen = null;
                    if (representatives.TryGetValue(day.Kind, out dayList))
                    {
                        foreach (WallpaperItem candidate in dayList)
                        {
                            if (candidate == null) continue;
                            if (usedDayImages.Contains(candidate.Id)) continue;
                            chosen = candidate;
                            break;
                        }
                    }
                    // Keep the day's own weather type. If no unused same-type
                    // candidate exists, leave the tile on its weather gradient
                    // instead of borrowing a different type's wallpaper.
                    if (chosen != null) usedDayImages.Add(chosen.Id);
                    dayRepresentatives.Add(chosen);
                }
            }
            _forecast.SetDayRepresentatives(dayRepresentatives);
            List<WallpaperItem> currentList;
            _hero.Wallpaper = representatives.TryGetValue(current, out currentList)
                && currentList.Count > 0 ? currentList[0] : null;
            _coverage.SetData(_controller.CountByKind(), current, representatives);
            RefreshState();
            Invalidate();
        }

        private void CycleRecommendation(int delta)
        {
            if (_recommendationOptions.Count < 2) return;
            _recommendationIndex = (_recommendationIndex + delta + _recommendationOptions.Count)
                % _recommendationOptions.Count;
            _match.Wallpaper = _recommendationOptions[_recommendationIndex];
        }

        private void SyncRotationUi()
        {
            bool enabled = _controller.Settings.RecommendationRotateEnabled;
            _syncingRotate = true;
            if (_rotateToggle.Checked != enabled) _rotateToggle.Checked = enabled;
            _syncingRotate = false;
            if (enabled && !_rotationShown)
            {
                _rotationShown = true;
                _rotateEditor.FadeIn();
            }
            else if (!enabled && _rotationShown)
            {
                _rotationShown = false;
                _rotateEditor.FadeOut();
            }
            bool show = Width >= 900;
            _rotateToggle.Visible = show;
            _rotateLabel.Visible = show;
            if (!show)
            {
                _rotationShown = false;
                _rotateEditor.SnapTo(false);
            }
            Invalidate(new Rectangle(0, 0, Width, 54));
        }

        private void RefreshState()
        {
            _refresh.Enabled = !_controller.IsRefreshingWeather;
            _refresh.Text = _controller.IsRefreshingWeather ? "更新中…" : "刷新天气";
            _refresh.Invalidate();
            SyncRotationUi();
            Invalidate(new Rectangle(0, 0, Width, 54));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        private void DoLayout()
        {
            if (Width < 700 || Height < 450) return;
            int gap = 14;
            int top = 54;
            bool wide = Width * Theme.DisplayScale >= 1500;
            Form host = FindForm();
            bool fullscreen = host != null && host.WindowState == FormWindowState.Maximized;
            int heroH = wide
                ? Math.Max(300, Math.Min(360, (int)(Height * 0.44f)))
                : Math.Max(238, Math.Min(286, (int)(Height * 0.385f)));
            int hourlyH = wide ? 112 : 104;
            int bottomTop = top + heroH + gap + hourlyH + gap;
            int bottomH = Math.Max(190, Height - bottomTop);
            int heroW = wide ? (int)((Width - gap) * 0.40f) : (int)((Width - gap) * 0.58f);
            int forecastW = wide ? (int)((Width - gap) * 0.52f) : (int)((Width - gap) * 0.60f);
            if (fullscreen)
            {
                // Fullscreen: smaller weather/forecast row, larger bottom row.
                // Bottom-left is the weather atlas, bottom-right is the
                // recommendation wallpaper.
                int topH = Math.Max(210, Math.Min(260, (int)(Height * 0.30f)));
                int lowerTop = top + topH + gap + hourlyH + gap;
                int bottomHeight = Math.Max(220, Height - lowerTop - 14);
                _hero.SetBounds(0, top, heroW, topH);
                _forecast.SetBounds(heroW + gap, top, Width - heroW - gap, topH);
                _hourly.SetBounds(0, top + topH + gap, Width, hourlyH);
                int atlasW = (int)((Width - gap) * 0.48f);
                _coverage.SetBounds(0, lowerTop, atlasW, bottomHeight);
                _match.FitWallpaper = true;
                _match.SetBounds(atlasW + gap, lowerTop, Width - atlasW - gap, bottomHeight);
            }
            else
            {
                _hero.SetBounds(0, top, heroW, heroH);
                _match.FitWallpaper = wide;
                _match.SetBounds(heroW + gap, top, Width - heroW - gap, heroH);
                _hourly.SetBounds(0, top + heroH + gap, Width, hourlyH);
                _forecast.SetBounds(0, bottomTop, forecastW, bottomH);
                _coverage.SetBounds(forecastW + gap, bottomTop, Width - forecastW - gap, bottomH);
            }
            _refresh.Location = new Point(Width - _refresh.Width, 0);
            int statusLeft = StatusLeft();
            int labelWidth = _rotateLabel.PreferredWidth;
            int groupWidth = 44 + 8 + labelWidth + 10 + _rotateEditor.Width;
            int groupLeft = statusLeft - groupWidth - 14;
            _rotateToggle.Location = new Point(groupLeft, 5);
            _rotateLabel.Location = new Point(groupLeft + 52, 4);
            _rotateEditor.Location = new Point(groupLeft + 52 + labelWidth + 10, 0);
            SyncRotationUi();
        }

        private int StatusLeft()
        {
            string status = _controller.Settings.AutoFollow ? "自动跟随已开启" : "自动跟随已暂停";
            Size size = TextRenderer.MeasureText(status, Theme.FontTiny);
            return Width - _refresh.Width - size.Width - 34;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            Theme.DrawText(e.Graphics, Theme.Greeting() + "，这里是今天的天空", Theme.FontTiny, Theme.Dim, 2, 0);
            string location = _controller.Weather == null ? "定位中…" : _controller.Weather.Location.DisplayName;
            Theme.DrawText(e.Graphics, "此刻 · " + location, Theme.FontTitle, Theme.Text, 0, 15);

            string status = _controller.Settings.AutoFollow ? "自动跟随已开启" : "自动跟随已暂停";
            Color statusColor = _controller.Settings.AutoFollow ? Theme.Green : Theme.Dim;
            float sx = StatusLeft();
            using (SolidBrush dot = new SolidBrush(statusColor))
                e.Graphics.FillEllipse(dot, sx, 16, 6, 6);
            Theme.DrawText(e.Graphics, status, Theme.FontTiny, statusColor, sx + 12, 11);
            if (_controller.Weather != null)
            {
                Theme.DrawText(e.Graphics, Theme.Clock(_controller.Weather.Current.ObservedAt) + " 观测",
                    Theme.FontTiny, Theme.Dim, sx + 12, 26);
            }
            base.OnPaint(e);
        }
    }
}
