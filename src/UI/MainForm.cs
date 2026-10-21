using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SkyMatch
{
    public class MainForm : Form
    {
        private const int WM_NCHITTEST = 0x84;
        private const int WM_GETMINMAXINFO = 0x24;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private readonly AppController _controller;
        private readonly bool _startMinimized;
        private readonly string _initialDetailId;
        private readonly int _initialPage;
        private readonly TitleBar _title;
        private readonly Sidebar _sidebar;
        private readonly StatusBar _status;
        private readonly Panel _host;
        private readonly DashboardPage _dashboard;
        private readonly LibraryPage _library;
        private readonly WorkshopPage _workshop;
        private SettingsPage _settings;
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _trayAuto;
        private readonly Timer _timer;
        private readonly Timer _clockTimer;
        private readonly Timer _repaintTimer;
        private readonly Timer _subscriptionTimer;

        private bool _initialized;
        private bool _reallyExit;
        private bool _hintShown;
        private bool _wasMinimized;
        private FormWindowState _lastWindowState = FormWindowState.Normal;
        private int _page;
        private int _repaintTicks;
        private Panel _detailOverlay;
        private WallpaperDetailDialog _detailDialog;
        private StartupOverlay _startupOverlay;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Compose the whole client area as one surface. This prevents the unpainted
                // holes that can appear when a borderless window is first shown or restored.
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                // A borderless form otherwise lacks the system menu/minimize styles, which
                // makes clicking its taskbar button do nothing instead of minimizing it.
                cp.Style |= 0x00020000; // WS_MINIMIZEBOX
                cp.Style |= 0x00080000; // WS_SYSMENU
                return cp;
            }
        }

        public MainForm(bool startMinimized, string initialDetailId, int initialPage)
        {
            _startMinimized = startMinimized;
            _initialDetailId = initialDetailId == null ? "" : initialDetailId;
            _initialPage = initialPage;
            _controller = new AppController();

            Text = "SkyMatch · Weather Wallpaper";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Rectangle working = Screen.PrimaryScreen.WorkingArea;
            int startWidth = Math.Min(1440, Math.Max(1000, working.Width - 100));
            int startHeight = Math.Min(920, Math.Max(700, working.Height - 100));
            Size = new Size(startWidth, startHeight);
            MinimumSize = new Size(960, 720);
            MaximumSize = Size.Empty;
            MaximizeBox = true;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.FontBody;
            DoubleBuffered = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { Icon = SystemIcons.Application; }

            _title = new TitleBar(this);
            _title.Dock = DockStyle.Top;
            _status = new StatusBar();
            _status.Dock = DockStyle.Bottom;
            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Bg;
            _sidebar = new Sidebar();
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = 208;
            _host = new Panel();
            _host.Dock = DockStyle.Fill;
            _host.BackColor = Theme.Bg;
            _host.Padding = new Padding(24, 16, 24, 12);

            Controls.Add(body);
            Controls.Add(_status);
            Controls.Add(_title);
            body.Controls.Add(_host);
            body.Controls.Add(_sidebar);

            _dashboard = new DashboardPage(_controller);
            _dashboard.Dock = DockStyle.Fill;
            _library = new LibraryPage(_controller);
            _library.Dock = DockStyle.Fill;
            _workshop = new WorkshopPage(_controller);
            _workshop.Dock = DockStyle.Fill;
            _host.Controls.Add(_dashboard);
            _host.Controls.Add(_library);
            _host.Controls.Add(_workshop);
            _dashboard.Visible = true;
            _library.Visible = false;
            _workshop.Visible = false;

            _startupOverlay = new StartupOverlay();
            Controls.Add(_startupOverlay);
            _startupOverlay.BringToFront();

            _dashboard.ConditionClicked += delegate(WeatherKind kind)
            {
                _sidebar.SetActivePage(1);
                _library.FocusWeather(kind);
            };
            _dashboard.DetailsRequested += delegate(WallpaperItem item) { ShowDetail(item, false); };
            _dashboard.SimpleDetailsRequested += delegate(WallpaperItem item) { ShowDetail(item, true); };
            _library.DetailsRequested += delegate(WallpaperItem item) { ShowDetail(item, false); };
            _sidebar.Navigate += SwitchPage;
            _sidebar.SettingsRequested += delegate { _sidebar.SetActivePage(3); };
            _sidebar.VolumeChanged += delegate(int volume) { _controller.SetWallpaperVolumeLive(volume); };
            _sidebar.VolumeCommitted += delegate(int volume) { _controller.SetWallpaperVolume(volume); };
            _sidebar.MuteChanged += delegate(bool muted) { _controller.SetWallpaperMuted(muted); };
            _sidebar.AutoFollowChanged += async delegate
            {
                bool enabled = _sidebar.AutoFollow;
                _controller.SetAutoFollow(enabled);
                if (enabled && !_controller.Settings.HasAppliedOnce && _controller.Weather != null)
                    await _controller.MatchCurrentWeatherAsync();
            };

            _controller.WeatherUpdated += delegate { if (!IsDisposed) UpdateChrome(); };
            _controller.WallpapersUpdated += delegate { if (!IsDisposed) UpdateChrome(); };
            _controller.StateChanged += delegate { if (!IsDisposed) UpdateChrome(); };
            _controller.StatusChanged += delegate(object sender, AppStatusEventArgs e)
            {
                if (IsDisposed) return;
                _status.Message = e.Message;
                _status.IsError = e.IsError;
            };
            _controller.WallpaperApplied += delegate(object sender, WallpaperAppliedEventArgs e)
            {
                if (!IsDisposed) OnApplied(e);
            };
            _controller.ActiveWallpaperChanged += delegate
            {
                if (IsDisposed) return;
                UpdateChrome();
                if (_dashboard != null && _dashboard.Visible) _dashboard.RefreshAppliedState();
            };

            _trayMenu = new ContextMenuStrip();
            _trayMenu.ShowImageMargin = false;
            _trayMenu.BackColor = Color.FromArgb(20, 26, 35);
            _trayMenu.ForeColor = Theme.Text;
            _trayMenu.Font = Theme.FontSmall;
            _trayMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            ToolStripMenuItem open = new ToolStripMenuItem("Open SkyMatch");
            open.Font = Theme.FontBold;
            open.Click += delegate { ShowFromTray(); };
            ToolStripMenuItem match = new ToolStripMenuItem("Match by weather now");
            match.Click += async delegate
            {
                ShowFromTray();
                await _controller.MatchCurrentWeatherAsync();
            };
            _trayAuto = new ToolStripMenuItem("Auto-follow");
            _trayAuto.CheckOnClick = false;
            _trayAuto.Click += delegate
            {
                bool enabled = !_controller.Settings.AutoFollow;
                _controller.SetAutoFollow(enabled);
                if (enabled)
                {
                    Task ignored = _controller.MatchCurrentWeatherAsync();
                }
            };
            ToolStripMenuItem restore = new ToolStripMenuItem("Restore original wallpaper");
            restore.Click += async delegate { await _controller.RestoreOriginalAsync(); };
            ToolStripMenuItem exit = new ToolStripMenuItem("quit");
            exit.Click += delegate { ExitApplication(); };
            _trayMenu.Items.Add(open);
            _trayMenu.Items.Add(match);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_trayAuto);
            _trayMenu.Items.Add(restore);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(exit);

            _tray = new NotifyIcon();
            _tray.Icon = Icon;
            _tray.Text = "SkyMatch · Synchronizing weather";
            _tray.Visible = true;
            _tray.ContextMenuStrip = _trayMenu;
            _tray.DoubleClick += delegate { ShowFromTray(); };

            _timer = new Timer();
            _timer.Interval = 30000;
            _timer.Tick += async delegate
            {
                UpdateChrome();
                if (_initialized)
                {
                    await _controller.RefreshIfDueAsync();
                    await _controller.CheckTimeChangeAsync();
                    await _controller.ResumeDeferredAutoFollowAsync();
                }
            };
            _timer.Start();

            _clockTimer = new Timer();
            _clockTimer.Interval = 1000;
            _clockTimer.Tick += delegate
            {
                if (!_initialized || !Visible || _status == null || IsDisposed) return;
                _status.RightText = BuildStatusRight();
            };
            _clockTimer.Start();

            _subscriptionTimer = new Timer();
            _subscriptionTimer.Interval = 5000;
            _subscriptionTimer.Tick += async delegate
            {
                if (!_initialized) return;
                try
                {
                    _controller.EnsureWallpaperAudio();
                    _controller.CheckActiveWallpaper();
                    await _controller.CheckWorkshopDownloadsAsync();
                    await _controller.CheckWorkshopRefreshDueAsync();
                    await _controller.CheckRecommendationRotationAsync();
                }
                catch (Exception ex) { Log.Warn("Background sync failed:" + ex.Message); }
            };
            _subscriptionTimer.Start();

            _repaintTimer = new Timer();
            _repaintTimer.Interval = 90;
            _repaintTimer.Tick += delegate
            {
                _repaintTicks++;
                WindowFx.Repaint(this);
                if (_repaintTicks >= 4) _repaintTimer.Stop();
            };

            Shown += MainForm_Shown;
            FormClosing += MainForm_FormClosing;
            Resize += delegate
            {
                UpdateChrome();
                bool stateChanged = _lastWindowState != WindowState;
                _lastWindowState = WindowState;
                if (stateChanged)
                {
                    ApplyWindowChrome();
                    BeginInvoke(new Action(delegate
                    {
                        if (IsDisposed) return;
                        PerformLayout();
                        _host.PerformLayout();
                        _host.Invalidate(true);
                        _sidebar.Invalidate(true);
                        Update();
                        QueueRepaint();
                    }));
                }
                else if (WindowState == FormWindowState.Normal)
                {
                    WindowFx.ApplyRoundedCorners(this, 14);
                }
                if (WindowState == FormWindowState.Maximized || (_wasMinimized && WindowState != FormWindowState.Minimized))
                    QueueRepaint();
                _wasMinimized = WindowState == FormWindowState.Minimized;
            };
            Activated += delegate { QueueRepaint(); };
            VisibleChanged += delegate { if (Visible) QueueRepaint(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.F5)
                {
                    Task ignored = _controller.RefreshWeatherAsync(true);
                }
            };
            UpdateChrome();
        }

        private void QueueRepaint()
        {
            if (_repaintTimer == null || !IsHandleCreated || !Visible) return;
            WindowFx.Repaint(this);
            _repaintTicks = 0;
            _repaintTimer.Stop();
            _repaintTimer.Start();
        }

        private void ApplyWindowChrome()
        {
            if (!IsHandleCreated) return;
            bool normal = WindowState == FormWindowState.Normal;
            WindowFx.SetComposited(this, normal);
            WindowFx.ApplyRoundedCorners(this, normal ? 14 : 0);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (e != null && e.Graphics != null)
            {
                e.Graphics.Clear(Theme.Bg);
                return;
            }
            base.OnPaintBackground(e);
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            if (_startMinimized) Hide();
            ApplyWindowChrome();
            if (_startupOverlay != null)
            {
                _startupOverlay.Visible = true;
                _startupOverlay.BringToFront();
            }
            QueueRepaint();
            if (_initialized) return;
            _initialized = true;
            try
            {
                await _controller.InitializeAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Initialization failed", ex);
                _status.Message = "Initialization failed:" + ex.Message;
                _status.IsError = true;
            }
            _dashboard.RefreshData();
            HideStartupOverlay();
            UpdateChrome();
            QueueRepaint();
            _library.RefreshView();
            if (_startMinimized && _controller.Settings.ShowNotifications)
            {
                _tray.ShowBalloonTip(2500, "SkyMatch is running in the background",
                    "The wallpaper will automatically change in the background when the weather changes. Double-click the tray icon to open it.", ToolTipIcon.None);
            }
            if (_initialPage == 4) _sidebar.SetActivePage(3);
            else if (_initialPage > 0) _sidebar.SetActivePage(_initialPage);
            if (_initialPage == 4)
            {
                using (CitySearchDialog dialog = new CitySearchDialog(_controller))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedLocation != null)
                        await _controller.SetLocationAsync(dialog.SelectedLocation);
                }
            }
            if (!String.IsNullOrEmpty(_initialDetailId))
            {
                WallpaperItem target = _controller.FindWallpaper(_initialDetailId);
                if (target != null) BeginInvoke(new Action(delegate { ShowDetail(target, false); }));
            }
        }

        private void HideStartupOverlay()
        {
            if (_startupOverlay == null) return;
            _startupOverlay.Visible = false;
            _startupOverlay.Dispose();
            _startupOverlay = null;
        }

        private void ShowDetail(WallpaperItem item, bool simplified)
        {
            if (item == null || _detailOverlay != null) return;
            if (_controller.IsBlocked(item)) return;
            Panel overlay = new Panel();
            overlay.Dock = DockStyle.None;
            overlay.Bounds = ClientRectangle;
            overlay.BackColor = Color.FromArgb(8, 11, 16);
            overlay.TabStop = true;
            Controls.Add(overlay);
            overlay.BringToFront();

            WallpaperDetailDialog dialog = new WallpaperDetailDialog(_controller, item, simplified, true);
            dialog.TopLevel = false;
            dialog.FormBorderStyle = FormBorderStyle.None;
            dialog.StartPosition = FormStartPosition.Manual;
            dialog.ShowInTaskbar = false;
            overlay.Controls.Add(dialog);

            Action center = delegate
            {
                if (dialog.IsDisposed || overlay.IsDisposed) return;
                dialog.Location = new Point(
                    Math.Max(0, (overlay.ClientSize.Width - dialog.Width) / 2),
                    Math.Max(0, (overlay.ClientSize.Height - dialog.Height) / 2));
            };
            overlay.Resize += delegate { center(); };
            EventHandler fitOverlay = delegate
            {
                if (!overlay.IsDisposed)
                {
                    overlay.Bounds = ClientRectangle;
                    center();
                }
            };
            Resize += fitOverlay;
            overlay.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    overlay.Capture = true;
                }
            };
            overlay.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                overlay.Capture = false;
                // Let the full mouse-up/click cycle finish on the overlay, then
                // close. This prevents the click leaking through to a card
                // behind the overlay.
                Action closeOverlay = delegate
                {
                    if (!dialog.IsDisposed) dialog.Close();
                };
                if (overlay.IsHandleCreated) overlay.BeginInvoke(closeOverlay);
                else closeOverlay();
            };
            dialog.FormClosed += delegate
            {
                Resize -= fitOverlay;
                if (_detailDialog == dialog)
                {
                    _detailDialog = null;
                    if (_detailOverlay != null)
                    {
                        Controls.Remove(_detailOverlay);
                        _detailOverlay.Dispose();
                        _detailOverlay = null;
                    }
                }
            };

            _detailOverlay = overlay;
            _detailDialog = dialog;
            dialog.Show();
            dialog.Relayout();
            center();
            dialog.BringToFront();
            UpdateChrome();
        }

        private void SwitchPage(int page)
        {
            _page = page;
            _dashboard.Visible = page == 0;
            _library.Visible = page == 1;
            _workshop.Visible = page == 2;
            if (page == 3 && _settings == null)
            {
                _settings = new SettingsPage(_controller);
                _settings.Dock = DockStyle.Fill;
                _settings.Visible = false;
                _host.Controls.Add(_settings);
            }
            if (_settings != null) _settings.Visible = page == 3;
            if (page == 3) _settings.RefreshState();
            if (page == 1) _library.PerformLayout();
            if (page == 1) _library.RefreshView();
            if (page == 0)
            {
                _dashboard.PerformLayout();
                _dashboard.Invalidate(true);
            }
            _host.Invalidate(true);
            if (IsHandleCreated && Visible) WindowFx.Repaint(this);
        }

        private void ShowFromTray()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            QueueRepaint();
        }

        private void ExitApplication()
        {
            _reallyExit = true;
            _tray.Visible = false;
            _controller.SaveSettings();
            Close();
            Application.Exit();
        }

        private void OnApplied(WallpaperAppliedEventArgs e)
        {
            if (_controller.Settings.ShowNotifications && (!Visible || e.Automatic))
            {
                _tray.ShowBalloonTip(3000, e.Automatic ? "The wallpaper has changed with the weather" : "Wallpaper switched",
                    "《" + e.Wallpaper.Title + "》\n" + WeatherKindInfo.Name(_controller.Weather == null
                        ? e.Wallpaper.MatchWeather : _controller.Weather.Current.Kind), ToolTipIcon.None);
            }
            UpdateChrome();
            if (_dashboard != null && _dashboard.Visible) _dashboard.RefreshAppliedState();
        }

        private string BuildStatusRight()
        {
            string right = (_controller.Settings.AutoFollow ? "Auto follow ON" : "Automatically follow PAUSED")
                + "  ·  Hourly weather"
                + "  ·  " + DateTime.Now.ToString("HH:mm:ss");
            if (_controller.Weather != null)
                right += "  ·  " + Theme.Clock(_controller.Weather.FetchedAt) + "renew";
            return right;
        }

        private void UpdateChrome()
        {
            if (_sidebar == null) return;
            _sidebar.AutoFollow = _controller.Settings.AutoFollow;
            _sidebar.SetState(_controller.IsEngineAvailable, _controller.IsEngineRunning,
                _controller.GetVisibleWallpapers().Count, _controller.Settings.RefreshMinutes);
            _sidebar.SetAudio(_controller.WallpaperVolume, _controller.WallpaperMuted);
            _status.IsBusy = _controller.IsBusy;
            _status.RightText = BuildStatusRight();
            _trayAuto.Checked = _controller.Settings.AutoFollow;
            if (_controller.Weather != null)
            {
                string text = "SkyMatch · " + Theme.Temperature(_controller.Weather.Current.Temperature)
                    + " " + _controller.Weather.Current.Description + " · " + _controller.Weather.Location.ShortDisplay;
                _tray.Text = text.Length > 63 ? text.Substring(0, 60) + "…" : text;
            }
            if (_settings != null && _settings.Visible) _settings.RefreshState();
            Invalidate();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing && _controller.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
                if (!_hintShown && _controller.Settings.ShowNotifications)
                {
                    _hintShown = true;
                    _tray.ShowBalloonTip(2500, "SkyMatch is still running",
                        "Minimized to tray. Right-click the tray icon to exit.", ToolTipIcon.None);
                }
                return;
            }
            if (e.Cancel) return;
            _timer.Stop();
            _tray.Visible = false;
            _controller.SaveSettings();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.ShowWindowMessage)
            {
                ShowFromTray();
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT && WindowState == FormWindowState.Normal)
                {
                    Point p = PointToClient(new Point((short)((long)m.LParam & 0xFFFF),
                        (short)(((long)m.LParam >> 16) & 0xFFFF)));
                    const int edge = 6;
                    bool left = p.X < edge;
                    bool right = p.X >= ClientSize.Width - edge;
                    bool top = p.Y < edge;
                    bool bottom = p.Y >= ClientSize.Height - edge;
                    if (left && top) m.Result = (IntPtr)HTTOPLEFT;
                    else if (right && top) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (left) m.Result = (IntPtr)HTLEFT;
                    else if (right) m.Result = (IntPtr)HTRIGHT;
                    else if (top) m.Result = (IntPtr)HTTOP;
                    else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }
            if (m.Msg == WM_GETMINMAXINFO)
            {
                base.WndProc(ref m);
                try
                {
                    Screen screen = Screen.FromHandle(Handle);
                    MINMAXINFO info = (MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                    info.ptMaxPosition.X = screen.WorkingArea.Left;
                    info.ptMaxPosition.Y = screen.WorkingArea.Top;
                    info.ptMaxSize.X = screen.WorkingArea.Width;
                    info.ptMaxSize.Y = screen.WorkingArea.Height;
                    info.ptMinTrackSize.X = MinimumSize.Width;
                    info.ptMinTrackSize.Y = MinimumSize.Height;
                    Marshal.StructureToPtr(info, m.LParam, false);
                }
                catch
                {
                }
                return;
            }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                Task ignored = _controller.RefreshWeatherAsync(true);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null) _timer.Dispose();
                if (_repaintTimer != null) _repaintTimer.Dispose();
                if (_subscriptionTimer != null) _subscriptionTimer.Dispose();
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                }
                if (_trayMenu != null) _trayMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class DarkMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Color.FromArgb(35, 44, 58); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(35, 44, 58); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(35, 44, 58); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(55, 66, 82); } }
        public override Color MenuBorder { get { return Color.FromArgb(45, 55, 70); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(20, 26, 35); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(20, 26, 35); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(20, 26, 35); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(20, 26, 35); } }
    }
}
