using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SkyMatch
{
    public class WallpaperCard : Control
    {
        private WallpaperItem _wallpaper;
        private bool _hover;
        private bool _blockHover;
        private bool _suppressClick;
        private RectangleF _blockRect;

        public event Action<WallpaperCard> Hovered;
        public event EventHandler Unhovered;
        public event Action<WallpaperItem> BlockRequested;
        public event Action<WallpaperCard> BlockHovered;
        public event EventHandler BlockUnhovered;
        public Rectangle BlockBounds { get { return Rectangle.Round(_blockRect); } }

        public WallpaperCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
            Size = new Size(300, 238);
        }

        public WallpaperItem Wallpaper
        {
            get { return _wallpaper; }
            set { _wallpaper = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            Action<WallpaperCard> handler = Hovered;
            if (handler != null) handler(this);
            base.OnMouseEnter(e);        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _blockHover = false;
            Invalidate();
            EventHandler blockHandler = BlockUnhovered;
            if (blockHandler != null) blockHandler(this, EventArgs.Empty);
            EventHandler handler = Unhovered;
            if (handler != null) handler(this, EventArgs.Empty);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool overBlock = _hover && _wallpaper != null && _wallpaper.IsRemote
                && _blockRect.Contains(e.Location);
            if (overBlock != _blockHover)
            {
                _blockHover = overBlock;
                if (_blockHover)
                {
                    Action<WallpaperCard> handler = BlockHovered;
                    if (handler != null) handler(this);
                }
                else
                {
                    EventHandler handler = BlockUnhovered;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _hover && _wallpaper != null
                && _wallpaper.IsRemote && _blockRect.Contains(e.Location))
            {
                _suppressClick = true;
                Action<WallpaperItem> handler = BlockRequested;
                if (handler != null) handler(_wallpaper);
                base.OnMouseDown(e);
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnClick(EventArgs e)
        {
            if (_suppressClick)
            {
                _suppressClick = false;
                return;
            }
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            if (_wallpaper == null) return;
            WeatherKind kind = _wallpaper.MatchWeather;
            Color accent = Theme.Accent(kind);

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (GraphicsPath clip = Theme.RoundedRect(rect, 15))
            {
                e.Graphics.SetClip(clip);
                using (SolidBrush bg = new SolidBrush(_hover ? Theme.CardHover : Theme.Card))
                    e.Graphics.FillRectangle(bg, rect);
                int imageH = (int)(Height * 0.62f);
                Bitmap image = AppImages.GetWallpaper(_wallpaper, Width, imageH);
                if (image != null)
                {
                    e.Graphics.DrawImage(image, 0, 0, Width, imageH);
                }
                else
                {
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new Rectangle(0, 0, Width, imageH), Theme.Mix(Theme.Card, accent, 0.22f),
                        Color.FromArgb(12, 17, 24), 90f))
                        e.Graphics.FillRectangle(brush, 0, 0, Width, imageH);
                    WeatherGlyph.Draw(e.Graphics, kind, new RectangleF(Width / 2f - 26, imageH / 2f - 26, 52, 52), accent, 200);
                }
                using (LinearGradientBrush shade = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, imageH + 12),
                    Color.FromArgb(12, 8, 12, 18), Color.FromArgb(180, 8, 12, 18), 90f))
                {
                    Blend blend = new Blend();
                    blend.Factors = new[] { 0f, 0.48f, 1f };
                    blend.Positions = new[] { 0f, 0.55f, 1f };
                    shade.Blend = blend;
                    e.Graphics.FillRectangle(shade, 0, 0, Width, imageH + 12);
                }

                RectangleF badge = new RectangleF(12, 12, 76, 22);
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(accent, 55)))
                    Theme.FillRounded(e.Graphics, brush, badge, 11);
                WeatherGlyph.Draw(e.Graphics, kind, new RectangleF(badge.X + 8, badge.Y + 5, 12, 12), accent, 245);
                Theme.DrawText(e.Graphics, WeatherKindInfo.Name(kind), Theme.FontTiny, Theme.Text, badge.X + 24, badge.Y + 3);

                Color timeColor = Theme.TimeColor(_wallpaper.PrimaryTime);
                RectangleF timeBadge = new RectangleF(94, 12, 66, 22);
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(timeColor, 52)))
                    Theme.FillRounded(e.Graphics, brush, timeBadge, 11);
                Theme.DrawText(e.Graphics, TimeSlotInfo.Name(_wallpaper.PrimaryTime), Theme.FontTiny, timeColor,
                    timeBadge.X + 9, timeBadge.Y + 3);

                if (_wallpaper.IsActive)
                {
                    RectangleF active = new RectangleF(Width - 82, 12, 70, 22);
                    using (SolidBrush brush = new SolidBrush(Theme.Alpha(Theme.Green, 45)))
                        Theme.FillRounded(e.Graphics, brush, active, 11);
                    using (SolidBrush dot = new SolidBrush(Theme.Green))
                        e.Graphics.FillEllipse(dot, active.X + 9, active.Y + 8, 6, 6);
                    Theme.DrawText(e.Graphics, "In use", Theme.FontTiny, Theme.Green, active.X + 20, active.Y + 3);
                }
                else if (_wallpaper.IsManual)
                {
                    RectangleF manual = new RectangleF(Width - 54, 12, 42, 22);
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(45, 255, 255, 255)))
                        Theme.FillRounded(e.Graphics, brush, manual, 11);
                    Theme.DrawText(e.Graphics, "Manual", Theme.FontTiny, Theme.Muted, manual.X + 8, manual.Y + 3);
                }

                _blockRect = RectangleF.Empty;
                e.Graphics.ResetClip();

                float textY = imageH + 8;
                string title = Theme.FitText(e.Graphics, _wallpaper.Title, Theme.FontBold, Width - 28);
                Theme.DrawText(e.Graphics, title, Theme.FontBold, Theme.Text, 14, textY);
                string meta = _wallpaper.TypeLabel + "  ·  " + TimeSlotInfo.Name(_wallpaper.PrimaryTime)
                    + "  ·  " + WeatherKindInfo.Name(kind)
                    + (_wallpaper.IsManual ? "  ·  Manual" : "  ·  " + Math.Round(_wallpaper.Confidence) + "%");
                Theme.DrawText(e.Graphics, meta, Theme.FontTiny, Theme.Alpha(accent, 235), 14, textY + 24);
                Theme.DrawText(e.Graphics, Theme.FitText(e.Graphics, _wallpaper.Reason, Theme.FontTiny, Width - 28),
                    Theme.FontTiny, Theme.Dim, 14, textY + 43);

                if (_hover)
                {
                    using (Pen pen = new Pen(Theme.Alpha(accent, 120), 1f))
                        Theme.DrawRounded(e.Graphics, pen, rect, 15);
                }
                else
                {
                    using (Pen pen = new Pen(_wallpaper.IsActive ? Theme.Alpha(Theme.Green, 90) : Theme.BorderSoft, 1f))
                        Theme.DrawRounded(e.Graphics, pen, rect, 15);
                }
            }
        }
    }

    public class LibraryPage : UserControl
    {
        private readonly AppController _controller;
        private readonly SearchBox _search;
        private readonly ChipStrip _chips;
        private readonly BufferedPanel _grid;
        private readonly Panel _content;
        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
        private readonly List<Control> _cards;
        private readonly Dictionary<string, int> _scrollByFilter = new Dictionary<string, int>();
        private int? _scrollForNextRebuild;
        private WeatherKind? _filter;
        private Label _empty;
        private LibraryDivider _divider;
        private WorkshopHoverPanel _hoverPanel;
        private FadeTip _blockTip;
        private int _columns = 3;
        private readonly Timer _supplementTimer;
        private int _lastWorkshopCount;
        private int _supplementCount;
        private bool _workshopDirty;
        private bool _rebuilding;

        public event Action<WallpaperItem> DetailsRequested;

        public LibraryPage(AppController controller)
        {
            _controller = controller;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
            _cards = new List<Control>();

            _search = new SearchBox();
            _search.Placeholder = "Search title, tag, or description…";
            _search.Size = new Size(260, 36);
            _search.TextChanged += delegate { Rebuild(); };

            _chips = new ChipStrip();
            _chips.Height = 32;
            _chips.SelectedIndexChanged += delegate
            {
                ChipStrip.Chip chip = _chips.SelectedChip;
                WeatherKind? kind = chip != null && chip.Tag is WeatherKind ? (WeatherKind?)(WeatherKind)chip.Tag : null;
                ChangeFilter(kind, false);
            };

            _grid = new BufferedPanel();
            _grid.AutoScroll = true;
            _grid.BackColor = Theme.Bg;
            _grid.Padding = Padding.Empty;
            _content = new Panel();
            _content.BackColor = Theme.Bg;
            _content.Location = Point.Empty;
            _grid.Controls.Add(_content);
            _grid.Resize += delegate { ClampGridScroll(); };
            _grid.Scroll += delegate { ClampGridScroll(); };

            _empty = new Label();
            _empty.AutoSize = false;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = Theme.Dim;
            _empty.Font = Theme.FontMedium;
            _empty.BackColor = Color.Transparent;
            _empty.Visible = false;

            _divider = new LibraryDivider();
            _divider.Height = 44;
            _divider.Text = "The following are wallpapers of the same type in the workshop (hover to see the larger image, click to view details/subscribe)";
            _divider.Visible = false;

            _hoverPanel = new WorkshopHoverPanel();
            _hoverPanel.Visible = false;
            _blockTip = new FadeTip();

            Controls.Add(_search);
            Controls.Add(_chips);
            Controls.Add(_grid);
            Controls.Add(_empty);
            Controls.Add(_hoverPanel);
            Controls.Add(_blockTip);
            _hoverPanel.BringToFront();
            _blockTip.BringToFront();

            _controller.WallpaperApplied += delegate
            {
                if (!IsDisposed && Visible) _grid.Invalidate(true);
            };
            _controller.ActiveWallpaperChanged += delegate
            {
                if (!IsDisposed && Visible) _grid.Invalidate(true);
            };
            _controller.WallpapersUpdated += delegate
            {
                if (IsDisposed) return;
                UpdateChips();
                if (Visible) Rebuild();
            };
            _controller.WorkshopUpdated += delegate
            {
                if (IsDisposed) return;
                _workshopDirty = true;
            };
            _controller.StateChanged += delegate { if (!IsDisposed) UpdateHeader(); };
            _supplementTimer = new Timer();
            _supplementTimer.Interval = 2500;
            _supplementTimer.Tick += delegate
            {
                if (IsDisposed || !Visible || _rebuilding) return;
                if (_workshopDirty || _controller.WorkshopItems.Count != _lastWorkshopCount)
                {
                    _workshopDirty = false;
                    Rebuild();
                }
            };
            _supplementTimer.Start();
            UpdateChips();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !IsDisposed) Rebuild();
        }

        public void RefreshView()
        {
            if (IsDisposed || !Visible) return;
            Rebuild();
        }

        public void FocusWeather(WeatherKind? kind)
        {
            ChangeFilter(kind, true);
        }

        private string FilterKey
        {
            get { return _filter.HasValue ? "W:" + _filter.Value.ToString() : "ALL"; }
        }

        private void ChangeFilter(WeatherKind? kind, bool syncChips)
        {
            _scrollByFilter[FilterKey] = GridScrollY;
            _filter = kind;
            int target;
            _scrollForNextRebuild = _scrollByFilter.TryGetValue(FilterKey, out target) ? target : 0;
            if (syncChips)
            {
                UpdateChips();
                int index = 0;
                if (kind.HasValue)
                {
                    for (int i = 0; i < WeatherKindInfo.All.Length; i++)
                    {
                        if (WeatherKindInfo.All[i] == kind.Value)
                        {
                            index = i + 1;
                            break;
                        }
                    }
                }
                _chips.SetSelectedSilent(index);
            }
            Rebuild();
        }

        private void UpdateChips()
        {
            int previous = _chips.SelectedIndex;
            _chips.Items.Clear();
            int total = _controller.GetLibraryWallpapers().Count;
            _chips.Items.Add(new ChipStrip.Chip("All", null) { Count = total });
            Dictionary<WeatherKind, int> counts = _controller.CountByKindForLibrary();
            foreach (WeatherKind kind in WeatherKindInfo.All)
            {
                int count = 0;
                counts.TryGetValue(kind, out count);
                _chips.Items.Add(new ChipStrip.Chip(WeatherKindInfo.Name(kind), kind) { Count = count });
            }
            if (previous >= _chips.Items.Count) previous = 0;
            _chips.SetSelectedSilent(previous);
            _chips.Invalidate();
        }

        private void Rebuild()
        {
            if (_rebuilding || IsDisposed) return;
            _rebuilding = true;
            WindowFx.SetRedraw(_grid, false);
            try
            {
                _workshopDirty = false;
                int scroll = _scrollForNextRebuild.HasValue
                    ? _scrollForNextRebuild.Value : GridScrollY;
                _scrollForNextRebuild = null;
                try { _grid.AutoScrollPosition = Point.Empty; } catch { }
                try { _grid.AutoScrollMinSize = Size.Empty; } catch { }
                _grid.SuspendLayout();
                foreach (Control card in _cards) card.Dispose();
                _cards.Clear();
                _content.Controls.Clear();

                string query = _search.Text == null ? "" : _search.Text.Trim().ToLowerInvariant();
                List<WallpaperItem> items = _controller.GetLibraryWallpapers();
                List<WallpaperItem> shown = new List<WallpaperItem>();
                foreach (WallpaperItem item in items)
                {
                    if (_filter.HasValue && item.MatchWeather != _filter.Value) continue;
                    if (query.Length > 0)
                    {
                        string haystack = (item.Title + " " + String.Join(" ", item.Tags.ToArray()) + " "
                            + item.Description).ToLowerInvariant();
                        if (haystack.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    }
                    shown.Add(item);
                }
                shown.Sort(delegate(WallpaperItem a, WallpaperItem b)
                {
                    if (a.IsActive != b.IsActive) return a.IsActive ? -1 : 1;
                    if (a.IsManual != b.IsManual) return a.IsManual ? -1 : 1;
                    return String.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
                });

                foreach (WallpaperItem item in shown)
                    AddLibraryCard(item);
                _supplementCount = 0;
                _lastWorkshopCount = _controller.WorkshopItems.Count;
                List<WallpaperItem> supplements = _controller.GetLibraryWorkshopSupplements(_filter, query);
                if (supplements.Count > 0)
                {
                    _divider.Visible = true;
                    _divider.Width = Math.Max(400, _grid.ClientSize.Width - 20);
                    _divider.Text = "Matching Workshop wallpapers · " + supplements.Count
                        + " items (hover for a preview, click for details/subscribe)";
                    _content.Controls.Add(_divider);
                    foreach (WallpaperItem item in supplements)
                    {
                        AddLibraryCard(item);
                        _supplementCount++;
                    }
                }
                _grid.ResumeLayout();
                LayoutGrid();
                int shownCount = shown.Count + _supplementCount;
                _empty.Visible = shownCount == 0;
                _empty.Text = query.Length > 0 || _filter.HasValue
                    ? "No wallpapers match.\nTry another weather tab or clear the search."
                    : "No wallpapers available yet.\nMake sure Wallpaper Engine is installed and you are subscribed.";
                _empty.BringToFront();
                UpdateHeader();
                _hoverPanel.Visible = false;
                SetGridScrollY(scroll);
            }
            finally
            {
                WindowFx.SetRedraw(_grid, true);
                _grid.Invalidate(true);
                _rebuilding = false;
            }
        }

        private void AddLibraryCard(WallpaperItem item)
        {
            if (item.IsRemote)
            {
                AddWorkshopLibraryCard(item);
                return;
            }
            WallpaperCard card = new WallpaperCard();
            card.Wallpaper = item;
            card.Margin = new Padding(0, 0, 12, 12);
            card.Hovered += delegate(WallpaperCard hovered) { ShowLibraryHover(hovered, item); };
            card.Unhovered += delegate { if (!IsDisposed) _hoverPanel.Visible = false; };
            card.Click += delegate
            {
                Action<WallpaperItem> handler = DetailsRequested;
                if (handler != null) handler(item);
            };
            _cards.Add(card);
            _content.Controls.Add(card);
        }

        private void AddWorkshopLibraryCard(WallpaperItem item)
        {
            WorkshopGridCard card = new WorkshopGridCard(item, false);
            card.FeaturedLabel = "workshop";
            card.Margin = new Padding(0, 0, 12, 12);
            card.Hovered += delegate(WorkshopGridCard hovered) { ShowLibraryHover(hovered, item); };
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
                _workshopDirty = true;
                if (IsHandleCreated) BeginInvoke(new Action(Rebuild));
            };
            card.BlockHovered += delegate(WorkshopGridCard hovered)
            {
                if (IsDisposed) return;
                Rectangle anchor = hovered.RectangleToScreen(hovered.BlockBounds);
                _blockTip.ShowBelow("After blocking, it will not appear in the workshop. You can unblock it in the settings.", this, anchor);
            };
            card.BlockUnhovered += delegate { if (!IsDisposed) _blockTip.HideTip(); };
            _cards.Add(card);
            _content.Controls.Add(card);
        }

        private void ShowLibraryHover(Control hovered, WallpaperItem item)
        {
            if (IsDisposed) return;
            Point location = HoverPlacement.Find(hovered, this, _hoverPanel.Size);
            _hoverPanel.ShowItem(item, location, ClientSize);
        }

        private int GridScrollY
        {
            get
            {
                try { return Math.Max(0, -_grid.AutoScrollPosition.Y); }
                catch { return 0; }
            }
        }

        private void SetGridScrollY(int value)
        {
            try
            {
                int max = Math.Max(0, _content.Height - _grid.ClientSize.Height);
                _grid.AutoScrollPosition = new Point(0, Math.Max(0, Math.Min(value, max)));
            }
            catch { }
        }

        private bool _clampingScroll;

        private void HideHorizontalBar()
        {
            try
            {
                _grid.HorizontalScroll.Enabled = false;
                _grid.HorizontalScroll.Visible = false;
                if (_grid.IsHandleCreated) ShowScrollBar(_grid.Handle, 0, false);
            }
            catch { }
        }

        private void ClampGridScroll()
        {
            if (_grid == null || _clampingScroll) return;
            _clampingScroll = true;
            try
            {
                if (_grid.HorizontalScroll.Value != 0) _grid.HorizontalScroll.Value = 0;
                _grid.HorizontalScroll.Enabled = false;
                _grid.HorizontalScroll.Visible = false;
                if (_grid.IsHandleCreated) ShowScrollBar(_grid.Handle, 0, false);
                int max = Math.Max(0, _content.Height - _grid.ClientSize.Height);
                int value = GridScrollY;
                if (value <= 0)
                {
                    _grid.VerticalScroll.Value = 0;
                    if (_content.Top != 0) _grid.AutoScrollPosition = Point.Empty;
                }
                else if (value > max)
                {
                    SetGridScrollY(max);
                }
            }
            catch { }
            finally { _clampingScroll = false; }
        }

        private void LayoutGrid()
        {
            if (_search == null) return;
            _search.Location = new Point(Width - _search.Width, 3);
            _chips.SetBounds(0, 50, Width, 32);
            _grid.SetBounds(0, 94, Width, Math.Max(0, Height - 94));
            _empty.SetBounds(0, 94, Width, Math.Max(0, Height - 94));
            int availableWidth = _grid.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
            int gridWidth = availableWidth - 20;
            if (gridWidth < 300) return;
            Form host = FindForm();
            bool maximized = host != null && host.WindowState == FormWindowState.Maximized;
            int cols = maximized ? 5 : 3;
            int gap = 12;
            int cardWidth = (gridWidth - (cols - 1) * gap) / cols;
            if (cardWidth > 440) cardWidth = 440;
            if (cardWidth < 180) cardWidth = 180;
            int cardHeight = cardWidth <= 366
                ? 238
                : (int)Math.Round(cardWidth * 238.0 / 366.0);
            int rowHeight = cardHeight + 12;
            int gridSpan = cardWidth * cols + (cols - 1) * gap;
            int startX = Math.Max(0, (availableWidth - gridSpan) / 2);
            _columns = cols;
            int scroll = GridScrollY;
            _grid.SuspendLayout();
            try { _grid.AutoScrollPosition = Point.Empty; } catch { }
            try { _grid.AutoScrollMinSize = Size.Empty; } catch { }
            _content.SuspendLayout();
            _content.Width = Math.Max(1, availableWidth);
            int x = startX;
            int y = 0;
            int column = 0;
            foreach (Control child in _content.Controls)
            {
                if (child == _divider)
                {
                    if (column > 0)
                    {
                        y += rowHeight;
                        column = 0;
                        x = startX;
                    }
                    child.SetBounds(startX, y, gridSpan, 44);
                    y += 56;
                    continue;
                }
                child.SetBounds(x, y, cardWidth, cardHeight);
                x += cardWidth + gap;
                column++;
                if (column == cols)
                {
                    column = 0;
                    x = startX;
                    y += rowHeight;
                }
            }
            if (column > 0) y += rowHeight;
            int contentHeight = Math.Max(y + 20, _grid.ClientSize.Height);
            _content.Height = contentHeight;
            _content.ResumeLayout(false);
            _grid.AutoScrollMinSize = new Size(0, contentHeight);
            _grid.ResumeLayout(true);
            _content.PerformLayout();
            _grid.PerformLayout();
            _grid.AutoScrollMinSize = new Size(0, contentHeight);
            try
            {
                _grid.HorizontalScroll.Value = 0;
                _grid.HorizontalScroll.Enabled = false;
                _grid.HorizontalScroll.Visible = false;
                int max = Math.Max(0, contentHeight - _grid.ClientSize.Height);
                SetGridScrollY(Math.Min(scroll, max));
                HideHorizontalBar();
            }
            catch { }
        }

        private void UpdateHeader()
        {
            Invalidate(new Rectangle(0, 0, Width, 46));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutGrid();
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(delegate
                {
                    if (!IsDisposed) LayoutGrid();
                }));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            Theme.DrawText(e.Graphics, "Library", Theme.FontTitle, Theme.Text, 0, 0);
            int total = _controller.GetLibraryWallpapers().Count;
            string sub = total + " wallpapers available · " + _controller.ManualCount + " manually calibrated";
            if (_controller.AutoExcludedCount > 0)
                sub += "  ·  Auto-excluded " + _controller.AutoExcludedCount;
            if (_controller.AgeFilteredCount > 0)
                sub += "  ·  Rating hidden " + _controller.AgeFilteredCount;
            if (_supplementCount > 0)
                sub += "  ·  Workshop extras " + _supplementCount + " items";
            Theme.DrawText(e.Graphics, sub, Theme.FontTiny, Theme.Dim, 0, 34);
            base.OnPaint(e);
        }
    }

    public class LibraryDivider : Control
    {
        public LibraryDivider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF bar = new RectangleF(0, 6, Math.Max(0, Width - 1), Height - 8);
            using (SolidBrush fill = new SolidBrush(Theme.CardHover))
                Theme.FillRounded(e.Graphics, fill, bar, 10);
            using (Pen pen = new Pen(Theme.Border, 1f))
                Theme.DrawRounded(e.Graphics, pen, bar, 10);
            using (SolidBrush accent = new SolidBrush(Theme.Yellow))
                e.Graphics.FillRectangle(accent, bar.X + 14, bar.Y + 10, 3, bar.Height - 20);
            string text = Theme.FitText(e.Graphics, Text, Theme.FontBold, bar.Width - 52);
            Theme.DrawText(e.Graphics, text, Theme.FontBold, Theme.Text, bar.X + 26,
                bar.Y + (bar.Height - e.Graphics.MeasureString(text, Theme.FontBold).Height) / 2f);
            using (Pen line = new Pen(Theme.Alpha(Theme.Yellow, 90), 1f))
                e.Graphics.DrawLine(line, bar.X + 14, bar.Bottom + 3, bar.Right - 14, bar.Bottom + 3);
        }
    }

    public class BufferedPanel : Panel
    {
        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        public BufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            try
            {
                HorizontalScroll.Enabled = false;
                HorizontalScroll.Visible = false;
                if (IsHandleCreated) ShowScrollBar(Handle, 0, false);
            }
            catch { }
        }
    }
}
