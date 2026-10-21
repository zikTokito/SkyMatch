using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SkyMatch
{
    public static class WindowFx
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr region, uint flags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom,
            int widthEllipse, int heightEllipse);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y,
            int width, int height, uint flags);

        public static void ApplyRoundedCorners(Form form, int radius)
        {
            if (form == null || !form.IsHandleCreated) return;
            try
            {
                if (form.WindowState != FormWindowState.Normal)
                {
                    SetWindowRgn(form.Handle, IntPtr.Zero, true);
                    return;
                }
                if (radius <= 0)
                {
                    SetWindowRgn(form.Handle, IntPtr.Zero, true);
                    return;
                }
                IntPtr region = CreateRoundRectRgn(0, 0, form.Width + 1, form.Height + 1,
                    radius * 2, radius * 2);
                if (region != IntPtr.Zero) SetWindowRgn(form.Handle, region, true);
            }
            catch
            {
            }
        }

        public static void SetComposited(Form form, bool enabled)
        {
            if (form == null || !form.IsHandleCreated) return;
            try
            {
                const int exStyleIndex = -20;
                const int composited = 0x02000000;
                int style = GetWindowLong(form.Handle, exStyleIndex);
                int next = enabled ? (style | composited) : (style & ~composited);
                if (next == style) return;
                SetWindowLong(form.Handle, exStyleIndex, next);
                SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0,
                    0x0001 | 0x0002 | 0x0004 | 0x0020); // NOSIZE | NOMOVE | NOZORDER | FRAMECHANGED
            }
            catch
            {
            }
        }

        public static void Repaint(Form form)
        {
            if (form == null || !form.IsHandleCreated || !form.Visible) return;
            try
            {
                form.Invalidate(true);
                const uint flags = 0x0001 | 0x0004 | 0x0080 | 0x0100 | 0x0400;
                RedrawWindow(form.Handle, IntPtr.Zero, IntPtr.Zero, flags);
            }
            catch
            {
            }
        }

        public static void Drag(Form form)
        {
            if (form == null) return;
            ReleaseCapture();
            SendMessage(form.Handle, 0xA1, 0x2, 0);
        }

        public static void SetRedraw(Control control, bool enabled)
        {
            if (control == null || !control.IsHandleCreated) return;
            SendMessage(control.Handle, 0x000B, enabled ? 1 : 0, 0); // WM_SETREDRAW
        }

        public static void DrawBorder(Form form, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Color.FromArgb(44, 54, 68), 1f))
            using (GraphicsPath path = Theme.RoundedRect(
                new RectangleF(0.5f, 0.5f, form.Width - 1f, form.Height - 1f), 14))
                e.Graphics.DrawPath(pen, path);
        }
    }

    public class DialogHeader : Control
    {
        private readonly Form _form;
        private readonly WindowButton _close;
        private string _subtitle = "";

        public DialogHeader(Form form)
        {
            _form = form;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Bg;
            Height = 58;
            _close = new WindowButton();
            _close.Kind = WindowButton.ButtonKind.Close;
            _close.Size = new Size(46, 56);
            _close.Click += delegate { form.Close(); };
            Controls.Add(_close);
            Cursor = Cursors.SizeAll;
        }

        public string Subtitle
        {
            get { return _subtitle; }
            set { _subtitle = value; Invalidate(); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_close != null) _close.Location = new Point(Width - _close.Width, 0);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) WindowFx.Drag(_form);
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            using (SolidBrush brush = new SolidBrush(Theme.Bg)) e.Graphics.FillRectangle(brush, ClientRectangle);
            gdot(e.Graphics);
            Theme.DrawText(e.Graphics, "SkyMatch", Theme.FontBold, Theme.Text, 24, 13);
            Theme.DrawText(e.Graphics, String.IsNullOrEmpty(_subtitle) ? "Wallpapers and weather" : _subtitle,
                Theme.FontTiny, Theme.Dim, 24, 33);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }

        private static void gdot(Graphics g)
        {
            using (SolidBrush brush = new SolidBrush(Theme.Yellow))
            {
                g.FillEllipse(brush, 13, 23, 6, 6);
                g.FillEllipse(brush, 18, 19, 3, 3);
            }
        }
    }

    public class DetailPreview : Control
    {
        private WallpaperItem _wallpaper;

        public DetailPreview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public WallpaperItem Wallpaper
        {
            get { return _wallpaper; }
            set { _wallpaper = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            if (_wallpaper == null) return;
            RectangleF rect = new RectangleF(0, 0, Width, Height);
            using (GraphicsPath clip = Theme.RoundedRect(rect, 14))
            {
                e.Graphics.SetClip(clip);
                Bitmap image = AppImages.GetWallpaper(_wallpaper, Width, Height);
                if (image != null)
                {
                    e.Graphics.DrawImage(image, rect);
                }
                else
                {
                    using (LinearGradientBrush brush = new LinearGradientBrush(rect,
                        Color.FromArgb(28, 36, 48), Color.FromArgb(12, 17, 24), 90f))
                        e.Graphics.FillRectangle(brush, rect);
                }
                using (LinearGradientBrush shade = new LinearGradientBrush(rect,
                    Color.FromArgb(0, 8, 12, 18), Color.FromArgb(242, 8, 12, 18), 90f))
                {
                    Blend blend = new Blend();
                    blend.Factors = new[] { 0f, 0.2f, 1f };
                    blend.Positions = new[] { 0f, 0.48f, 1f };
                    shade.Blend = blend;
                    e.Graphics.FillRectangle(shade, rect);
                }
                WeatherKind kind = _wallpaper.MatchWeather;
                Color accent = Theme.Accent(kind);
                RectangleF badge = new RectangleF(14, 14, 74, 24);
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(accent, 60)))
                    Theme.FillRounded(e.Graphics, brush, badge, 12);
                WeatherGlyph.Draw(e.Graphics, kind, new RectangleF(badge.X + 8, badge.Y + 5, 14, 14), accent, 255);
                Theme.DrawText(e.Graphics, WeatherKindInfo.Name(kind), Theme.FontTiny, Theme.Text, badge.X + 26, badge.Y + 4);
                Color timeColor = Theme.TimeColor(_wallpaper.PrimaryTime);
                RectangleF timeBadge = new RectangleF(94, 14, 64, 24);
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(timeColor, 55)))
                    Theme.FillRounded(e.Graphics, brush, timeBadge, 12);
                Theme.DrawText(e.Graphics, TimeSlotInfo.Name(_wallpaper.PrimaryTime), Theme.FontTiny,
                    timeColor, timeBadge.X + 10, timeBadge.Y + 4);
                if (_wallpaper.IsActive)
                {
                    RectangleF active = new RectangleF(Width - 72, 14, 58, 24);
                    using (SolidBrush brush = new SolidBrush(Theme.Alpha(Theme.Green, 50)))
                        Theme.FillRounded(e.Graphics, brush, active, 12);
                    Theme.DrawText(e.Graphics, "In use", Theme.FontTiny, Theme.Green, active.X + 10, active.Y + 4);
                }
                string title = Theme.FitText(e.Graphics, _wallpaper.Title, Theme.FontBold, Width - 32);
                Theme.DrawText(e.Graphics, title, Theme.FontBold, Theme.Text, 16, Height - 58);
                string meta = _wallpaper.TypeLabel + "  ·  " + _wallpaper.Id;
                Theme.DrawText(e.Graphics, meta, Theme.FontTiny, Theme.Alpha(Theme.Text, 190), 16, Height - 32);
                e.Graphics.ResetClip();
            }
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                Theme.DrawRounded(e.Graphics, pen, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 14);
        }
    }

    public class DetailInfoPanel : Control
    {
        private WallpaperItem _wallpaper;

        public DetailInfoPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public WallpaperItem Wallpaper
        {
            get { return _wallpaper; }
            set { _wallpaper = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            if (_wallpaper == null) return;
            WeatherKind kind = _wallpaper.MatchWeather;
            Color accent = Theme.Accent(kind);
            Theme.DrawText(e.Graphics, "Identification basis", Theme.FontTiny, Theme.Alpha(accent, 235), 0, 0);
            Theme.DrawWrapped(e.Graphics, _wallpaper.Reason, Theme.FontBody, Theme.Text,
                new RectangleF(0, 21, Width, 64), 3);
            string auto = _wallpaper.IsManual
                ? "Weather: locked to \"" + WeatherKindInfo.Name(_wallpaper.MatchWeather) + "\""
                    + "  ·  Period: " + TimeSlotInfo.Name(_wallpaper.PrimaryTime)
                    + " " + Math.Round(_wallpaper.TimeConfidence) + "%"
                : "weather:" + Math.Round(_wallpaper.Confidence) + "%"
                    + "  ·  Period: " + TimeSlotInfo.Name(_wallpaper.PrimaryTime)
                    + " " + Math.Round(_wallpaper.TimeConfidence) + "%";
            Theme.DrawText(e.Graphics, auto, Theme.FontTiny, Theme.Muted, 0, 88);

            float tagX = 0;
            float tagY = 116;
            List<string> tags = new List<string>();
            tags.Add(_wallpaper.TypeLabel);
            if (_wallpaper.Tags != null) tags.AddRange(_wallpaper.Tags);
            for (int i = 0; i < tags.Count && i < 4; i++)
            {
                string tag = tags[i];
                SizeF size = e.Graphics.MeasureString(tag, Theme.FontTiny);
                float w = size.Width + 16;
                if (tagX + w > Width) break;
                RectangleF pill = new RectangleF(tagX, tagY, w, 22);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(24, 255, 255, 255)))
                    Theme.FillRounded(e.Graphics, brush, pill, 8);
                Theme.DrawText(e.Graphics, tag, Theme.FontTiny, Theme.Muted, tagX + 8, tagY + 3);
                tagX += w + 6;
            }

            Theme.DrawText(e.Graphics, "Picture characteristics", Theme.FontTiny, Theme.Dim, 0, 158);
            DrawMetric(e.Graphics, 0, 178, "brightness", _wallpaper.Brightness, Width / 2 - 10);
            DrawMetric(e.Graphics, Width / 2 + 10, 178, "saturation", _wallpaper.Saturation, Width / 2 - 10);
            string contrast = "contrast" + Math.Round(_wallpaper.Contrast * 100) + "%";
            Theme.DrawText(e.Graphics, contrast, Theme.FontTiny, Theme.Dim, 0, 208);
            if (_wallpaper.IsManual)
            {
                Theme.DrawText(e.Graphics, "Currently manually calibrated, this classification will be used first when the weather changes.",
                    Theme.FontTiny, Theme.Alpha(Theme.Yellow, 225), 0, 228);
            }
        }

        private void DrawMetric(Graphics g, float x, float y, string label, double value, float width)
        {
            Theme.DrawText(g, label, Theme.FontTiny, Theme.Dim, x, y);
            RectangleF track = new RectangleF(x, y + 17, width - 42, 5);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(34, 42, 55)))
                Theme.FillRounded(g, brush, track, 2.5f);
            using (SolidBrush brush = new SolidBrush(Theme.Alpha(Theme.Text, 190)))
                Theme.FillRounded(g, brush, new RectangleF(x, y + 17, Math.Max(3, (float)(track.Width * Math.Max(0, Math.Min(1, value)))), 5), 2.5f);
            Theme.DrawText(g, Math.Round(value * 100) + "%", Theme.FontTiny, Theme.Muted, x + width - 32, y);
        }
    }

    public class WallpaperDetailDialog : Form
    {
        private readonly AppController _controller;
        private readonly WallpaperItem _wallpaper;
        private readonly DialogHeader _header;
        private readonly DetailPreview _preview;
        private readonly DetailInfoPanel _info;
        private readonly ScoreBars _scores;
        private readonly TimeBars _timeBars;
        private readonly ChipStrip _chips;
        private readonly ToggleSwitch _hideToggle;
        private readonly Label _hideLabel;
        private readonly ActionButton _apply;
        private readonly ActionButton _cancel;
        private readonly ActionButton _block;
        private readonly bool _simplified;
        private readonly bool _embedded;
        private readonly Timer _outsideTimer;
        private OutsideClickFilter _outsideFilter;
        private DateTime _outsideArmedAt;
        private bool _updating;

        public WallpaperDetailDialog(AppController controller, WallpaperItem wallpaper)
            : this(controller, wallpaper, false, false)
        {
        }

        public WallpaperDetailDialog(AppController controller, WallpaperItem wallpaper, bool simplified)
            : this(controller, wallpaper, simplified, false)
        {
        }

        public WallpaperDetailDialog(AppController controller, WallpaperItem wallpaper, bool simplified,
            bool embedded)
        {
            _controller = controller;
            _wallpaper = wallpaper;
            _simplified = simplified;
            _embedded = embedded;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            Size = new Size(780, simplified ? 512 : 662);
            DoubleBuffered = true;
            KeyPreview = true;
            Font = Theme.FontBody;

            _header = new DialogHeader(this);
            _header.Subtitle = "Wallpaper details ·" + wallpaper.Id;
            _header.Dock = DockStyle.Top;
            _header.Visible = false;

            _preview = new DetailPreview();
            _preview.Wallpaper = wallpaper;
            _info = new DetailInfoPanel();
            _info.Wallpaper = wallpaper;

            _scores = new ScoreBars();
            _scores.Wallpaper = wallpaper;
            _scores.Columns = 4;

            _timeBars = new TimeBars();
            _timeBars.Wallpaper = wallpaper;
            if (simplified)
            {
                _scores.Visible = false;
                _timeBars.Visible = false;
            }

            _chips = new ChipStrip();
            _chips.ShowCounts = false;
            _chips.Items.Add(new ChipStrip.Chip("automatic recognition", null));
            foreach (WeatherKind kind in WeatherKindInfo.All)
                _chips.Items.Add(new ChipStrip.Chip(WeatherKindInfo.Name(kind), kind));
            _chips.SetSelectedSilent(wallpaper.ManualKind.HasValue ? wallpaper.ManualKind.Value + 1 : 0);
            _chips.SelectedIndexChanged += ChipsChanged;

            _hideToggle = new ToggleSwitch();
            _hideToggle.AccentColor = Theme.Yellow;
            _hideToggle.SetCheckedSilent(wallpaper.Hidden);
            _hideToggle.CheckedChanged += delegate
            {
                if (_updating) return;
                _controller.SetHidden(_wallpaper, _hideToggle.Checked);
                _hideLabel.Text = _hideToggle.Checked ? "Excluded from automatic matching" : "Exclude from automatic matching";
                if (_hideToggle.Checked) Close();
            };

            _hideLabel = new Label();
            _hideLabel.AutoSize = true;
            _hideLabel.BackColor = Color.Transparent;
            _hideLabel.ForeColor = Theme.Muted;
            _hideLabel.Font = Theme.FontSmall;
            _hideLabel.Text = wallpaper.Hidden ? "Excluded from automatic matching" : "Exclude from automatic matching";

            _apply = new ActionButton();
            _apply.Primary = true;
            _apply.Pill = true;
            _apply.Size = new Size(148, 38);
            _apply.AccentColor = Theme.Accent(wallpaper.MatchWeather);
            if (wallpaper.IsRemote)
            {
                _apply.Text = wallpaper.IsInstalled ? "Apply this wallpaper" : "Open subscription page";
                _apply.Enabled = true;
                if (!_simplified) _chips.Visible = false;
                _hideToggle.Visible = false;
                _hideLabel.Visible = false;
                _apply.Click += async delegate
                {
                    _apply.Enabled = false;
                    if (!_wallpaper.IsInstalled)
                    {
                        _controller.OpenWorkshopPage(_wallpaper);
                        Close();
                        return;
                    }
                    bool ok = await _controller.ApplyWorkshopItemAsync(_wallpaper);
                    if (ok) Close();
                    else _apply.Enabled = true;
                };
            }
            else
            {
                _apply.Text = wallpaper.IsActive ? "in use" : "Apply this wallpaper";
                _apply.Enabled = !wallpaper.IsActive;
                _apply.Click += async delegate
                {
                    _apply.Enabled = false;
                    bool ok = await _controller.ApplyWallpaperAsync(_wallpaper, false, "Apply from wallpaper gallery");
                    if (ok) Close();
                    else _apply.Enabled = true;
                };
            }

            if (_simplified)
            {
                _hideToggle.Visible = false;
                _hideLabel.Visible = false;
            }

            _cancel = new ActionButton();
            _cancel.Text = "Close";
            _cancel.Pill = true;
            _cancel.Size = new Size(82, 38);
            _cancel.Click += delegate { Close(); };

            _block = new ActionButton();
            _block.Text = "Block";
            _block.Pill = true;
            _block.Size = new Size(82, 38);
            _block.FillColor = Color.FromArgb(220, 58, 24, 30);
            _block.TextColor = Theme.Text;
            _block.AccentColor = Theme.Red;
            _block.Click += delegate
            {
                _controller.BlockWallpaper(_wallpaper);
                Close();
            };

            Controls.Add(_header);
            Controls.Add(_preview);
            Controls.Add(_info);
            Controls.Add(_scores);
            Controls.Add(_timeBars);
            Controls.Add(_chips);
            Controls.Add(_hideToggle);
            Controls.Add(_hideLabel);
            Controls.Add(_apply);
            Controls.Add(_cancel);
            Controls.Add(_block);

            _outsideTimer = new Timer();
            _outsideTimer.Interval = 80;
            _outsideTimer.Tick += delegate
            {
                if (!Visible || !IsHandleCreated) return;
                if (DateTime.Now < _outsideArmedAt) return;
                if ((Control.MouseButtons & MouseButtons.Left) != 0
                    && !Bounds.Contains(Cursor.Position))
                {
                    Close();
                }
            };
            Shown += delegate
            {
                if (!_embedded)
                {
                    _outsideArmedAt = DateTime.Now.AddMilliseconds(350);
                    _outsideTimer.Start();
                    if (_outsideFilter == null)
                    {
                        _outsideFilter = new OutsideClickFilter(this);
                        Application.AddMessageFilter(_outsideFilter);
                    }
                    WindowFx.ApplyRoundedCorners(this, 14);
                }
                else
                {
                    UpdateRoundedRegion();
                }
            };
            FormClosed += delegate
            {
                _outsideTimer.Stop();
                if (_outsideFilter != null)
                {
                    Application.RemoveMessageFilter(_outsideFilter);
                    _outsideFilter = null;
                }
            };

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
        }

        public void Relayout()
        {
            OnResize(EventArgs.Empty);
        }

        private void ChipsChanged(object sender, EventArgs e)
        {
            if (_updating) return;
            int index = _chips.SelectedIndex;
            if (index < 0) return;
            _updating = true;
            try
            {
                if (index == 0) _controller.SetManualKind(_wallpaper, null);
                else _controller.SetManualKind(_wallpaper, WeatherKindInfo.All[index - 1]);
                _header.Subtitle = "Wallpaper details ·" + _wallpaper.Id;
                _preview.Invalidate();
                _info.Invalidate();
                _scores.Invalidate();
                _timeBars.Invalidate();
                _apply.AccentColor = Theme.Accent(_wallpaper.MatchWeather);
                _apply.Text = _wallpaper.IsActive ? "in use" : "Apply this wallpaper";
                _apply.Enabled = !_wallpaper.IsActive;
            }
            finally
            {
                _updating = false;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            WindowFx.Repaint(this);
            BeginInvoke(new Action(delegate
            {
                if (!IsDisposed && IsHandleCreated) WindowFx.Repaint(this);
            }));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.Setup(e.Graphics);
            if (!_simplified)
            {
                Theme.DrawText(e.Graphics, "Weather suitability score", Theme.FontBold, Theme.Text, 20, 270);
                Theme.DrawText(e.Graphics, "Score based on screen brightness, color and text clues", Theme.FontTiny, Theme.Dim, 128, 275);
                Theme.DrawText(e.Graphics, "Time period adaptation score", Theme.FontBold, Theme.Text, 20, 404);
                Theme.DrawText(e.Graphics, "Combine the current time to determine morning, day, dusk and night", Theme.FontTiny, Theme.Dim, 128, 409);
                Theme.DrawText(e.Graphics, "Manual", Theme.FontBold, Theme.Text, 20, 494);
                Theme.DrawText(e.Graphics, "After selecting a type of weather, Auto Follow will only use it in that weather",
                    Theme.FontTiny, Theme.Dim, 92, 499);
            }
            else
            {
                WeatherKind bestWeather = WeatherKind.ClearDay;
                double bestWeatherScore = Double.MinValue;
                foreach (WeatherKind kind in WeatherKindInfo.All)
                {
                    double score = _wallpaper.VisualScore(kind);
                    if (score > bestWeatherScore)
                    {
                        bestWeatherScore = score;
                        bestWeather = kind;
                    }
                }
                TimeSlot bestTime = TimeSlot.Day;
                double bestTimeScore = Double.MinValue;
                foreach (TimeSlot slot in TimeSlotInfo.All)
                {
                    double score = _wallpaper.VisualTimeScore(slot);
                    if (score > bestTimeScore)
                    {
                        bestTimeScore = score;
                        bestTime = slot;
                    }
                }
                Theme.DrawText(e.Graphics, "Highest weather adaptation", Theme.FontTiny, Theme.Dim, 20, 324);
                Theme.DrawText(e.Graphics, WeatherKindInfo.Name(bestWeather) + "  "
                    + bestWeatherScore.ToString("0.0"), Theme.FontBold,
                    Theme.Accent(bestWeather), 118, 322);
                Theme.DrawText(e.Graphics, "Highest time period adaptation", Theme.FontTiny, Theme.Dim, 20, 346);
                Theme.DrawText(e.Graphics, TimeSlotInfo.Name(bestTime) + "  "
                    + bestTimeScore.ToString("0.0"), Theme.FontBold,
                    Theme.TimeColor(bestTime), 118, 344);

                Theme.DrawText(e.Graphics, "Manual", Theme.FontBold, Theme.Text, 20, 372);
                Theme.DrawText(e.Graphics, "After selecting a type of weather, Auto Follow will only use it in that weather",
                    Theme.FontTiny, Theme.Dim, 92, 377);
            }
            WindowFx.DrawBorder(this, e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_header == null || _preview == null || _info == null || _scores == null
                || _timeBars == null || _chips == null || _hideToggle == null || _hideLabel == null
                || _apply == null || _cancel == null || _block == null) return;
            _header.SetBounds(0, 0, Width, 1);
            int buttonY = _simplified ? 447 : 602;
            _preview.SetBounds(20, 10, 360, _simplified ? 300 : 250);
            _info.SetBounds(400, 10, 360, _simplified ? 300 : 250);
            _scores.SetBounds(20, 296, Width - 40, 100);
            _timeBars.SetBounds(20, 430, Width - 40, 54);
            if (_simplified)
            {
                _chips.SetBounds(20, 392, Width - 40, 34);
            }
            else
            {
                _chips.SetBounds(20, 516, Width - 40, 34);
                _hideToggle.SetBounds(20, 566, 44, 24);
                _hideLabel.Location = new Point(72, 569);
            }
            _apply.Location = new Point(Width - 20 - _apply.Width, buttonY);
            _cancel.Location = new Point(Width - 20 - _apply.Width - _cancel.Width - 10, buttonY);
            _block.Location = new Point(_cancel.Left - _block.Width - 10, buttonY);
            if (_embedded) UpdateRoundedRegion();
            else WindowFx.ApplyRoundedCorners(this, 14);
        }

        private void UpdateRoundedRegion()
        {
            using (GraphicsPath path = Theme.RoundedRect(
                new RectangleF(0, 0, Width, Height), 14))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        [DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr parent, IntPtr child);

        private class OutsideClickFilter : IMessageFilter
        {
            private readonly WallpaperDetailDialog _dialog;

            public OutsideClickFilter(WallpaperDetailDialog dialog)
            {
                _dialog = dialog;
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x0201 && m.Msg != 0x0204 && m.Msg != 0x0207) return false;
                if (_dialog == null || _dialog.IsDisposed || !_dialog.Visible
                    || !_dialog.IsHandleCreated) return false;
                if (m.HWnd == _dialog.Handle || IsChild(_dialog.Handle, m.HWnd)) return false;
                // Swallow the outside click and close without letting the
                // disabled owner process it (which can trigger the Windows
                // "ding" sound).
                _dialog.BeginInvoke(new Action(_dialog.Close));
                return true;
            }
        }
    }

    public class CityRow : Control
    {
        private LocationInfo _location;
        private bool _hover;

        public CityRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 64;
            Cursor = Cursors.Hand;
        }

        public LocationInfo City
        {
            get { return _location; }
            set { _location = value; Invalidate(); }
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            if (_location == null) return;
            RectangleF rect = new RectangleF(0, 0, Width, Height - 4);
            using (SolidBrush brush = new SolidBrush(_hover ? Color.FromArgb(27, 34, 45) : Color.FromArgb(19, 24, 33)))
                Theme.FillRounded(e.Graphics, brush, rect, 10);
            using (SolidBrush dot = new SolidBrush(Theme.Yellow))
                e.Graphics.FillEllipse(dot, 16, 22, 5, 5);
            Theme.DrawText(e.Graphics, _location.Name, Theme.FontBold, Theme.Text, 31, 12);
            string sub = _location.Admin1;
            if (!String.IsNullOrEmpty(_location.Country) && _location.Country != _location.Admin1)
                sub += (String.IsNullOrEmpty(sub) ? "" : " · ") + _location.Country;
            Theme.DrawText(e.Graphics, sub, Theme.FontTiny, Theme.Muted, 31, 35);
            string coords = _location.Latitude.ToString("0.00") + ", " + _location.Longitude.ToString("0.00");
            SizeF size = e.Graphics.MeasureString(coords, Theme.FontTiny);
            Theme.DrawText(e.Graphics, coords, Theme.FontTiny, Theme.Dim, Width - size.Width - 16, 23);
        }
    }

    public class CitySearchDialog : Form
    {
        private readonly AppController _controller;
        private readonly DialogHeader _header;
        private readonly SearchBox _search;
        private readonly ActionButton _go;
        private readonly Panel _results;
        private readonly Label _status;
        private bool _busy;

        public LocationInfo SelectedLocation { get; set; }

        public CitySearchDialog(AppController controller)
        {
            _controller = controller;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            Size = new Size(560, 540);
            DoubleBuffered = true;
            KeyPreview = true;
            Font = Theme.FontBody;

            _header = new DialogHeader(this);
            _header.Subtitle = "Search cities · Accurate to the city level";
            _header.Dock = DockStyle.Top;

            _search = new SearchBox();
            _search.Placeholder = "Enter the city name, such as Nanjing / Tokyo / London";
            _search.Size = new Size(380, 38);
            _search.InnerTextBox.KeyDown += async delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) await SearchAsync();
            };

            _go = new ActionButton();
            _go.Text = "Search";
            _go.Primary = true;
            _go.Pill = true;
            _go.Size = new Size(96, 38);
            _go.AccentColor = Theme.Yellow;
            _go.Click += async delegate { await SearchAsync(); };

            _status = new Label();
            _status.AutoSize = false;
            _status.BackColor = Color.Transparent;
            _status.ForeColor = Theme.Dim;
            _status.Font = Theme.FontSmall;
            _status.Text = "After searching, click on the result to switch locations.";
            _status.TextAlign = ContentAlignment.MiddleLeft;

            _results = new Panel();
            _results.AutoScroll = true;
            _results.BackColor = Color.Transparent;

            Controls.Add(_header);
            Controls.Add(_search);
            Controls.Add(_go);
            Controls.Add(_status);
            Controls.Add(_results);
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
        }

        private void ShowHistory()
        {
            _results.Controls.Clear();
            List<LocationInfo> history = _controller.GetLocationHistory();
            if (history == null || history.Count == 0) return;
            _status.ForeColor = Theme.Dim;
            _status.Text = "Recent searches · Click to switch directly";
            Label title = new Label();
            title.Text = "Recent searches";
            title.AutoSize = false;
            title.BackColor = Color.Transparent;
            title.ForeColor = Theme.Dim;
            title.Font = Theme.FontTiny;
            title.SetBounds(4, 0, _results.ClientSize.Width - 20, 22);
            _results.Controls.Add(title);
            int y = 28;
            foreach (LocationInfo location in history)
            {
                CityRow row = new CityRow();
                row.City = location;
                row.SetBounds(0, y, _results.ClientSize.Width - 20, 64);
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                row.Click += delegate
                {
                    SelectedLocation = location;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                _results.Controls.Add(row);
                y += 68;
            }
            try { _results.AutoScrollPosition = Point.Empty; } catch { }
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            if (_busy) return;
            string query = _search.Text == null ? "" : _search.Text.Trim();
            if (query.Length == 0)
            {
                _status.Text = "Please enter a city name.";
                return;
            }
            _busy = true;
            _go.Enabled = false;
            _status.ForeColor = Theme.Muted;
            _status.Text = "Searching for \"" + query + "」…";
            try
            {
                List<LocationInfo> results = await _controller.SearchCitiesAsync(query);
                _results.Controls.Clear();
                if (results.Count == 0)
                {
                    _status.Text = "This city is not found, try another way of writing it.";
                    return;
                }
                _status.Text = "turn up" + results.Count + "results, click to select.";
                int y = 0;
                foreach (LocationInfo location in results)
                {
                    CityRow row = new CityRow();
                    row.City = location;
                    row.SetBounds(0, y, _results.ClientSize.Width - 20, 64);
                    row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    row.Click += delegate
                    {
                        SelectedLocation = location;
                        DialogResult = DialogResult.OK;
                        Close();
                    };
                    _results.Controls.Add(row);
                    y += 68;
                }
            }
            catch (Exception ex)
            {
                _status.ForeColor = Theme.Red;
                _status.Text = "Search failed:" + ex.Message;
            }
            finally
            {
                _busy = false;
                _go.Enabled = true;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            WindowFx.Repaint(this);
            BeginInvoke(new Action(delegate
            {
                if (!IsDisposed && IsHandleCreated) WindowFx.Repaint(this);
            }));
            ShowHistory();
            _search.InnerTextBox.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            WindowFx.DrawBorder(this, e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_header == null || _search == null || _go == null || _status == null || _results == null) return;
            _header.SetBounds(0, 0, Width, 58);
            _search.SetBounds(22, 76, Width - 146, 38);
            _go.SetBounds(Width - 116, 76, 94, 38);
            _status.SetBounds(22, 124, Width - 44, 28);
            _results.SetBounds(22, 158, Width - 44, Height - 180);
        }
    }
}
