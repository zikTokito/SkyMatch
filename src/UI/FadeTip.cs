using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SkyMatch
{
    public class FadeTip : Control
    {
        private readonly Timer _timer;
        private string _text = "";
        private int _alpha;
        private int _target;

        public FadeTip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Visible = false;
            Enabled = false;
            Size = new Size(370, 48);
            _timer = new Timer();
            _timer.Interval = 24;
            _timer.Tick += delegate
            {
                if (_alpha < _target) _alpha = Math.Min(_target, _alpha + 38);
                else if (_alpha > _target) _alpha = Math.Max(_target, _alpha - 38);
                if (_alpha == _target && _target == 0)
                {
                    _timer.Stop();
                    Visible = false;
                }
                Invalidate();
            };
        }

        public void ShowTip(string text, Point parentPoint, Size parentSize)
        {
            _text = text ?? "";
            Size textSize = TextRenderer.MeasureText(_text, Theme.FontTiny, new Size(420, 200),
                TextFormatFlags.WordBreak);
            Width = Math.Min(440, Math.Max(210, textSize.Width + 28));
            Height = Math.Max(42, textSize.Height + 18);
            int x = parentPoint.X - Width / 2;
            int y = parentPoint.Y - Height - 12;
            if (x < 8) x = 8;
            if (x + Width > parentSize.Width - 8) x = parentSize.Width - Width - 8;
            if (y < 8) y = parentPoint.Y + 18;
            if (y + Height > parentSize.Height - 8) y = parentSize.Height - Height - 8;
            Location = new Point(x, y);
            _alpha = 0;
            _target = 232;
            Visible = true;
            BringToFront();
            UpdateRegion();
            _timer.Start();
            Invalidate();
        }

        private void UpdateRegion()
        {
            using (GraphicsPath path = Theme.RoundedRect(new RectangleF(0, 0, Width, Height), 9))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        public void ShowAtCursor(string text, Control parent)
        {
            if (parent == null) return;
            Point point = parent.PointToClient(Cursor.Position);
            ShowTip(text, point, parent.ClientSize);
        }

        public void ShowBelow(string text, Control parent, Rectangle screenAnchor)
        {
            if (parent == null) return;
            _text = text ?? "";
            int maxWidth = Math.Max(180, Math.Min(460, parent.ClientSize.Width - 16));
            Size natural = TextRenderer.MeasureText(_text, Theme.FontTiny, new Size(10000, 200),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            Size textSize;
            if (natural.Width + 40 <= maxWidth)
            {
                Width = natural.Width + 40;
                textSize = natural;
            }
            else
            {
                Width = maxWidth;
                textSize = TextRenderer.MeasureText(_text, Theme.FontTiny,
                    new Size(Width - 32, 400), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            }
            Height = Math.Max(34, textSize.Height + 22);
            Point anchor = parent.PointToClient(new Point(screenAnchor.Left, screenAnchor.Top));
            int x = anchor.X + (screenAnchor.Width - Width) / 2;
            int y = anchor.Y + screenAnchor.Height + 8;
            if (x < 6) x = 6;
            if (x + Width > parent.ClientSize.Width - 6)
                x = parent.ClientSize.Width - Width - 6;
            if (y + Height > parent.ClientSize.Height - 6)
                y = Math.Max(6, anchor.Y - Height - 8);
            Location = new Point(x, y);
            _alpha = 0;
            _target = 232;
            Visible = true;
            BringToFront();
            UpdateRegion();
            _timer.Start();
            Invalidate();
        }

        public void HideTip()
        {
            if (!Visible && _alpha == 0) return;
            _target = 0;
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF rect = new RectangleF(0, 0, Width - 1, Height - 1);
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(_alpha, 17, 23, 32)))
                Theme.FillRounded(e.Graphics, fill, rect, 9);
            using (Pen border = new Pen(Color.FromArgb(Math.Min(210, _alpha), 71, 86, 105), 1f))
                Theme.DrawRounded(e.Graphics, border, rect, 9);
            using (SolidBrush text = new SolidBrush(Color.FromArgb(_alpha, Theme.Text)))
                e.Graphics.DrawString(_text, Theme.FontTiny, text,
                    new RectangleF(12, 8, Width - 24, Height - 16));
        }
    }
}
