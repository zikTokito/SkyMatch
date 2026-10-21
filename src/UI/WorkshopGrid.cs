using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SkyMatch
{
    public static class HoverPlacement
    {
        public static Point Find(Control card, Control host, Size panel)
        {
            const int gap = 12;
            const int margin = 8;
            const int topMin = 70;
            Rectangle cardScreen = card.RectangleToScreen(card.ClientRectangle);
            Rectangle local = host.RectangleToClient(cardScreen);
            int rightX = local.Right + gap;
            int leftX = local.Left - gap - panel.Width;
            bool rightFits = rightX + panel.Width <= host.ClientSize.Width - margin;
            bool leftFits = leftX >= margin;
            int x;
            int y = local.Top;
            if (rightFits)
            {
                x = rightX;
            }
            else if (leftFits)
            {
                x = leftX;
            }
            else
            {
                // No room on either side: put it above/below the card so the
                // hovered card itself stays uncovered.
                x = Math.Max(margin, Math.Min(local.Left, host.ClientSize.Width - panel.Width - margin));
                int above = local.Top - panel.Height - gap;
                int below = local.Bottom + gap;
                if (above >= topMin) y = above;
                else if (below + panel.Height <= host.ClientSize.Height - margin) y = below;
                else
                {
                    bool moreRight = (host.ClientSize.Width - local.Right) > local.Left;
                    x = moreRight
                        ? Math.Max(margin, Math.Min(host.ClientSize.Width - panel.Width - margin, rightX))
                        : Math.Max(margin, leftX);
                }
            }
            if (y + panel.Height > host.ClientSize.Height - margin)
                y = Math.Max(topMin, host.ClientSize.Height - panel.Height - margin);
            if (y < topMin) y = topMin;
            return new Point(x, y);
        }
    }

    public class WorkshopGridCard : Control
    {
        private bool _hover;
        private bool _blockHover;
        private RectangleF _blockRect;

        public WallpaperItem Item { get; set; }
        public bool Featured { get; set; }
        public string FeaturedLabel { get; set; }
        public string BlockLabel { get; set; }
        public Rectangle BlockBounds { get { return Rectangle.Round(_blockRect); } }
        public event Action<WallpaperItem> Activate;
        public event Action<WallpaperItem> BlockRequested;
        public event Action<WorkshopGridCard> BlockHovered;
        public event EventHandler BlockUnhovered;
        public event Action<WorkshopGridCard> Hovered;
        public event EventHandler Unhovered;

        public WorkshopGridCard(WallpaperItem item, bool featured)
        {
            Item = item;
            Featured = featured;
            FeaturedLabel = "最佳匹配";
            BlockLabel = "屏蔽";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            Action<WorkshopGridCard> handler = Hovered;
            if (handler != null) handler(this);
            base.OnMouseEnter(e);
        }

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
            bool overBlock = _hover && _blockRect.Contains(e.Location);
            if (overBlock != _blockHover)
            {
                _blockHover = overBlock;
                if (_blockHover)
                {
                    Action<WorkshopGridCard> handler = BlockHovered;
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
            if (e.Button == MouseButtons.Left)
            {
                if (_hover && _blockRect.Contains(e.Location))
                {
                    Action<WallpaperItem> block = BlockRequested;
                    if (block != null) block(Item);
                    base.OnMouseDown(e);
                    return;
                }
                Action<WallpaperItem> handler = Activate;
                if (handler != null) handler(Item);
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            Color accent = Theme.Accent(Item.MatchWeather);
            RectangleF card = new RectangleF(0, 0, Width - 1, Height - 1);
            using (SolidBrush brush = new SolidBrush(_hover ? Theme.CardHover : Theme.Card))
                Theme.FillRounded(e.Graphics, brush, card, 12);
            using (Pen pen = new Pen(_hover ? Theme.Alpha(accent, 220) : Theme.Border, _hover ? 1.4f : 1f))
                Theme.DrawRounded(e.Graphics, pen, card, 12);

            int imageHeight = Math.Max(80, Height - 72);
            RectangleF imageRect = new RectangleF(6, 6, Width - 13, imageHeight - 6);
            Bitmap image = AppImages.GetWallpaper(Item, (int)imageRect.Width, (int)imageRect.Height);
            if (image != null)
            {
                using (GraphicsPath clip = Theme.RoundedRect(imageRect, 9))
                {
                    GraphicsState state = e.Graphics.Save();
                    e.Graphics.SetClip(clip);
                    e.Graphics.DrawImage(image, imageRect);
                    e.Graphics.Restore(state);
                }
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(accent, 45)))
                    Theme.FillRounded(e.Graphics, brush, imageRect, 9);
                WeatherGlyph.Draw(e.Graphics, Item.MatchWeather,
                    new RectangleF(imageRect.X + imageRect.Width / 2 - 18, imageRect.Y + imageRect.Height / 2 - 18, 36, 36),
                    accent, 210);
            }

            if (Item.IsInstalled)
            {
                string installed = "已安装";
                SizeF installedSize = e.Graphics.MeasureString(installed, Theme.FontTiny);
                RectangleF badge = new RectangleF(Width - installedSize.Width - 27, 13,
                    installedSize.Width + 14, 21);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(180, 12, 25, 19)))
                    Theme.FillRounded(e.Graphics, brush, badge, 7);
                Theme.DrawText(e.Graphics, installed, Theme.FontTiny, Theme.Green, badge.X + 7, badge.Y + 2);
            }

            int textY = imageHeight + 7;
            Theme.DrawText(e.Graphics, Theme.FitText(e.Graphics, Item.Title, Theme.FontBold, Width - 24),
                Theme.FontBold, Theme.Text, 12, textY);
            string meta = (Item.WorkshopSubscriptions > 0 ? Item.WorkshopSubscriptions + " 订阅" : "")
                + (Item.IsInstalled ? "  ·  已安装" : "  ·  待订阅");
            Theme.DrawText(e.Graphics, meta, Theme.FontTiny,
                Item.IsInstalled ? Theme.Green : Theme.Muted, 12, textY + 21);

            using (SolidBrush brush = new SolidBrush(Theme.Alpha(accent, 38)))
                Theme.FillRounded(e.Graphics, brush, new RectangleF(12, Height - 26,
                    Math.Min(76, Width - 24), 19), 6);
            Theme.DrawText(e.Graphics, WeatherKindInfo.Name(Item.MatchWeather), Theme.FontTiny,
                accent, 18, Height - 24);
            Theme.DrawText(e.Graphics, TimeSlotInfo.Name(Item.PrimaryTime), Theme.FontTiny,
                Theme.TimeColor(Item.PrimaryTime), 92, Height - 24);

            string blockLabel = String.IsNullOrEmpty(BlockLabel) ? "屏蔽" : BlockLabel;
            SizeF blockText = e.Graphics.MeasureString(blockLabel, Theme.FontBold);
            float blockWidth = Math.Max(74, blockText.Width + 24);
            _blockRect = new RectangleF(Math.Max(8, Width - blockWidth - 14), Height - 34,
                blockWidth, 27);
            if (_hover)
            {
                using (SolidBrush block = new SolidBrush(Color.FromArgb(220, 58, 24, 30)))
                    Theme.FillRounded(e.Graphics, block, _blockRect, 9);
                using (Pen blockBorder = new Pen(Color.FromArgb(180, 255, 111, 117), 1f))
                    Theme.DrawRounded(e.Graphics, blockBorder, _blockRect, 9);
                Theme.DrawText(e.Graphics, blockLabel, Theme.FontBold, Theme.Text,
                    _blockRect.X + (_blockRect.Width - blockText.Width) / 2f,
                    _blockRect.Y + (_blockRect.Height - blockText.Height) / 2f);
            }
        }
    }

    public class WorkshopHoverPanel : Control
    {
        public WallpaperItem Item { get; set; }

        public WorkshopHoverPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Card;
            Size = new Size(460, 356);
            Visible = false;
        }

        public void ShowItem(WallpaperItem item, Point location, Size parentSize)
        {
            Item = item;
            Visible = true;
            BringToFront();
            int x = location.X;
            int y = location.Y;
            if (x + Width > parentSize.Width - 10) x = parentSize.Width - Width - 10;
            if (y + Height > parentSize.Height - 10) y = parentSize.Height - Height - 10;
            if (x < 8) x = 8;
            if (y < 70) y = 70;
            Location = new Point(x, y);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            Color accent = Theme.Accent(Item.MatchWeather);
            RectangleF panel = new RectangleF(0, 0, Width - 1, Height - 1);
            using (SolidBrush brush = new SolidBrush(Theme.Card))
                Theme.FillRounded(e.Graphics, brush, panel, 13);
            using (Pen pen = new Pen(Theme.Alpha(accent, 190), 1.3f))
                Theme.DrawRounded(e.Graphics, pen, panel, 13);

            RectangleF imageRect = new RectangleF(16, 16, Width - 32, 232);
            Bitmap image = AppImages.GetWallpaper(Item, (int)imageRect.Width, (int)imageRect.Height);
            if (image != null)
            {
                using (GraphicsPath clip = Theme.RoundedRect(imageRect, 9))
                {
                    GraphicsState state = e.Graphics.Save();
                    e.Graphics.SetClip(clip);
                    e.Graphics.DrawImage(image, imageRect);
                    e.Graphics.Restore(state);
                }
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(accent, 50)))
                    Theme.FillRounded(e.Graphics, brush, imageRect, 9);
            }

            Theme.DrawText(e.Graphics, Theme.FitText(e.Graphics, Item.Title, Theme.FontBold, Width - 32),
                Theme.FontBold, Theme.Text, 18, 258);
            string meta = (Item.WorkshopSubscriptions > 0 ? Item.WorkshopSubscriptions + " 订阅" : "")
                + (Item.IsInstalled ? "  ·  已安装" : "  ·  待订阅");
            Theme.DrawText(e.Graphics, Theme.FitText(e.Graphics, meta, Theme.FontTiny, Width - 32),
                Theme.FontTiny, Item.IsInstalled ? Theme.Green : Theme.Muted, 18, 282);

            string tags = Item.Tags == null ? "" : String.Join(" · ", Item.Tags.ToArray());
            Theme.DrawText(e.Graphics, Theme.FitText(e.Graphics, tags, Theme.FontTiny, Width - 32),
                Theme.FontTiny, Theme.Dim, 18, 303);

            DrawPill(e.Graphics, 18, 323, WeatherKindInfo.Name(Item.MatchWeather), accent);
            DrawPill(e.Graphics, 98, 323, TimeSlotInfo.Name(Item.PrimaryTime),
                Theme.TimeColor(Item.PrimaryTime));
            Theme.DrawText(e.Graphics, "天气 " + Math.Round(Item.Confidence) + "%", Theme.FontTiny,
                Theme.Dim, 178, 326);
            Theme.DrawText(e.Graphics, Item.IsInstalled ? "点击卡片应用" : "点击卡片订阅",
                Theme.FontTiny, Theme.Alpha(accent, 230), Width - 105, 326);
        }

        private static void DrawPill(Graphics g, float x, float y, string text, Color color)
        {
            SizeF size = g.MeasureString(text, Theme.FontTiny);
            RectangleF pill = new RectangleF(x, y, size.Width + 16, 20);
            using (SolidBrush brush = new SolidBrush(Theme.Alpha(color, 40)))
                Theme.FillRounded(g, brush, pill, 6);
            Theme.DrawText(g, text, Theme.FontTiny, color, x + 8, y + 2);
        }
    }
}
