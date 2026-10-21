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

            AddTitle(_locationCard, "位置与天气", "IP 定位会反查到区县；也可搜索城市修正（区县来自坐标反查）。", 20, 16);
            _locationName = AddLabel(_locationCard, "尚未定位", 20, 66, Theme.FontNumber, Theme.Text);
            _locationSub = AddLabel(_locationCard, "", 20, 96, Theme.FontTiny, Theme.Muted);
            _autoLocate = MakeButton(_locationCard, "自动 IP 定位", 20, 128, 126);
            _autoLocate.Icon = ActionButton.IconKind.Refresh;
            _autoLocate.Click += async delegate
            {
                _autoLocate.Enabled = false;
                try { await _controller.LocateByIpAsync(); }
                finally { _autoLocate.Enabled = true; }
            };
            _searchCity = MakeButton(_locationCard, "搜索城市", 156, 128, 108);
            _searchCity.Icon = ActionButton.IconKind.Plus;
            _searchCity.Click += async delegate
            {
                using (CitySearchDialog dialog = new CitySearchDialog(_controller))
                {
                    if (dialog.ShowDialog(this.FindForm()) == DialogResult.OK && dialog.SelectedLocation != null)
                        await _controller.SetLocationAsync(dialog.SelectedLocation);
                }
            };

            _lockLabel = AddLabel(_locationCard, "锁定定位", 0, 0, Theme.FontTiny, Theme.Dim);
            _lockLabel.AutoSize = true;
            _lockToggle = new ToggleSwitch();
            _lockToggle.AccentColor = Theme.Green;
            _lockToggle.CheckedChanged += delegate
            {
                _controller.SetLocationLocked(_lockToggle.Checked);
            };
            _locationCard.Controls.Add(_lockToggle);

            AddTitle(_autoCard, "自动跟随", "天气类别或清晨 / 白天 / 黄昏 / 夜晚变化时，自动换壁纸。", 20, 16);
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
            AddLabel(_autoCard, "结合当前时段选择", 20, 76, Theme.FontSmall, Theme.Text);
            AddLabel(_autoCard, "清晨 / 白天 / 黄昏 / 夜晚", 20, 94, Theme.FontTiny, Theme.Dim);

            AddLabel(_autoCard, "天气刷新", 20, 122, Theme.FontSmall, Theme.Text);
            _intervalChips = new ChipStrip();
            _intervalChips.ShowCounts = false;
            _intervalChips.Items.Add(new ChipStrip.Chip("每小时", 60));
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
            AddLabel(_autoCard, "登录 Windows 后自动启动", 20, 184, Theme.FontSmall, Theme.Muted);

            _trayToggle = new ToggleSwitch();
            _trayToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.Settings.MinimizeToTray = _trayToggle.Checked;
                _controller.SaveSettings();
            };
            _autoCard.Controls.Add(_trayToggle);
            AddLabel(_autoCard, "关闭窗口时保留在系统托盘", 20, 219, Theme.FontSmall, Theme.Muted);

            _notifyToggle = new ToggleSwitch();
            _notifyToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.Settings.ShowNotifications = _notifyToggle.Checked;
                _controller.SaveSettings();
            };
            _autoCard.Controls.Add(_notifyToggle);
            AddLabel(_autoCard, "自动切换时显示通知", 20, 254, Theme.FontSmall, Theme.Muted);

            _refreshOnStartupToggle = new ToggleSwitch();
            _refreshOnStartupToggle.AccentColor = Theme.Green;
            _refreshOnStartupToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.Settings.WorkshopRefreshOnStartup = _refreshOnStartupToggle.Checked;
                _controller.SaveSettings();
            };
            _autoCard.Controls.Add(_refreshOnStartupToggle);
            AddLabel(_autoCard, "每次打开软件自动抓取", 20, 292, Theme.FontSmall, Theme.Text);

            _workshopAutoToggle = new ToggleSwitch();
            _workshopAutoToggle.AccentColor = Theme.Green;
            _workshopAutoToggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                _controller.SetWorkshopAutoRefresh(_workshopAutoToggle.Checked);
            };
            _autoCard.Controls.Add(_workshopAutoToggle);
            AddLabel(_autoCard, "工坊倒计时自动抓取", 20, 326, Theme.FontSmall, Theme.Text);

            AddLabel(_autoCard, "自定义抓取间隔（天）", 20, 358, Theme.FontTiny, Theme.Muted);
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

            _saveWorkshopDays = MakeButton(_autoCard, "保存", 86, 376, 54);
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

            AddLabel(_dataCard, "识图算法", 20, 382, Theme.FontSmall, Theme.Text);
            AddLabel(_dataCard, "CLIP ViT-L/14 · 全量高精度", 20, 404, Theme.FontBold, Theme.Text);
            _visionStatus = AddLabel(_dataCard, "", 20, 436, Theme.FontTiny, Theme.Dim);
            _visionStatus.AutoSize = false;
            _visionStatus.Width = 400;

            _recalcVision = MakeButton(_dataCard, "重新计算匹配度", 250, 398, 170);
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
            AddAlgorithmLabel("除 CLIP 外的过滤算法", 18, Theme.FontBold, Theme.Text, 20, 14, 460, 22);
            AddAlgorithmLabel("• 单色块预览：单色覆盖 ≥50% 像素", 17, Theme.FontTiny, Theme.Muted, 20, 42, 460, 20);
            AddAlgorithmLabel("• 人物检测：YOLO，风景优先下过滤所有人物", 17, Theme.FontTiny, Theme.Muted, 20, 64, 460, 20);
            AddAlgorithmLabel("• Steam 标签：游戏/动漫/3D/卡通/女性/男性/中世纪/网红/MMD/音乐/科幻/运动/科技/电视/汽车/未指定", 30, Theme.FontTiny, Theme.Muted, 20, 86, 460, 36);
            AddAlgorithmLabel("• 年龄分级与不适配预览：头像/竖屏/Logo/文字/测试模板", 17, Theme.FontTiny, Theme.Muted, 20, 124, 460, 20);
            AddAlgorithmLabel("• 天气保底：每种天气至少补足 10 张", 17, Theme.FontTiny, Theme.Muted, 20, 146, 460, 20);
            AddAlgorithmLabel("• 手动校准/屏蔽优先于自动结果", 17, Theme.FontTiny, Theme.Muted, 20, 168, 460, 20);
            Controls.Add(_algoPanel);

            AddTitle(_engineCard, "Wallpaper Engine", "通过官方命令行控制当前桌面壁纸。", 20, 16);
            _engineStatus = AddLabel(_engineCard, "检查中…", 20, 66, Theme.FontBold, Theme.Muted);
            _enginePath = AddLabel(_engineCard, "", 20, 94, Theme.FontTiny, Theme.Dim);
            _enginePath.AutoEllipsis = true;
            _enginePath.AutoSize = false;
            _enginePath.Width = 300;
            _rescan = MakeButton(_engineCard, "重新扫描", 20, 128, 110);
            _rescan.Icon = ActionButton.IconKind.Refresh;
            _rescan.Click += async delegate
            {
                _rescan.Enabled = false;
                try { await _controller.ScanWallpapersAsync(); }
                finally { _rescan.Enabled = true; }
            };
            _restore = MakeButton(_engineCard, "恢复最初壁纸", 142, 128, 136);
            _restore.Icon = ActionButton.IconKind.Restore;
            _restore.Click += async delegate
            {
                if (MessageBox.Show(this, "恢复 SkyMatch 第一次启动时保存的壁纸？", "恢复最初壁纸",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _restore.Enabled = false;
                try { await _controller.RestoreOriginalAsync(); }
                finally { _restore.Enabled = true; }
            };

            AddTitle(_dataCard, "数据与维护", "所有配置保存在本机，不会上传壁纸或桌面内容。", 20, 16);
            _manualLabel = AddLabel(_dataCard, "", 20, 66, Theme.FontSmall, Theme.Muted);
            _openFolder = MakeButton(_dataCard, "配置目录", 20, 100, 112);
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
                    MessageBox.Show(this, "打开目录失败：" + ex.Message, "SkyMatch");
                }
            };
            _resetManual = MakeButton(_dataCard, "重置手动校准", 142, 100, 130);
            _resetManual.Click += delegate
            {
                if (MessageBox.Show(this, "清除全部手动天气校准，恢复自动识别？", "重置校准",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _controller.ResetManualMappings();
            };
            _resetHidden = MakeButton(_dataCard, "恢复被排除的壁纸", 20, 142, 154);
            _resetHidden.Click += delegate
            {
                if (_controller.HiddenCount == 0)
                {
                    MessageBox.Show(this, "目前没有排除任何壁纸。", "SkyMatch");
                    return;
                }
                _controller.ResetHiddenWallpapers();
            };
            _aboutButton = MakeButton(_dataCard, "关于 SkyMatch", 184, 142, 138);
            _aboutButton.Click += delegate
            {
                MessageBox.Show(this,
                    "SkyMatch 1.2.0\n\n天气：Open-Meteo\n定位：IP 地理定位 / Open-Meteo 城市搜索\n"
                    + "壁纸：Wallpaper Engine 创意工坊\n\n自动识别依据：预览画面色彩、亮度、对比度与标题/标签/描述。",
                    "关于 SkyMatch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _about = AddLabel(_dataCard, "SkyMatch 1.2.0  ·  为 Windows 桌面而做", 20, 194, Theme.FontTiny, Theme.Dim);
            _blockedButton = MakeButton(_dataCard, "屏蔽壁纸", 20, 330, 120);
            _blockedButton.AccentColor = Theme.Red;
            _blockedButton.Click += delegate
            {
                using (BlockedWallpapersDialog dialog = new BlockedWallpapersDialog(_controller))
                {
                    dialog.ShowDialog(this);
                }
            };

            AddLabel(_dataCard, "内容分级", 20, 236, Theme.FontSmall, Theme.Text);
            AddLabel(_dataCard, "可多选，工坊和壁纸库只显示勾选的分级", 20, 256, Theme.FontTiny, Theme.Dim);
            AddLabel(_dataCard, "全年龄", 20, 286, Theme.FontTiny, Theme.Muted);
            _ageAllToggle = new ToggleSwitch();
            _ageAllToggle.AccentColor = Theme.Green;
            _ageAllToggle.CheckedChanged += AgeToggleChanged;
            _dataCard.Controls.Add(_ageAllToggle);
            AddLabel(_dataCard, "青少年", 150, 286, Theme.FontTiny, Theme.Muted);
            _ageTeenToggle = new ToggleSwitch();
            _ageTeenToggle.AccentColor = Theme.Yellow;
            _ageTeenToggle.CheckedChanged += AgeToggleChanged;
            _dataCard.Controls.Add(_ageTeenToggle);
            AddLabel(_dataCard, "成人", 280, 286, Theme.FontTiny, Theme.Muted);
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
                MessageBox.Show(this, "请输入 1 到 365 之间的天数。", "SkyMatch",
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
                        + "   " + loc.CoordinateText + "   " + (loc.IsApproximate ? "IP 定位" : "手动选择");
                }
                else
                {
                    _locationName.Text = "尚未定位";
                    _locationSub.Text = "允许联网后会自动获取大致位置。";
                }

                if (s.LocationLocked) _locationSub.Text += "   ·   定位已锁定";
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
                    ? "模型可用 · 全部壁纸按最高精度重算"
                    : "模型未安装 · 暂时回退到内置识别";
                _recalcVision.Enabled = !_controller.IsVisionBusy;
                if (!s.WorkshopTopEnabled)
                    _nextFetchLabel.Text = "工坊订阅榜尚未开启";
                else if (!s.WorkshopAutoRefresh)
                    _nextFetchLabel.Text = "自动抓取已关闭，可手动点「重新抓取」";
                else if (_controller.WorkshopItems.Count == 0)
                    _nextFetchLabel.Text = "距离下一次抓取壁纸：等待首次抓取";
                else
                {
                    int days = _controller.WorkshopDaysUntilRefresh;
                    _nextFetchLabel.Text = days <= 0
                        ? "距离下一次抓取壁纸：今天"
                        : "距离下一次抓取壁纸还有 " + days + " 天";
                }
                _intervalChips.SetSelectedSilent(0);

                if (_controller.IsEngineAvailable)
                {
                    _engineStatus.Text = "已连接  ·  " + (_controller.IsEngineRunning ? "正在运行" : "进程未运行");
                    _engineStatus.ForeColor = _controller.IsEngineRunning ? Theme.Green : Theme.Yellow;
                }
                else
                {
                    _engineStatus.Text = "未找到 Wallpaper Engine";
                    _engineStatus.ForeColor = Theme.Red;
                }
                string path = _controller.EnginePath;
                if (String.IsNullOrEmpty(path)) path = "未能自动定位，请确认 Steam 安装目录。";
                _enginePath.Text = path;
                _enginePath.Width = Math.Max(120, _engineCard.Width - 40);
                _manualLabel.Text = "手动校准 " + _controller.ManualCount + " 张  ·  已排除 "
                    + _controller.HiddenCount + " 张  ·  自动排除 " + _controller.AutoExcludedCount
                    + " 张  ·  共 " + _controller.GetVisibleWallpapers().Count + " 张参与匹配";
                _blockedButton.Text = "屏蔽壁纸 (" + _controller.BlockedCount + ")";
                _about.Text = "SkyMatch 1.2.0  ·  Open-Meteo  ·  数据仅保存在本机";
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
            Theme.DrawText(e.Graphics, "设置", Theme.FontTitle, Theme.Text, 0, 0);
            Theme.DrawText(e.Graphics, "让天气自动接管桌面，也可以随时手动校准。",
                Theme.FontTiny, Theme.Dim, 0, 34);
            base.OnPaint(e);
        }
    }
}
