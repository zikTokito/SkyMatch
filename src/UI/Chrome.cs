using System;
using System.Drawing;
using System.Windows.Forms;

namespace SkyMatch
{
    public class TitleBar : Control
    {
        private readonly Form _form;
        private readonly WindowButton _min;
        private readonly WindowButton _max;
        private readonly WindowButton _close;

        public TitleBar(Form form)
        {
            _form = form;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Bg;
            Height = 56;
            _min = new WindowButton();
            _min.Kind = WindowButton.ButtonKind.Minimize;
            _min.Click += delegate { form.WindowState = FormWindowState.Minimized; };
            _max = new WindowButton();
            _max.Kind = WindowButton.ButtonKind.Maximize;
            _max.Click += delegate
            {
                form.WindowState = form.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
            };
            _close = new WindowButton();
            _close.Kind = WindowButton.ButtonKind.Close;
            _close.Click += delegate { form.Close(); };
            Controls.Add(_min);
            Controls.Add(_max);
            Controls.Add(_close);
            Cursor = Cursors.SizeAll;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_min == null) return;
            _close.Location = new Point(Width - _close.Width, 0);
            _max.Location = new Point(Width - _close.Width - _max.Width, 0);
            _min.Location = new Point(Width - _close.Width - _max.Width - _min.Width, 0);
            _max.IsMaximized = _form.WindowState == FormWindowState.Maximized;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Clicks == 1) WindowFx.Drag(_form);
            base.OnMouseDown(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            _form.WindowState = _form.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
            base.OnDoubleClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            using (SolidBrush bg = new SolidBrush(Theme.Bg)) e.Graphics.FillRectangle(bg, ClientRectangle);
            RectangleF logo = new RectangleF(20, 13, 30, 30);
            using (System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                logo, Color.FromArgb(255, 208, 116), Color.FromArgb(238, 146, 71), 55f))
                Theme.FillRounded(e.Graphics, brush, logo, 9);
            WeatherGlyph.Draw(e.Graphics, WeatherKind.ClearDay,
                new RectangleF(logo.X + 4, logo.Y + 4, 22, 22), Color.FromArgb(20, 24, 30), 255);
            Theme.DrawText(e.Graphics, "SkyMatch", Theme.FontBold, Theme.Text, 59, 11);
            Theme.DrawText(e.Graphics, "Sky-aware · Weather Wallpaper", Theme.FontTiny, Theme.Dim, 60, 31);

            if (Width > 1000)
            {
                string center = "OUTDOOR WEATHER   /   DESKTOP WALLPAPER";
                SizeF size = e.Graphics.MeasureString(center, Theme.FontTiny);
                Theme.DrawText(e.Graphics, center, Theme.FontTiny, Color.FromArgb(60, 72, 86),
                    (Width - size.Width) / 2f, 23);
            }
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    public class Sidebar : Control
    {
        private readonly NavButton[] _nav;
        private readonly ToggleSwitch _autoToggle;
        private readonly VolumeSlider _volume;
        private readonly string[] _captions = { "Weather", "Library", "Workshop", "Settings" };
        private int _activePage;
        private RectangleF _engineRect;
        private RectangleF _volumeRect;
        private bool _engineHover;
        private bool _engineReady;
        private bool _engineRunning;
        private int _wallpaperCount;
        private int _volumePercent = 100;
        private bool _muted;

        public event Action<int> Navigate;
        public event EventHandler AutoFollowChanged;
        public event EventHandler SettingsRequested;
        public event Action<int> VolumeChanged;
        public event Action<int> VolumeCommitted;
        public event Action<bool> MuteChanged;

        public Sidebar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Sidebar;
            _nav = new NavButton[4];
            for (int i = 0; i < 4; i++)
            {
                NavButton button = new NavButton();
                button.Caption = _captions[i];
                button.IconIndex = i;
                button.Width = 176;
                button.Location = new Point(16, 40 + i * 50);
                int page = i;
                button.Click += delegate { SetActivePage(page); };
                _nav[i] = button;
                Controls.Add(button);
            }
            _nav[0].Active = true;

            _autoToggle = new ToggleSwitch();
            _autoToggle.AccentColor = Theme.Green;
            _autoToggle.CheckedChanged += delegate
            {
                EventHandler handler = AutoFollowChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            Controls.Add(_autoToggle);

            _volume = new VolumeSlider();
            _volume.ValueChanged += delegate
            {
                _volumePercent = _volume.Value;
                Invalidate();
                Action<int> changed = VolumeChanged;
                if (changed != null) changed(_volume.Value);
            };
            _volume.VolumeCommitted += delegate
            {
                Action<int> handler = VolumeCommitted;
                if (handler != null) handler(_volume.Value);
            };
            _volume.MuteChanged += delegate
            {
                _muted = _volume.Muted;
                Action<bool> handler = MuteChanged;
                if (handler != null) handler(_volume.Muted);
            };
            Controls.Add(_volume);
        }

        public void SetAudio(int volume, bool muted)
        {
            _volumePercent = volume;
            _muted = muted;
            _volume.SetSilent(volume, muted);
            Invalidate();
        }

        public bool AutoFollow
        {
            get { return _autoToggle.Checked; }
            set { _autoToggle.SetCheckedSilent(value); Invalidate(); }
        }

        public void SetState(bool engineReady, bool engineRunning, int wallpaperCount, int refreshMinutes)
        {
            _engineReady = engineReady;
            _engineRunning = engineRunning;
            _wallpaperCount = wallpaperCount;
            Invalidate();
        }

        public void SetActivePage(int page)
        {
            _activePage = page;
            for (int i = 0; i < _nav.Length; i++) _nav[i].Active = i == page;
            Action<int> handler = Navigate;
            if (handler != null) handler(page);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_nav == null) return;
            for (int i = 0; i < _nav.Length; i++)
                _nav[i].SetBounds(16, 40 + i * 50, Width - 32, 44);
            int autoTop = Math.Min(266, Math.Max(210, Height - 306));
            _autoToggle.Location = new Point(Width - _autoToggle.Width - 18, autoTop + 17);
            int engineTop = Height - 146;
            int volumeTop = autoTop + 116;
            int volumeHeight = 96;
            if (volumeTop + volumeHeight > engineTop - 12)
            {
                volumeTop = autoTop + 104;
                volumeHeight = Math.Max(80, engineTop - volumeTop - 12);
            }
            _volumeRect = new RectangleF(16, volumeTop, Width - 32, volumeHeight);
            _volume.SetBounds((int)_volumeRect.X + 14, (int)_volumeRect.Y + 46,
                Math.Max(80, (int)_volumeRect.Width - 28), 26);
            _engineRect = new RectangleF(16, engineTop, Width - 32, 122);
        }

        private RectangleF AutoCardRect
        {
            get
            {
                int autoTop = Math.Min(266, Math.Max(210, Height - 306));
                return new RectangleF(16, autoTop, Width - 32, 98);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hover = _engineRect.Contains(e.Location);
            if (hover != _engineHover)
            {
                _engineHover = hover;
                Cursor = hover ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _engineHover = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _engineRect.Contains(e.Location))
            {
                EventHandler handler = SettingsRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            using (SolidBrush bg = new SolidBrush(Theme.Sidebar)) e.Graphics.FillRectangle(bg, ClientRectangle);
            using (Pen border = new Pen(Theme.BorderSoft, 1f))
                e.Graphics.DrawLine(border, Width - 1, 0, Width - 1, Height);

            Theme.DrawText(e.Graphics, "Workspace", Theme.FontTiny, Theme.Dim, 20, 18);

            RectangleF auto = AutoCardRect;
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(17, 22, 30)))
                Theme.FillRounded(e.Graphics, brush, auto, 13);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                Theme.DrawRounded(e.Graphics, pen, auto, 13);
            Theme.DrawText(e.Graphics, "Auto-follow", Theme.FontBold, Theme.Text, auto.X + 14, auto.Y + 14);
            Theme.DrawText(e.Graphics, "Follow weather changes", Theme.FontTiny, Theme.Dim, auto.X + 14, auto.Y + 36);
            using (SolidBrush dot = new SolidBrush(AutoFollow ? Theme.Green : Theme.Dim))
                e.Graphics.FillEllipse(dot, auto.X + 14, auto.Y + 64, 6, 6);
            Theme.DrawText(e.Graphics, (AutoFollow ? "Running" : "Suspended") + "  ·  Hourly weather",
                Theme.FontTiny, AutoFollow ? Theme.Alpha(Theme.Green, 230) : Theme.Dim, auto.X + 26, auto.Y + 59);

            RectangleF volume = _volumeRect;
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(17, 22, 30)))
                Theme.FillRounded(e.Graphics, brush, volume, 13);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                Theme.DrawRounded(e.Graphics, pen, volume, 13);
            Theme.DrawText(e.Graphics, "Desktop volume", Theme.FontBold, Theme.Text, volume.X + 14, volume.Y + 12);
            Theme.DrawText(e.Graphics, _muted ? "mute" : _volumePercent + "%", Theme.FontTiny,
                _muted ? Theme.Red : Theme.TimeColor(TimeSlot.Dusk), volume.X + 78, volume.Y + 15);
            if (volume.Height >= 90)
            {
                Theme.DrawText(e.Graphics, "Wallpaper Engine audio only", Theme.FontTiny, Theme.Dim,
                    volume.X + 14, volume.Y + 72);
            }

            RectangleF engine = _engineRect;
            using (SolidBrush brush = new SolidBrush(_engineHover ? Color.FromArgb(23, 29, 39) : Color.FromArgb(15, 20, 28)))
                Theme.FillRounded(e.Graphics, brush, engine, 13);
            using (Pen pen = new Pen(_engineHover ? Theme.Border : Theme.BorderSoft, 1f))
                Theme.DrawRounded(e.Graphics, pen, engine, 13);
            Theme.DrawText(e.Graphics, "WALLPAPER ENGINE", Theme.FontTiny, Theme.Dim, engine.X + 14, engine.Y + 13);
            Color dotColor = !_engineReady ? Theme.Red : (_engineRunning ? Theme.Green : Theme.Yellow);
            using (SolidBrush dot = new SolidBrush(dotColor))
                e.Graphics.FillEllipse(dot, engine.X + 14, engine.Y + 43, 7, 7);
            string line = !_engineReady ? "Not connected" : (_engineRunning ? "Connected" : "Waiting to start");
            Theme.DrawText(e.Graphics, line, Theme.FontSmall, Theme.Text, engine.X + 28, engine.Y + 36);
            Theme.DrawText(e.Graphics, _wallpaperCount + " wallpapers matched", Theme.FontTiny, Theme.Muted,
                engine.X + 14, engine.Y + 61);
            Theme.DrawText(e.Graphics, "Open settings →", Theme.FontTiny, Theme.Alpha(Theme.Yellow, 210),
                engine.X + 14, engine.Y + 88);

            Theme.DrawText(e.Graphics, "v1.2.0  ·  LOCAL ONLY", Theme.FontTiny, Color.FromArgb(62, 73, 87),
                20, Height - 22);
        }
    }

    public class StatusBar : Control
    {
        private string _message = "Starting...";
        private string _rightText = "";
        private bool _isError;
        private bool _isBusy;

        public StatusBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Bg;
            Height = 30;
        }

        public string Message
        {
            get { return _message; }
            set { _message = value == null ? "" : value; Invalidate(); }
        }

        public string RightText
        {
            get { return _rightText; }
            set { _rightText = value == null ? "" : value; Invalidate(); }
        }

        public bool IsError
        {
            get { return _isError; }
            set { _isError = value; Invalidate(); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set { _isBusy = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            using (SolidBrush bg = new SolidBrush(Theme.Bg)) e.Graphics.FillRectangle(bg, ClientRectangle);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                e.Graphics.DrawLine(pen, 0, 0, Width, 0);
            Color dot = _isError ? Theme.Red : (_isBusy ? Theme.Yellow : Theme.Green);
            using (SolidBrush brush = new SolidBrush(dot))
                e.Graphics.FillEllipse(brush, 24, Height / 2f - 3, 6, 6);
            float rightWidth = 0;
            if (!String.IsNullOrEmpty(_rightText))
            {
                SizeF rs = e.Graphics.MeasureString(_rightText, Theme.FontTiny);
                rightWidth = rs.Width;
                Theme.DrawText(e.Graphics, _rightText, Theme.FontTiny, Theme.Dim, Width - rightWidth - 24,
                    Height / 2f - 7);
            }
            RectangleF messageRect = new RectangleF(40, 0, Width - 40 - rightWidth - 50, Height);
            using (StringFormat format = new StringFormat())
            using (SolidBrush brush = new SolidBrush(_isError ? Theme.Red : Theme.Muted))
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                format.LineAlignment = StringAlignment.Center;
                e.Graphics.DrawString(_message, Theme.FontTiny, brush, messageRect, format);
            }
        }
    }
}
