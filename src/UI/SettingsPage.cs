using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace SkyMatch
{
    public class SettingsPage : UserControl
    {
        private readonly AppController _controller;
        private readonly CardPanel _locationCard;
        private readonly CardPanel _autoCard;
        private readonly CardPanel _engineCard;
        private readonly CardPanel _dataCard;

        private readonly Label _locationName;
        private readonly Label _locationSub;
        private readonly Label _engineStatus;
        private readonly Label _enginePath;
        private readonly Label _manualLabel;
        private readonly Label _about;

        private readonly ToggleSwitch _autoToggle;
        private readonly ToggleSwitch _timeToggle;
        private readonly ToggleSwitch _startupToggle;
        private readonly ToggleSwitch _trayToggle;
        private readonly ToggleSwitch _notifyToggle;
        private readonly ToggleSwitch _refreshOnStartupToggle;
        private readonly ToggleSwitch _workshopAutoToggle;
        private readonly TextBox _workshopDays;
        private readonly ActionButton _saveWorkshopDays;        private readonly Label _nextFetchLabel;
        private readonly Label _visionStatus;
        private readonly ActionButton _recalcVision;
        private readonly ActionButton _algoExpand;
        private readonly CardPanel _algoPanel;
        private readonly ChipStrip _intervalChips;

        private readonly ActionButton _autoLocate;
        private readonly ActionButton _searchCity;
        private readonly ToggleSwitch _lockToggle;
        private readonly Label _lockLabel;
        private readonly ActionButton _rescan;
        private readonly ActionButton _restore;
        private readonly ActionButton _openFolder;
        private readonly ActionButton _resetManual;
        private readonly ActionButton _resetHidden;
        private readonly ActionButton _blockedButton;
        private readonly ActionButton _aboutButton;
        private readonly ToggleSwitch _ageAllToggle;
        private readonly ToggleSwitch _ageTeenToggle;
        private readonly ToggleSwitch _ageAdultToggle;

        private bool _syncing;

        public SettingsPage(AppController controller)
        {
            _controller = controller;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;

            _locationCard = MakeCard();
            _autoCard = MakeCard();
            _engineCard = MakeCard();
            _dataCard = MakeCard();

            AddTitle(_locationCard, "Location & Weather", "IP-based location; search a city to correct it.", 20, 16);
            _locationName = AddLabel(_locationCard, "Not yet located", 20, 66, Theme.FontNumber, Theme.Text);
            _locationSub = AddLabel(_locationCard, "", 20, 96, Theme.FontTiny, Theme.Muted);
            _autoLocate = MakeButton(_locationCard, "Auto locate", 20, 128, 126);
            _autoLocate.Icon = ActionButton.IconKind.Refresh;
            _autoLocate.Click += async delegate
            {
                _autoLocate.Enabled = false;
                try { await _controller.LocateByIpAsync(); }
                finally { _autoLocate.Enabled = true; }
            };
            _searchCity = MakeButton(_locationCard, "Search city", 156, 128, 108);
            _searchCity.Icon = ActionButton.IconKind.Plus;
            _searchCity.Click += async delegate
            {
                using (CitySearchDialog dialog = new CitySearchDialog(_controller))
                {
                    if (dialog.ShowDialog(this.FindForm()) == DialogResult.OK && dialog.SelectedLocation != null)
                        await _controller.SetLocationAsync(dialog.SelectedLocation);
                }
            };

            _lockLabel = AddLabel(_locationCard, "Lock positioning", 0, 0, Theme.FontTiny, Theme.Dim);
            _lockLabel.AutoSize = true;
            _lockToggle = new ToggleSwitch();
            _lockToggle.AccentColor = Theme.Green;
            _lockToggle.CheckedChanged += delegate
            {
                _controller.SetLocationLocked(_lockToggle.Checked);
            };
            _locationCard.Controls.Add(_lockToggle);

            AddTitle(_autoCard, "Auto-follow", "Switch when the weather or time of day changes.", 20, 16);
            _autoToggle = new ToggleSwitch();
            _autoToggle.AccentColor = Theme.Green;
            _autoToggle.CheckedChanged += async delegate
            {
                if (_syncing) return;
                _controller.SetAutoFollow(_autoToggle.Checked);
                if (_autoToggle.Checked && !_controller.Settings.HasAppliedOnce && _controller.Weather != null)
                    await _controller.MatchCurrentWeatherAsync();
            };
            _autoCard.Controls.Add(_autoToggle);

            _timeToggle = new ToggleSwitch();
            _timeToggle.AccentColor = Theme.TimeColor(TimeSlot.Dusk);
            _timeToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.SetUseTimeOfDay(_timeToggle.Checked);
            };
            _autoCard.Controls.Add(_timeToggle);
            AddLabel(_autoCard, "Combined with current period selection", 20, 76, Theme.FontSmall, Theme.Text);
            AddLabel(_autoCard, "Early morning / day / dusk / night", 20, 94, Theme.FontTiny, Theme.Dim);

            AddLabel(_autoCard, "Weather refresh", 20, 122, Theme.FontSmall, Theme.Text);
            _intervalChips = new ChipStrip();
            _intervalChips.ShowCounts = false;
            _intervalChips.Items.Add(new ChipStrip.Chip("Hourly", 60));
            _intervalChips.SelectedIndexChanged += delegate
            {
                if (_syncing) return;
                ChipStrip.Chip chip = _intervalChips.SelectedChip;
                if (chip != null && chip.Tag is int) _controller.SetRefreshMinutes((int)chip.Tag);
            };
            _autoCard.Controls.Add(_intervalChips);

            _startupToggle = new ToggleSwitch();
            _startupToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.SetStartWithWindows(_startupToggle.Checked);
            };
            _autoCard.Controls.Add(_startupToggle);
            AddLabel(_autoCard, "Start with Windows", 20, 184, Theme.FontSmall, Theme.Muted);

            _trayToggle = new ToggleSwitch();
            _trayToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.Settings.MinimizeToTray = _trayToggle.Checked;
                _controller.SaveSettings();
            };
            _autoCard.Controls.Add(_trayToggle);
            AddLabel(_autoCard, "Minimize to tray", 20, 219, Theme.FontSmall, Theme.Muted);

            _notifyToggle = new ToggleSwitch();
            _notifyToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.Settings.ShowNotifications = _notifyToggle.Checked;
                _controller.SaveSettings();
            };
            _autoCard.Controls.Add(_notifyToggle);
            AddLabel(_autoCard, "Show switch notifications", 20, 254, Theme.FontSmall, Theme.Muted);

            _refreshOnStartupToggle = new ToggleSwitch();
            _refreshOnStartupToggle.AccentColor = Theme.Green;
            _refreshOnStartupToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.Settings.WorkshopRefreshOnStartup = _refreshOnStartupToggle.Checked;
                _controller.SaveSettings();
            };
            _autoCard.Controls.Add(_refreshOnStartupToggle);
            AddLabel(_autoCard, "Refresh Workshop on launch", 20, 292, Theme.FontSmall, Theme.Text);

            _workshopAutoToggle = new ToggleSwitch();
            _workshopAutoToggle.AccentColor = Theme.Green;
            _workshopAutoToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.SetWorkshopAutoRefresh(_workshopAutoToggle.Checked);
            };
            _autoCard.Controls.Add(_workshopAutoToggle);
            AddLabel(_autoCard, "Scheduled Workshop refresh", 20, 326, Theme.FontSmall, Theme.Text);

            AddLabel(_autoCard, "Refresh interval (days)", 20, 358, Theme.FontTiny, Theme.Muted);
            _workshopDays = new TextBox();
            _workshopDays.Text = "7";
            _workshopDays.MaxLength = 3;
            _workshopDays.Size = new Size(54, 27);
            _workshopDays.Location = new Point(20, 376);
            _workshopDays.AutoSize = false;
            _workshopDays.BackColor = Theme.CardSoft;
            _workshopDays.ForeColor = Theme.Text;
            _workshopDays.BorderStyle = BorderStyle.None;
            _workshopDays.Font = Theme.FontBold;
            _workshopDays.TextAlign = HorizontalAlignment.Center;
            _workshopDays.KeyPress += delegate(object sender, KeyPressEventArgs e)
            {
                if (!Char.IsControl(e.KeyChar) && !Char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            _autoCard.Controls.Add(_workshopDays);

            _saveWorkshopDays = MakeButton(_autoCard, "Save", 86, 376, 54);
            _saveWorkshopDays.Size = new Size(54, 27);
            _saveWorkshopDays.AccentColor = Theme.Yellow;
            _saveWorkshopDays.Click += delegate { SaveWorkshopDays(); };
            _workshopDays.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { SaveWorkshopDays(); e.SuppressKeyPress = true; }
            };
            _nextFetchLabel = AddLabel(_autoCard, "", 20, 416, Theme.FontTiny, Theme.Dim);
            _nextFetchLabel.AutoSize = false;
            _nextFetchLabel.Width = 260;

            AddLabel(_dataCard, "Vision", 20, 382, Theme.FontSmall, Theme.Text);
            AddLabel(_dataCard, "CLIP ViT-L/14 · High precision", 20, 404, Theme.FontBold, Theme.Text);
            _visionStatus = AddLabel(_dataCard, "", 20, 436, Theme.FontTiny, Theme.Dim);
            _visionStatus.AutoSize = false;
            _visionStatus.Width = 400;

            _recalcVision = MakeButton(_dataCard, "Recalculate", 250, 398, 170);
            _recalcVision.Pill = true;
            _recalcVision.AccentColor = Theme.Yellow;
            _recalcVision.Click += async delegate
            {
                _recalcVision.Enabled = false;
                try { await _controller.ReclassifyVisionAsync(); }
                finally { _recalcVision.Enabled = true; }
            };

            _algoExpand = new ActionButton();
            _algoExpand.Text = "▸";
            _algoExpand.Pill = true;
            _algoExpand.Size = new Size(22, 20);
            _algoExpand.AccentColor = Theme.Muted;
            _algoExpand.Click += delegate
            {
                _algoPanel.Visible = !_algoPanel.Visible;
                _algoExpand.Text = _algoPanel.Visible ? "▾" : "▸";
                if (_algoPanel.Visible)
                {
                    PositionAlgorithmPanel();
                    _algoPanel.BringToFront();
                }
            };
            _dataCard.Controls.Add(_algoExpand);

            _algoPanel = new CardPanel();
            _algoPanel.Radius = 14;
            _algoPanel.FillColor = Theme.Card;
            _algoPanel.Size = new Size(500, 214);
            _algoPanel.Visible = false;
            AddAlgorithmLabel("Filtering algorithms other than CLIP", 18, Theme.FontBold, Theme.Text, 20, 14, 460, 22);
            AddAlgorithmLabel("• Monochrome block preview: Monochrome coverage ≥50% pixels", 17, Theme.FontTiny, Theme.Muted, 20, 42, 460, 20);
            AddAlgorithmLabel("• People detection: YOLO, filter all people under landscape priority", 17, Theme.FontTiny, Theme.Muted, 20, 64, 460, 20);
            AddAlgorithmLabel("• Steam Tags: Game/Anime/3D/Cartoon/Female/Male/Medieval/Internet Celebrity/MMD/Music/Science Fiction/Sports/Technology/TV/Car/Unspecified", 30, Theme.FontTiny, Theme.Muted, 20, 86, 460, 36);
            AddAlgorithmLabel("• Age rating and unsuitable preview: avatar/vertical screen/Logo/text/test template", 17, Theme.FontTiny, Theme.Muted, 20, 124, 460, 20);
            AddAlgorithmLabel("• Weather guarantee: at least 10 tickets for each weather condition", 17, Theme.FontTiny, Theme.Muted, 20, 146, 460, 20);
            AddAlgorithmLabel("• Manual calibration/masking takes precedence over automatic results", 17, Theme.FontTiny, Theme.Muted, 20, 168, 460, 20);
            Controls.Add(_algoPanel);

            AddTitle(_engineCard, "Wallpaper Engine", "Control the current desktop wallpaper through the official command line.", 20, 16);
            _engineStatus = AddLabel(_engineCard, "Checking…", 20, 66, Theme.FontBold, Theme.Muted);
            _enginePath = AddLabel(_engineCard, "", 20, 94, Theme.FontTiny, Theme.Dim);
            _enginePath.AutoEllipsis = true;
            _enginePath.AutoSize = false;
            _enginePath.Width = 300;
            _rescan = MakeButton(_engineCard, "Rescan", 20, 128, 110);
            _rescan.Icon = ActionButton.IconKind.Refresh;
            _rescan.Click += async delegate
            {
                _rescan.Enabled = false;
                try { await _controller.ScanWallpapersAsync(); }
                finally { _rescan.Enabled = true; }
            };
            _restore = MakeButton(_engineCard, "Restore original", 142, 128, 136);
            _restore.Icon = ActionButton.IconKind.Restore;
            _restore.Click += async delegate
            {
                if (MessageBox.Show(this, "Restore wallpapers saved when SkyMatch was first launched?", "Restore original wallpaper",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _restore.Enabled = false;
                try { await _controller.RestoreOriginalAsync(); }
                finally { _restore.Enabled = true; }
            };

            AddTitle(_dataCard, "Data & Maintenance", "Saved locally. No wallpapers or desktop content are uploaded.", 20, 16);
            _manualLabel = AddLabel(_dataCard, "", 20, 66, Theme.FontSmall, Theme.Muted);
            _openFolder = MakeButton(_dataCard, "Data folder", 20, 100, 112);
            _openFolder.Icon = ActionButton.IconKind.Folder;
            _openFolder.Click += delegate
            {
                try
                {
                    ProcessStartInfo info = new ProcessStartInfo();
                    info.FileName = "explorer.exe";
                    info.Arguments = "\"" + AppPaths.DataDirectory + "\"";
                    info.UseShellExecute = true;
                    Process.Start(info);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to open directory:" + ex.Message, "SkyMatch");
                }
            };
            _resetManual = MakeButton(_dataCard, "Reset", 142, 100, 130);
            _resetManual.Click += delegate
            {
                if (MessageBox.Show(this, "Clear all manual weather calibrations and restore automatic recognition?", "reset calibration",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _controller.ResetManualMappings();
            };
            _resetHidden = MakeButton(_dataCard, "Restore excluded", 20, 142, 154);
            _resetHidden.Click += delegate
            {
                if (_controller.HiddenCount == 0)
                {
                    MessageBox.Show(this, "No wallpapers have been excluded at this time.", "SkyMatch");
                    return;
                }
                _controller.ResetHiddenWallpapers();
            };
            _aboutButton = MakeButton(_dataCard, "About SkyMatch", 184, 142, 138);
            _aboutButton.Click += delegate
            {
                MessageBox.Show(this,
                    "SkyMatch 1.2.0\n\nWeather: Open-Meteo\nLocation: IP geolocation / Open-Meteo city search\n"
                    + "Wallpaper: Wallpaper Engine Workshop\n\nClassification uses preview color, brightness, contrast, and title/tags/description.",
                    "About SkyMatch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _about = AddLabel(_dataCard, "SkyMatch 1.2.0 · Made for Windows Desktop", 20, 194, Theme.FontTiny, Theme.Dim);
            _blockedButton = MakeButton(_dataCard, "Blocked", 20, 330, 120);
            _blockedButton.AccentColor = Theme.Red;
            _blockedButton.Click += delegate
            {
                using (BlockedWallpapersDialog dialog = new BlockedWallpapersDialog(_controller))
                {
                    dialog.ShowDialog(this);
                }
            };

            AddLabel(_dataCard, "Content rating", 20, 236, Theme.FontSmall, Theme.Text);
            AddLabel(_dataCard, "Only selected ratings appear in Workshop and Library.", 20, 256, Theme.FontTiny, Theme.Dim);
            AddLabel(_dataCard, "Everyone", 20, 286, Theme.FontTiny, Theme.Muted);
            _ageAllToggle = new ToggleSwitch();
            _ageAllToggle.AccentColor = Theme.Green;
            _ageAllToggle.CheckedChanged += AgeToggleChanged;
            _dataCard.Controls.Add(_ageAllToggle);
            AddLabel(_dataCard, "Teen", 150, 286, Theme.FontTiny, Theme.Muted);
            _ageTeenToggle = new ToggleSwitch();
            _ageTeenToggle.AccentColor = Theme.Yellow;
            _ageTeenToggle.CheckedChanged += AgeToggleChanged;
            _dataCard.Controls.Add(_ageTeenToggle);
            AddLabel(_dataCard, "Mature", 280, 286, Theme.FontTiny, Theme.Muted);
            _ageAdultToggle = new ToggleSwitch();
            _ageAdultToggle.AccentColor = Theme.Red;
            _ageAdultToggle.CheckedChanged += AgeToggleChanged;
            _dataCard.Controls.Add(_ageAdultToggle);

            Controls.Add(_locationCard);
            Controls.Add(_autoCard);
            Controls.Add(_engineCard);
            Controls.Add(_dataCard);

            _controller.StateChanged += delegate { if (!IsDisposed && Visible) RefreshState(); };
            _controller.WeatherUpdated += delegate { if (!IsDisposed && Visible) RefreshState(); };
            _controller.WallpapersUpdated += delegate { if (!IsDisposed && Visible) RefreshState(); };
            RefreshState();
        }

        private CardPanel MakeCard()
        {
            CardPanel panel = new CardPanel();
            panel.Radius = 16;
            panel.FillColor = Theme.Card;
            return panel;
        }

        private Label AddTitle(CardPanel card, string title, string subtitle, int x, int y)
        {
            Label t = AddLabel(card, title, x, y, Theme.FontMedium, Theme.Text);
            t.UseMnemonic = false;
            t.Font = new Font(Theme.FontMedium.FontFamily, 12.5f, FontStyle.Bold);
            AddLabel(card, subtitle, x, y + 26, Theme.FontTiny, Theme.Dim);
            return t;
        }

        private void AddAlgorithmLabel(string text, float fontSize, Font font, Color color,
            int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Size = new Size(width, height);
            label.Location = new Point(x, y);
            label.BackColor = Color.Transparent;
            label.ForeColor = color;
            label.Font = font;
            _algoPanel.Controls.Add(label);
        }

        private void PositionAlgorithmPanel()
        {
            if (_algoPanel == null || _algoExpand == null) return;
            Point button = PointToClient(_algoExpand.PointToScreen(Point.Empty));
            int x = Math.Max(12, Math.Min(button.X, Width - _algoPanel.Width - 12));
            int y = button.Y - _algoPanel.Height - 8;
            if (y < 58) y = button.Y + _algoExpand.Height + 8;
            _algoPanel.Location = new Point(x, y);
        }

        private Label AddLabel(Control parent, string text, int x, int y, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.UseMnemonic = false;
            label.AutoSize = true;
            label.BackColor = Color.Transparent;
            label.ForeColor = color;
            label.Font = font;
            label.Location = new Point(x, y);
            parent.Controls.Add(label);
            return label;
        }

        private ActionButton MakeButton(Control parent, string text, int x, int y, int width)
        {
            ActionButton button = new ActionButton();
            button.Text = text;
            button.Size = new Size(width, 34);
            button.Location = new Point(x, y);
            button.Pill = true;
            parent.Controls.Add(button);
            return button;
        }

        private void SaveWorkshopDays()        {
            int days;
            if (!Int32.TryParse(_workshopDays.Text.Trim(), out days) || days < 1 || days > 365)
            {
                MessageBox.Show(this, "Please enter a number of days between 1 and 365.", "SkyMatch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _workshopDays.Focus();
                _workshopDays.SelectAll();
                return;
            }
            _controller.SetWorkshopRefreshDays(days);
            _workshopDays.Text = days.ToString();
        }

        private void AgeToggleChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            int mask = 0;
            if (_ageAllToggle.Checked) mask |= 1;
            if (_ageTeenToggle.Checked) mask |= 2;
            if (_ageAdultToggle.Checked) mask |= 4;
            if (mask == 0)
            {
                ToggleSwitch toggle = sender as ToggleSwitch;
                if (toggle != null) toggle.SetCheckedSilent(true);
                return;
            }
            _controller.SetAgeRatings(mask);
        }

        public void RefreshState()
        {
            _syncing = true;
            try
            {
                AppSettings s = _controller.Settings;
                if (_controller.Weather != null && _controller.Weather.Location != null)
                {
                    LocationInfo loc = _controller.Weather.Location;
                    _locationName.Text = loc.CityDistrict;
                    _locationSub.Text = loc.Admin1 + (String.IsNullOrEmpty(loc.Country) ? "" : " · " + loc.Country)
                        + "   " + loc.CoordinateText + "   " + (loc.IsApproximate ? "IP positioning" : "Manual selection");
                }
                else
                {
                    _locationName.Text = "Not yet located";
                    _locationSub.Text = "The approximate location will be automatically obtained after allowing the Internet to connect.";
                }

                if (s.LocationLocked) _locationSub.Text += "   ·   Location locked";
                _lockToggle.SetCheckedSilent(s.LocationLocked);
                _autoLocate.Enabled = !s.LocationLocked;
                _autoToggle.SetCheckedSilent(s.AutoFollow);
                _timeToggle.SetCheckedSilent(s.UseTimeOfDay);
                _startupToggle.SetCheckedSilent(s.StartWithWindows);
                _trayToggle.SetCheckedSilent(s.MinimizeToTray);
                _ageAllToggle.SetCheckedSilent((s.AgeRatingMask & 1) != 0);
                _ageTeenToggle.SetCheckedSilent((s.AgeRatingMask & 2) != 0);
                _ageAdultToggle.SetCheckedSilent((s.AgeRatingMask & 4) != 0);
                _notifyToggle.SetCheckedSilent(s.ShowNotifications);
                _refreshOnStartupToggle.SetCheckedSilent(s.WorkshopRefreshOnStartup);
                _workshopAutoToggle.SetCheckedSilent(s.WorkshopAutoRefresh);
                int refreshDays = Math.Max(1, Math.Min(365, s.WorkshopRefreshDays));
                _workshopDays.Text = refreshDays.ToString();
                _visionStatus.Text = _controller.IsClipAvailable
                    ? "Model available · high accuracy"
                    : "Model missing · using basic recognition";
                _recalcVision.Enabled = !_controller.IsVisionBusy;
                if (!s.WorkshopTopEnabled)
                    _nextFetchLabel.Text = "The workshop subscription list has not yet been opened";
                else if (!s.WorkshopAutoRefresh)
                    _nextFetchLabel.Text = "Scheduled refresh off · refresh manually";
                else if (_controller.WorkshopItems.Count == 0)
                    _nextFetchLabel.Text = "Distance to next crawl of wallpaper: Waiting for first crawl";
                else
                {
                    int days = _controller.WorkshopDaysUntilRefresh;
                    _nextFetchLabel.Text = days <= 0
                        ? "Next Workshop refresh: today"
                        : "Next Workshop refresh in " + days + "sky";
                }
                _intervalChips.SetSelectedSilent(0);

                if (_controller.IsEngineAvailable)
                {
                    _engineStatus.Text = "Connected · " + (_controller.IsEngineRunning ? "Running" : "Process not running");
                    _engineStatus.ForeColor = _controller.IsEngineRunning ? Theme.Green : Theme.Yellow;
                }
                else
                {
                    _engineStatus.Text = "Wallpaper Engine not found";
                    _engineStatus.ForeColor = Theme.Red;
                }
                string path = _controller.EnginePath;
                if (String.IsNullOrEmpty(path)) path = "Failed to automatically locate, please confirm the Steam installation directory.";
                _enginePath.Text = path;
                _enginePath.Width = Math.Max(120, _engineCard.Width - 40);
                _manualLabel.Text = "Manual " + _controller.ManualCount + " · excluded "
                    + _controller.HiddenCount + " · auto-excluded " + _controller.AutoExcludedCount
                    + " · " + _controller.GetVisibleWallpapers().Count + " wallpapers matched";
                _blockedButton.Text = "Blocked (" + _controller.BlockedCount + ")";
                _about.Text = "SkyMatch 1.2.0 · Open-Meteo · data stays local";
            }
            finally
            {
                _syncing = false;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        private void DoLayout()
        {
            if (Width < 700 || _locationCard == null || _autoCard == null || _engineCard == null
                || _dataCard == null) return;
            int gap = 14;
            int top = 54;
            int colW = (Width - gap) / 2;
            int h1 = 178;
            int h2 = Math.Max(250, Height - top - h1 - gap);
            _locationCard.SetBounds(0, top, colW, h1);
            _autoCard.SetBounds(0, top + h1 + gap, colW, h2);
            _engineCard.SetBounds(colW + gap, top, Width - colW - gap, h1);
            _dataCard.SetBounds(colW + gap, top + h1 + gap, Width - colW - gap, h2);
            if (_lockLabel != null)
            {
                _lockLabel.Location = new Point(Math.Max(150, _locationCard.Width - 118), 70);
                _lockToggle.Location = new Point(_locationCard.Width - 58, 64);
            }
            if (_algoExpand != null) _algoExpand.Location = new Point(20, 454);
            PositionToggles();
            if (_algoPanel != null && _algoPanel.Visible) PositionAlgorithmPanel();
        }

        private void PositionToggles()
        {
            if (_autoToggle == null) return;
            _autoToggle.Location = new Point(_autoCard.Width - _autoToggle.Width - 22, 20);
            _timeToggle.Location = new Point(_autoCard.Width - _timeToggle.Width - 22, 70);
            _startupToggle.Location = new Point(_autoCard.Width - _startupToggle.Width - 22, 178);
            _trayToggle.Location = new Point(_autoCard.Width - _trayToggle.Width - 22, 213);
            _notifyToggle.Location = new Point(_autoCard.Width - _notifyToggle.Width - 22, 248);
            _refreshOnStartupToggle.Location = new Point(_autoCard.Width - _refreshOnStartupToggle.Width - 22, 286);
            _workshopAutoToggle.Location = new Point(_autoCard.Width - _workshopAutoToggle.Width - 22, 320);
            _intervalChips.SetBounds(20, 140, _autoCard.Width - 40, 32);
            _ageAllToggle.Location = new Point(82, 278);
            _ageTeenToggle.Location = new Point(212, 278);
            _ageAdultToggle.Location = new Point(326, 278);
            _enginePath.Width = Math.Max(120, _engineCard.Width - 40);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible && _algoPanel != null)
            {
                _algoPanel.Visible = false;
                if (_algoExpand != null) _algoExpand.Text = "▸";
            }
            else if (Visible && !IsDisposed)
            {
                RefreshState();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            Theme.DrawText(e.Graphics, "Settings", Theme.FontTitle, Theme.Text, 0, 0);
            Theme.DrawText(e.Graphics, "Let the weather automatically take over your desktop, or calibrate it manually at any time.",
                Theme.FontTiny, Theme.Dim, 0, 34);
            base.OnPaint(e);
        }
    }
}
