using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SkyMatch
{
    public class WorkshopRow : Control
    {
        private readonly AppController _controller;
        private readonly WallpaperItem _item;
        private readonly bool _featured;
        public string FeaturedLabel { get; set; }
        private bool _hover;
        private readonly ActionButton _open;
        private readonly ActionButton _apply;

        public event Action<WallpaperItem> OpenRequested;
        public event Action<WallpaperItem> ApplyRequested;

        public WorkshopRow(AppController controller, WallpaperItem item, bool featured)
        {
            _controller = controller;
            _item = item;
            _featured = featured;
            FeaturedLabel = "最佳匹配";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
            Height = featured ? 156 : 128;
            Cursor = Cursors.Default;

            _open = new ActionButton();
            _open.Text = item.IsInstalled ? "工坊页" : "订阅";
            _open.Pill = true;
            _open.Size = new Size(78, 32);
            _open.Font = Theme.FontSmall;
            _open.AccentColor = item.IsInstalled ? Theme.Muted : Theme.Yellow;
            _open.Primary = !item.IsInstalled;
            _open.TextColor = item.IsInstalled ? Theme.Text : Color.FromArgb(16, 20, 26);
            _open.Click += delegate
            {
                Action<WallpaperItem> handler = OpenRequested;
                if (handler != null) handler(_item);
            };
            Controls.Add(_open);

            _apply = new ActionButton();
            _apply.Text = "应用";
            _apply.Pill = true;
            _apply.Size = new Size(64, 32);
            _apply.Font = Theme.FontSmall;
            _apply.Primary = true;
            _apply.AccentColor = Theme.TimeColor(item.PrimaryTime);
            _apply.Visible = item.IsInstalled;
            _apply.Click += delegate
            {
                Action<WallpaperItem> handler = ApplyRequested;
                if (handler != null) handler(_item);
            };
            Controls.Add(_apply);
        }

        public WallpaperItem Item
        {
            get { return _item; }
        }

        public bool Featured
        {
            get { return _featured; }
        }

        public string ItemId
        {
            get { return _item.Id; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_open == null) return;
            int bottom = Height - 42;
            _open.Location = new Point(Width - _open.Width - 14, bottom);
            _apply.Location = new Point(Width - _open.Width - _apply.Width - 20, bottom);
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
            if (_item == null) return;
            WeatherKind kind = _item.MatchWeather;
            Color accent = Theme.Accent(kind);
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (SolidBrush brush = new SolidBrush(_featured ? Color.FromArgb(24, 31, 42)
                : (_hover ? Theme.CardHover : Theme.Card)))
                Theme.FillRounded(e.Graphics, brush, rect, 14);
            using (Pen pen = new Pen(_featured ? Theme.Alpha(Theme.TimeColor(_item.PrimaryTime), 120)
                : (_hover ? Theme.Border : Theme.BorderSoft), 1f))
                Theme.DrawRounded(e.Graphics, pen, rect, 14);

            int previewW = _featured ? 250 : 170;
            int previewH = Height - 24;
            RectangleF previewRect = new RectangleF(12, 12, previewW, previewH);
            using (GraphicsPath clip = Theme.RoundedRect(previewRect, 10))
            {
                e.Graphics.SetClip(clip);
                Bitmap image = AppImages.GetWallpaper(_item, previewW, previewH);
                if (image != null)
                {
                    e.Graphics.DrawImage(image, previewRect);
                }
                else
                {
                    using (LinearGradientBrush brush = new LinearGradientBrush(previewRect,
                        Theme.Mix(Theme.Card, accent, 0.25f), Color.FromArgb(12, 17, 24), 90f))
                        e.Graphics.FillRectangle(brush, previewRect);
                    WeatherGlyph.Draw(e.Graphics, kind, new RectangleF(
                        previewRect.X + previewW / 2f - 24, previewRect.Y + previewH / 2f - 24, 48, 48), accent, 205);
                }
                using (LinearGradientBrush shade = new LinearGradientBrush(previewRect,
                    Color.FromArgb(20, 8, 12, 18), Color.FromArgb(155, 8, 12, 18), 90f))
                {
                    Blend blend = new Blend();
                    blend.Factors = new[] { 0f, 0.6f, 1f };
                    blend.Positions = new[] { 0f, 0.62f, 1f };
                    shade.Blend = blend;
                    e.Graphics.FillRectangle(shade, previewRect);
                }
                e.Graphics.ResetClip();
            }

            float x = previewRect.Right + 18;
            float rightLimit = Width - (_item.IsInstalled ? 178 : 110);
            float titleY = 13;
            string title = Theme.FitText(e.Graphics, _item.Title, _featured ? Theme.FontMedium : Theme.FontBold,
                rightLimit - x);
            Theme.DrawText(e.Graphics, title, _featured ? Theme.FontMedium : Theme.FontBold, Theme.Text, x, titleY);
            string meta = _item.TypeLabel
                + (_item.WorkshopSubscriptions > 0 ? "  ·  " + _item.WorkshopSubscriptions + " 订阅" : "")
                + (_item.IsInstalled ? "  ·  已安装" : "  ·  待订阅");
            Theme.DrawText(e.Graphics, meta, Theme.FontTiny,
                _item.IsInstalled ? Theme.Green : Theme.Muted, x, titleY + (_featured ? 27 : 24));

            string tags = _item.Tags == null ? "" : String.Join(" · ", _item.Tags.ToArray());
            Theme.DrawText(e.Graphics, Theme.FitText(e.Graphics, tags, Theme.FontTiny, rightLimit - x),
                Theme.FontTiny, Theme.Dim, x, titleY + (_featured ? 48 : 43));

            string reason = _item.Reason;
            Theme.DrawText(e.Graphics, Theme.FitText(e.Graphics, reason, Theme.FontTiny, rightLimit - x),
                Theme.FontTiny, Theme.Alpha(Theme.Text, 185), x, titleY + (_featured ? 69 : 60));

            // Weather and time suitability badges.
            float badgeY = Height - (_featured ? 34 : 32);
            DrawPill(e.Graphics, x, badgeY, WeatherKindInfo.Name(kind), accent);
            DrawPill(e.Graphics, x + 76, badgeY, TimeSlotInfo.Name(_item.PrimaryTime),
                Theme.TimeColor(_item.PrimaryTime));
            string confidence = _item.IsManual ? "手动天气" : "天气 " + Math.Round(_item.Confidence) + "%";
            Theme.DrawText(e.Graphics, confidence, Theme.FontTiny, Theme.Dim, x + 158, badgeY + 3);
        }

        private static void DrawPill(Graphics g, float x, float y, string text, Color color)
        {
            SizeF size = g.MeasureString(text, Theme.FontTiny);
            RectangleF rect = new RectangleF(x, y, size.Width + 18, 22);
            using (SolidBrush brush = new SolidBrush(Theme.Alpha(color, 42)))
                Theme.FillRounded(g, brush, rect, 11);
            Theme.DrawText(g, text, Theme.FontTiny, color, x + 9, y + 3);
        }
    }

    public class WorkshopPage : UserControl
    {
        private readonly AppController _controller;
        private readonly ActionButton _refresh;
        private readonly ActionButton _rescan;
        private readonly Label _status;
        private readonly Label _empty;
        private readonly Panel _list;
        private readonly Panel _grid;
        private readonly ActionButton _viewButton;
        private readonly ActionButton _sceneryButton;
        private readonly WorkshopHoverPanel _hoverPanel;
        private readonly FadeTip _blockTip;
        private readonly List<WorkshopRow> _rows;
        private readonly List<WorkshopGridCard> _cards;
        private readonly ChipStrip _typeChips;
        private readonly ActionButton _prevPage;
        private readonly ActionButton _nextPage;
        private readonly Label _pageLabel;
        private bool _gridView;
        private bool _rebuilding;
        private bool _needsRebuild;
        private bool _detailsExpanded;
        private Rectangle _detailsArrowRect;
        private WeatherKind? _typeFilter;
        private int _page;
        private int _pageCount = 1;
        private int _columns = 4;
        private const int PageSize = 40;

        public WorkshopPage(AppController controller)
        {
            _controller = controller;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
            _rows = new List<WorkshopRow>();
            _cards = new List<WorkshopGridCard>();
            _gridView = controller.Settings.WorkshopGridView;

            _refresh = new ActionButton();
            _refresh.Text = "重新抓取";
            _refresh.Icon = ActionButton.IconKind.Refresh;
            _refresh.Pill = true;
            _refresh.Size = new Size(104, 34);
            _refresh.AccentColor = Theme.Yellow;
            _refresh.Click += async delegate
            {
                _refresh.Enabled = false;
                try { await _controller.RefreshWorkshopAsync(true); }
                finally { _refresh.Enabled = true; }
            };

            _rescan = new ActionButton();
            _rescan.Text = "扫描已安装";
            _rescan.Icon = ActionButton.IconKind.Refresh;
            _rescan.Pill = true;
            _rescan.Size = new Size(112, 34);
            _rescan.AccentColor = Theme.Yellow;
            _rescan.Click += async delegate
            {
                _rescan.Enabled = false;
                try { await _controller.ScanWallpapersAsync(); }
                finally { _rescan.Enabled = true; }
            };

            _status = new Label();            _status.AutoSize = false;
            _status.AutoEllipsis = true;
            _status.BackColor = Color.Transparent;
            _status.ForeColor = Theme.Muted;
            _status.Font = Theme.FontTiny;
            _status.TextAlign = ContentAlignment.MiddleLeft;

            _list = new Panel();
            _list.AutoScroll = true;
            _list.BackColor = Theme.Bg;
            _list.Padding = new Padding(0, 0, 0, 18);
            _list.Resize += delegate { LayoutRows(); };

            _grid = new Panel();
            _grid.AutoScroll = true;
            _grid.BackColor = Theme.Bg;
            _grid.Padding = new Padding(0, 0, 0, 18);
            _grid.Resize += delegate { LayoutCards(); };

            _viewButton = new ActionButton();
            _viewButton.Text = _gridView ? "列表视图" : "网格视图";
            _viewButton.Icon = ActionButton.IconKind.Grid;
            _viewButton.Pill = true;
            _viewButton.Size = new Size(92, 34);
            _viewButton.AccentColor = Theme.Yellow;
            _viewButton.Click += delegate
            {
                _gridView = !_gridView;
                _page = 0;
                _controller.Settings.WorkshopGridView = _gridView;
                _controller.SaveSettings();
                Rebuild();
            };

            _sceneryButton = new ActionButton();
            _sceneryButton.Text = _controller.Settings.PreferScenery ? "风景优先：开" : "风景优先：关";
            _sceneryButton.Icon = ActionButton.IconKind.Grid;
            _sceneryButton.Pill = true;
            _sceneryButton.Size = new Size(104, 34);
            _sceneryButton.AccentColor = Theme.Green;
            _sceneryButton.Click += delegate
            {
                _controller.Settings.PreferScenery = !_controller.Settings.PreferScenery;
                _page = 0;
                _controller.SaveSettings();
                if (_controller.Settings.PreferScenery)
                {
                    // Re-fetch the top 100 with the scenic tag filter applied.
                    Task ignored = _controller.RefreshWorkshopAsync(true);
                }
                else if (_controller.WorkshopTopEnabled)
                {
                    // Older builds removed these tagged entries while
                    // fetching. Pull them back when the filter is turned off.
                    Task ignored = _controller.RefreshWorkshopAsync(true);
                }
                Rebuild();
            };

            _typeChips = new ChipStrip();
            _typeChips.Height = 28;
            _typeChips.ShowCounts = false;
            _typeChips.Items.Add(new ChipStrip.Chip("全部类型", null));
            foreach (WeatherKind kind in WeatherKindInfo.All)
                _typeChips.Items.Add(new ChipStrip.Chip(WeatherKindInfo.Name(kind), kind));
            _typeChips.SetSelectedSilent(0);
            _typeChips.SelectedIndexChanged += delegate
            {
                ChipStrip.Chip chip = _typeChips.SelectedChip;
                _typeFilter = chip != null && chip.Tag is WeatherKind ? (WeatherKind?)(WeatherKind)chip.Tag : null;
                _page = 0;
                Rebuild();
            };

            _prevPage = new ActionButton();
            _prevPage.Text = "上一页";
            _prevPage.Pill = true;
            _prevPage.Size = new Size(62, 28);
            _prevPage.AccentColor = Theme.Muted;
            _prevPage.Click += delegate { ChangePage(-1); };

            _nextPage = new ActionButton();
            _nextPage.Text = "下一页";
            _nextPage.Pill = true;
            _nextPage.Size = new Size(62, 28);
            _nextPage.AccentColor = Theme.Yellow;
            _nextPage.Click += delegate { ChangePage(1); };

            _pageLabel = new Label();
            _pageLabel.AutoSize = false;
            _pageLabel.Size = new Size(58, 28);
            _pageLabel.TextAlign = ContentAlignment.MiddleCenter;
            _pageLabel.ForeColor = Theme.Muted;
            _pageLabel.BackColor = Color.Transparent;
            _pageLabel.Font = Theme.FontTiny;

            _hoverPanel = new WorkshopHoverPanel();
            _hoverPanel.Visible = false;
            _blockTip = new FadeTip();

            _empty = new Label();
            _empty.AutoSize = false;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = Theme.Dim;
            _empty.BackColor = Theme.Bg;
            _empty.Font = Theme.FontSmall;
            _empty.Visible = false;

            Controls.Add(_refresh);
            Controls.Add(_rescan);
            Controls.Add(_viewButton);
            Controls.Add(_sceneryButton);
            Controls.Add(_typeChips);
            Controls.Add(_prevPage);
            Controls.Add(_nextPage);
            Controls.Add(_pageLabel);
            Controls.Add(_status);
            _status.Visible = false;
            Controls.Add(_list);
            Controls.Add(_grid);
            Controls.Add(_empty);
            Controls.Add(_hoverPanel);
            Controls.Add(_blockTip);
            _grid.Visible = false;
            _hoverPanel.BringToFront();
            _blockTip.BringToFront();

            _controller.WallpaperApplied += delegate
            {
                if (IsDisposed || !Visible) return;
                _grid.Invalidate(true);
                _list.Invalidate(true);
            };
            _controller.ActiveWallpaperChanged += delegate
            {
                if (IsDisposed || !Visible) return;
                _grid.Invalidate(true);
                _list.Invalidate(true);
            };
            _controller.WorkshopUpdated += delegate { if (!IsDisposed) OnWorkshopUpdated(); };
            _controller.WeatherUpdated += delegate
            {
                if (IsDisposed || _controller.WorkshopBusy) return;
                if (Visible) Rebuild(); else _needsRebuild = true;
            };
            _controller.WallpapersUpdated += delegate
            {
                if (IsDisposed || _controller.WorkshopBusy) return;
                if (Visible) Rebuild(); else _needsRebuild = true;
            };
            _controller.StateChanged += delegate { if (!IsDisposed) UpdateStatus(); };
            _needsRebuild = true;
        }

        private void OnWorkshopUpdated()
        {
            if (IsDisposed) return;
            if (!Visible)
            {
                _needsRebuild = true;
                return;
            }
            if (_controller.WorkshopBusy) AppendNewWorkshopItems();
            else Rebuild();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && _needsRebuild && _grid != null)
            {
                _needsRebuild = false;
                Rebuild();
            }
        }

        private static readonly string[] QuickCharacterTerms =
        {
            "凌晨", "小钇", "苏雨果", "苏莓果", "顾冷冷", "老八", "清宵", "若不是你", "furry", "barrie",
            "acostaeliann", "berserk", "wallhall",
            "脓疱", "子非鱼", "你可以永远", "림버스", "limbus", "acheron", "林初雪",
            "云羽", "蓝皮书", "shadow fiend", "杨幂", "小丑", "silent conurbano",
            "lucía", "lucia", "shrek", "loud valorant", "魂类画师", "dohrn",
            "deepseek", "nikke", "valorant", "gta", "frieren", "芙莉莲",
            "清宵", "小钇", "星塔旅人", "心月狐", "爱弥斯", "绯雪"
        };

        private bool IsBlockedSceneItem(WallpaperItem item)
        {
            if (item == null || !_controller.Settings.PreferScenery) return false;
            string text = (item.Title + " " + String.Join(" ", item.Tags.ToArray())).ToLowerInvariant();
            foreach (string term in QuickCharacterTerms)
            {
                if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            if (text.IndexOf("furry", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("sports", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return _controller.IsCharacterItem(item);
        }

        private List<WallpaperItem> GetVisibleWorkshopRanked()
        {
            List<WallpaperItem> ranked = _controller.GetRankedWorkshop();
            if (!_controller.Settings.PreferScenery) return ranked;
            List<WallpaperItem> filtered = new List<WallpaperItem>();
            foreach (WallpaperItem item in ranked)
            {
                if (_typeFilter.HasValue && item.MatchWeather != _typeFilter.Value) continue;
                if (!IsBlockedSceneItem(item)) filtered.Add(item);
            }
            return filtered;
        }

        private void AppendNewWorkshopItems()
        {
            if (_rebuilding || IsDisposed || !_controller.WorkshopTopEnabled) return;
            bool enabled = _controller.WorkshopTopEnabled;
            if (!enabled) return;
            if (_page != 0) return;
            List<WallpaperItem> ranked = GetVisibleWorkshopRanked();
            if (ranked.Count == 0) return;
            _empty.Visible = false;
            string featuredLabel = "订阅榜最高";
            HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
            int scroll = 0;
            if (_gridView)
            {
                _grid.Visible = true;
                _list.Visible = false;
                foreach (WorkshopGridCard card in _cards) existing.Add(card.Item.Id);
                try { scroll = _grid.VerticalScroll.Value; } catch { }
                _grid.SuspendLayout();
                foreach (WallpaperItem item in ranked)
                {
                    if (_cards.Count >= PageSize) break;
                    if (existing.Contains(item.Id)) continue;
                    WorkshopGridCard card = CreateGridCard(item, false, featuredLabel);
                    if (card == null) continue;
                    _cards.Add(card);
                    _grid.Controls.Add(card);
                }
                _grid.ResumeLayout();
                LayoutCards();
                try { _grid.VerticalScroll.Value = Math.Min(scroll, _grid.VerticalScroll.Maximum); }
                catch { }
            }
            else
            {
                _list.Visible = true;
                _grid.Visible = false;
                foreach (WorkshopRow row in _rows) existing.Add(row.ItemId);
                try { scroll = _list.VerticalScroll.Value; } catch { }
                _list.SuspendLayout();
                foreach (WallpaperItem item in ranked)
                {
                    if (_rows.Count >= PageSize) break;
                    if (existing.Contains(item.Id)) continue;
                    WorkshopRow row = CreateRow(item, false, featuredLabel);
                    if (row == null) continue;
                    _rows.Add(row);
                    _list.Controls.Add(row);
                }
                _list.ResumeLayout();
                LayoutRows();
                try { _list.VerticalScroll.Value = Math.Min(scroll, _list.VerticalScroll.Maximum); }
                catch { }
            }
            UpdateStatus();
        }

        private WorkshopGridCard CreateGridCard(WallpaperItem item, bool featured, string featuredLabel)
        {
            if (IsBlockedSceneItem(item)) return null;
            WorkshopGridCard card = new WorkshopGridCard(item, featured);
            card.FeaturedLabel = featuredLabel;
            card.Hovered += delegate(WorkshopGridCard hovered)
            {
                if (IsDisposed || !_grid.Visible) return;
                Point location = HoverPlacement.Find(hovered, this, _hoverPanel.Size);
                _hoverPanel.ShowItem(hovered.Item, location, ClientSize);
            };
            card.Unhovered += delegate { if (!IsDisposed) _hoverPanel.Visible = false; };
            card.Activate += async delegate(WallpaperItem clicked)
            {
                await _controller.ApplyWorkshopItemAsync(clicked);
            };
            card.BlockRequested += delegate(WallpaperItem blocked)
            {
                _hoverPanel.Visible = false;
                _blockTip.HideTip();
                _controller.BlockWallpaper(blocked);
            };
            card.BlockHovered += delegate
            {
                if (IsDisposed) return;
                Rectangle anchor = card.RectangleToScreen(card.BlockBounds);
                _blockTip.ShowBelow("屏蔽后不会在工坊出现，可在设置中取消屏蔽", this, anchor);
            };
            card.BlockUnhovered += delegate { if (!IsDisposed) _blockTip.HideTip(); };
            return card;
        }

        private WorkshopRow CreateRow(WallpaperItem item, bool featured, string featuredLabel)
        {
            if (IsBlockedSceneItem(item)) return null;
            WorkshopRow row = new WorkshopRow(_controller, item, featured);
            row.FeaturedLabel = featuredLabel;
            row.Margin = new Padding(0);
            row.Width = Math.Max(400, _list.ClientSize.Width - 22);
            row.OpenRequested += delegate(WallpaperItem clicked)
            {
                _controller.OpenWorkshopPage(clicked);
            };
            row.ApplyRequested += async delegate(WallpaperItem clicked)
            {
                await _controller.ApplyWorkshopItemAsync(clicked);
            };
            return row;
        }

        private void ChangePage(int delta)
        {
            int next = _page + delta;
            if (next < 0 || next >= _pageCount) return;
            _page = next;
            Rebuild();
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(delegate
                {
                    if (IsDisposed) return;
                    try { _grid.AutoScrollPosition = Point.Empty; } catch { }
                    try { _list.AutoScrollPosition = Point.Empty; } catch { }
                    try { _grid.VerticalScroll.Value = 0; } catch { }
                    try { _list.VerticalScroll.Value = 0; } catch { }
                    _hoverPanel.Visible = false;
                }));
            }
        }

        private void Rebuild()
        {
            if (_rebuilding || IsDisposed) return;
            _rebuilding = true;
            if (_blockTip != null) _blockTip.HideTip();
            try
            {
                bool enabled = true;
                _refresh.Visible = enabled;
                _rescan.Visible = enabled;
                _refresh.Enabled = enabled;
                _rescan.Enabled = enabled;

                int scroll = 0;
                int gridScroll = 0;
                try { scroll = _list.VerticalScroll.Value; } catch { }
                try { gridScroll = _grid.VerticalScroll.Value; } catch { }
                _list.SuspendLayout();
                _grid.SuspendLayout();
                foreach (WorkshopRow row in _rows) row.Dispose();
                _rows.Clear();
                foreach (WorkshopGridCard card in _cards) card.Dispose();
                _cards.Clear();
                _list.Controls.Clear();
                _grid.Controls.Clear();
                _list.Visible = false;
                _grid.Visible = false;
                _hoverPanel.Visible = false;
                _viewButton.Visible = enabled;
                _sceneryButton.Visible = enabled;
                _viewButton.Text = _gridView ? "列表视图" : "网格视图";
                _sceneryButton.Text = _controller.Settings.PreferScenery ? "风景优先：开" : "风景优先：关";
                string featuredLabel = "订阅榜最高";

                List<WallpaperItem> ranked = GetVisibleWorkshopRanked();
                if (ranked.Count == 0)
                {
                    _empty.Visible = true;
                    if (_controller.WorkshopBusy)
                    {
                        _empty.Text = _controller.Settings.PreferScenery
                            ? "正在抓取 9 个未屏蔽标签，每个标签保留前 100 张…\n\n这可能需要一些时间，请保持网络畅通。"
                            : "正在抓取 " + WorkshopContentTags.AllCount + " 个 Steam 标签，每个标签保留前 50 张…\n\n这可能需要一些时间，请保持网络畅通。";
                        _status.ForeColor = Theme.Muted;
                    }
                    else if (!String.IsNullOrEmpty(_controller.WorkshopError))
                    {
                        _empty.Text = "读取创意工坊失败。\n\n" + _controller.WorkshopError
                            + "\n\n如果使用加速器，请开启 Steam 社区加速后再试。";
                        _status.ForeColor = Theme.Red;
                    }
                    else
                    {
                        _empty.Text = _controller.Settings.PreferScenery
                            ? "还没有工坊候选。\n\n点击“重新抓取”为 9 个未屏蔽标签各保留前 100 张。"
                            : "还没有工坊候选。\n\n点击“重新抓取”为 " + WorkshopContentTags.AllCount + " 个 Steam 标签各保留前 50 张。";
                        _status.ForeColor = Theme.Muted;
                    }
                    _empty.BringToFront();
                    UpdateStatus();
                    return;
                }

                _empty.Visible = false;
                int totalPages = Math.Max(1, (ranked.Count + PageSize - 1) / PageSize);
                _pageCount = totalPages;
                if (_page >= _pageCount) _page = _pageCount - 1;
                if (_page < 0) _page = 0;
                int start = _page * PageSize;
                int limit = Math.Min(PageSize, ranked.Count - start);
                if (_gridView)
                {
                    _grid.Visible = true;
                    for (int i = 0; i < limit; i++)
                    {
                        WorkshopGridCard card = CreateGridCard(ranked[start + i], false, featuredLabel);
                        if (card == null) continue;
                        _cards.Add(card);
                        _grid.Controls.Add(card);
                    }
                    _grid.ResumeLayout();
                    LayoutCards();
                    try { _grid.VerticalScroll.Value = Math.Min(gridScroll, _grid.VerticalScroll.Maximum); }
                    catch { }
                }
                else
                {
                    _list.Visible = true;
                    for (int i = 0; i < limit; i++)
                    {
                        WorkshopRow row = CreateRow(ranked[start + i], false, featuredLabel);
                        if (row == null) continue;
                        _rows.Add(row);
                        _list.Controls.Add(row);
                    }
                    _list.ResumeLayout();
                    LayoutRows();
                    try { _list.VerticalScroll.Value = Math.Min(scroll, _list.VerticalScroll.Maximum); }
                    catch { }
                }
                UpdateStatus();
                UpdatePager();
                Form host = FindForm();
                if (host != null && host.IsHandleCreated && host.Visible)
                {
                    Invalidate(true);
                    WindowFx.Repaint(host);
                }
            }
            finally
            {
                _list.ResumeLayout(false);
                _grid.ResumeLayout(false);
                _rebuilding = false;
            }
        }

        private void UpdateStatus()
        {
            if (_status == null) return;
            bool enabled = _controller.WorkshopTopEnabled;
            if (!enabled) return;
            string status = _controller.WorkshopStatus;
            if (_controller.WorkshopError != null && _controller.WorkshopError.Length > 0)
                status = "错误：" + _controller.WorkshopError;
            int installed = 0;
            foreach (WallpaperItem item in _controller.WorkshopItems) if (item.IsInstalled) installed++;
            int displayCount = _controller.WorkshopDisplayCount;
            string summary = _controller.WorkshopItems.Count > 0
                ? "  ·  已安装 " + installed + " / " + displayCount
                    + (_controller.WorkshopExcludedCount > 0
                        ? "  ·  过滤 " + _controller.WorkshopExcludedCount + " 个不适合项目" : "")
                    + (_controller.Settings.PreferScenery && _controller.WorkshopSceneryFilteredCount > 0
                        ? "  ·  风景优先过滤 " + _controller.WorkshopSceneryFilteredCount + " 个" : "")
                    + (_controller.WorkshopAgeFilteredCount > 0
                        ? "  ·  分级隐藏 " + _controller.WorkshopAgeFilteredCount + " 个" : "")
                    + "  ·  第 " + (_page + 1) + "/" + _pageCount + " 页"
                : "";
            string refreshText = "";
            if (_controller.WorkshopTopEnabled)
            {
                if (!_controller.WorkshopAutoRefresh) refreshText = "  ·  自动抓取已关闭";
                else if (_controller.WorkshopItems.Count == 0) refreshText = "  ·  等待首次抓取";
                else
                {
                    int days = _controller.WorkshopDaysUntilRefresh;
                    refreshText = days <= 0 ? "  ·  距离下一次抓取壁纸：今天"
                        : "  ·  距离下一次抓取壁纸还有 " + days + " 天";
                }
            }
            _status.Text = status + summary + refreshText;
            _status.ForeColor = _controller.WorkshopError != null && _controller.WorkshopError.Length > 0
                ? Theme.Red : Theme.Muted;
            _refresh.Enabled = true;
            _rescan.Enabled = true;
            _viewButton.Enabled = true;
            _sceneryButton.Enabled = true;
            _refresh.Enabled = true;
            UpdatePager();
        }

        private void UpdatePager()
        {
            if (_pageLabel == null) return;
            _pageLabel.Text = (_page + 1) + " / " + _pageCount;
            _prevPage.Enabled = _page > 0;
            _nextPage.Enabled = _page + 1 < _pageCount;
            bool visible = _controller.WorkshopTopEnabled;
            _prevPage.Visible = visible;
            _nextPage.Visible = visible;
            _pageLabel.Visible = visible;
            _typeChips.Visible = visible;
        }

        private int ContentTop
        {
            get
            {
                int chipsTop = _detailsExpanded ? 78 : 42;
                return Width < 1080 ? chipsTop + 94 : chipsTop + 34;
            }
        }

        private void LayoutCards()
        {
            if (_grid == null) return;
            int scroll = 0;
            try { scroll = _grid.VerticalScroll.Value; } catch { }
            _grid.AutoScrollMinSize = Size.Empty;
            _grid.AutoScrollPosition = new Point(0, 0);
            try { _grid.VerticalScroll.Value = 0; } catch { }
            int gap = 12;
            int panelWidth = Math.Max(400, _grid.ClientSize.Width - 22);
            int columns = Math.Max(3, Math.Min(8, (panelWidth + gap) / (230 + gap)));
            _columns = columns;
            int cardWidth = Math.Max(160, (panelWidth - gap * (columns - 1)) / columns);
            int imageHeight = (int)Math.Round(cardWidth * 9.0 / 16.0);
            int cardHeight = imageHeight + 72;
            for (int i = 0; i < _cards.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                _cards[i].SetBounds(column * (cardWidth + gap), row * (cardHeight + gap),
                    cardWidth, cardHeight);
            }
            int rows = (_cards.Count + columns - 1) / columns;
            _grid.AutoScrollMinSize = new Size(0, rows * (cardHeight + gap) + 18);
            try { _grid.VerticalScroll.Value = Math.Min(scroll, _grid.VerticalScroll.Maximum); }
            catch { }
            _empty.SetBounds(0, ContentTop + 8, Width, Math.Max(0, Height - ContentTop - 8));
        }

        private void LayoutRows()
        {
            if (_list == null) return;
            int scroll = 0;
            try { scroll = _list.VerticalScroll.Value; } catch { }
            _list.AutoScrollMinSize = Size.Empty;
            _list.AutoScrollPosition = new Point(0, 0);
            try { _list.VerticalScroll.Value = 0; } catch { }
            int width = Math.Max(400, _list.ClientSize.Width - 22);
            int y = 0;
            foreach (WorkshopRow row in _rows)
            {
                int height = row.Featured ? 156 : 128;
                row.SetBounds(0, y, width, height);
                y += height + 10;
            }
            _list.AutoScrollMinSize = new Size(0, y + 18);
            try { _list.VerticalScroll.Value = Math.Min(scroll, _list.VerticalScroll.Maximum); }
            catch { }
            _empty.SetBounds(0, ContentTop + 8, Width, Math.Max(0, Height - ContentTop - 8));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width < 700) return;
            _refresh.Location = new Point(Width - _refresh.Width, 2);
            _rescan.Location = new Point(Width - _refresh.Width - _rescan.Width - 8, 2);
            _viewButton.Location = new Point(
                Width - _refresh.Width - _rescan.Width - _viewButton.Width - 16, 2);
            _sceneryButton.Location = new Point(
                _viewButton.Left - _sceneryButton.Width - 8, 2);
            _status.Visible = _detailsExpanded;
            _status.SetBounds(0, 52, Math.Max(100, Width - 20), 24);
            int chipsTop = _detailsExpanded ? 78 : 42;
            bool compactChips = Width < 1080;
            _typeChips.Wrap = compactChips;
            _typeChips.SetBounds(0, chipsTop, compactChips ? Width : Math.Max(300, Width - 224),
                compactChips ? 56 : 28);
            int pagerTop = compactChips ? chipsTop + 60 : chipsTop;
            _prevPage.Location = new Point(Math.Max(0, Width - 202), pagerTop);
            _pageLabel.Location = new Point(Math.Max(0, Width - 138), pagerTop);
            _nextPage.Location = new Point(Math.Max(0, Width - 74), pagerTop);
            int contentTop = compactChips ? chipsTop + 94 : chipsTop + 34;
            _list.SetBounds(0, contentTop, Width, Math.Max(0, Height - contentTop));
            _grid.SetBounds(0, contentTop, Width, Math.Max(0, Height - contentTop));
            _empty.SetBounds(0, contentTop + 8, Width, Math.Max(0, Height - contentTop - 8));
            _hoverPanel.Visible = false;
            if (_blockTip != null) _blockTip.HideTip();
            LayoutRows();
            LayoutCards();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _detailsArrowRect.Contains(e.Location))
            {
                _detailsExpanded = !_detailsExpanded;
                _status.Visible = _detailsExpanded;
                OnResize(EventArgs.Empty);
                Invalidate();
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = _detailsArrowRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            Theme.DrawText(e.Graphics, "创意工坊精选", Theme.FontTitle, Theme.Text, 0, 0);
            _detailsArrowRect = new Rectangle(158, 2, 28, 26);
            using (SolidBrush arrow = new SolidBrush(Theme.Text))
            {
                if (_detailsExpanded)
                {
                    e.Graphics.FillPolygon(arrow, new[]
                    {
                        new Point(166, 11), new Point(181, 11), new Point(173, 20)
                    });
                }
                else
                {
                    e.Graphics.FillPolygon(arrow, new[]
                    {
                        new Point(168, 8), new Point(180, 15), new Point(168, 22)
                    });
                }
            }
            if (_detailsExpanded)
            {
                string subtitle = _controller.Settings.PreferScenery
                    ? "9 个未屏蔽标签各保留 100"
                    : WorkshopContentTags.AllCount + " 个 Steam 标签各保留 50";
                Theme.DrawText(e.Graphics, subtitle, Theme.FontTiny, Theme.Dim, 0, 34);
            }
            base.OnPaint(e);
        }
    }
}
