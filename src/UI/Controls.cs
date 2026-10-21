using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SkyMatch
{
    public class CardPanel : Panel
    {
        private Color _fill = Theme.Card;
        private Color _border = Theme.BorderSoft;
        private int _radius = 18;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }

        public Color FillColor
        {
            get { return _fill; }
            set { _fill = value; Invalidate(); }
        }

        public Color BorderColor
        {
            get { return _border; }
            set { _border = value; Invalidate(); }
        }

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (SolidBrush brush = new SolidBrush(_fill))
                Theme.FillRounded(e.Graphics, brush, rect, _radius);
            if (_border.A > 0)
            {
                using (Pen pen = new Pen(_border, 1f))
                    Theme.DrawRounded(e.Graphics, pen, rect, _radius);
            }
            base.OnPaint(e);
        }
    }

    public class ActionButton : Control
    {
        public enum IconKind { None, Refresh, Arrow, Play, Plus, Folder, Restore, Grid }
        private bool _hover;
        private bool _pressed;
        private bool _primary;
        private bool _pill;
        private Color _accent = Theme.Yellow;
        private IconKind _icon = IconKind.None;
        private Color _textColor = Theme.Text;
        private Color _fillColor = Color.Transparent;

        public ActionButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.FontBold;
            Cursor = Cursors.Hand;
            Size = new Size(120, 36);
        }

        public bool Primary
        {
            get { return _primary; }
            set { _primary = value; Invalidate(); }
        }

        public bool Pill
        {
            get { return _pill; }
            set { _pill = value; Invalidate(); }
        }

        public Color AccentColor
        {
            get { return _accent; }
            set { _accent = value; Invalidate(); }
        }

        public IconKind Icon
        {
            get { return _icon; }
            set { _icon = value; Invalidate(); }
        }

        public Color TextColor
        {
            get { return _textColor; }
            set { _textColor = value; Invalidate(); }
        }

        public Color FillColor
        {
            get { return _fillColor; }
            set { _fillColor = value; Invalidate(); }
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
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float radius = _pill ? Height / 2f : 10f;
            Color fill;
            Color border;
            Color text;
            if (!Enabled)
            {
                fill = Theme.Alpha(Theme.Border, 90);
                border = Theme.Alpha(Theme.Border, 150);
                text = Theme.Dim;
            }
            else if (_primary)
            {
                fill = _hover ? Theme.Mix(_accent, Color.White, 0.13) : _accent;
                if (_pressed) fill = Theme.Mix(_accent, Color.Black, 0.10);
                border = Theme.Alpha(_accent, 0);
                text = Color.FromArgb(14, 18, 24);
            }
            else if (_fillColor.A > 0)
            {
                fill = _hover ? Theme.Mix(_fillColor, Color.White, 0.06) : _fillColor;
                border = Theme.Alpha(_borderColorOrFill(), 120);
                text = _textColor;
            }
            else
            {
                fill = _hover ? Theme.Alpha(Color.White, 12) : Theme.Alpha(Color.White, 3);
                border = _hover ? Theme.Border : Theme.BorderSoft;
                text = _textColor;
            }
            using (SolidBrush brush = new SolidBrush(fill))
                Theme.FillRounded(e.Graphics, brush, rect, radius);
            if (border.A > 0)
            {
                using (Pen pen = new Pen(border, 1f))
                    Theme.DrawRounded(e.Graphics, pen, rect, radius);
            }

            float iconW = _icon == IconKind.None ? 0 : 16;
            float gap = _icon == IconKind.None ? 0 : 7;
            SizeF textSize = e.Graphics.MeasureString(Text, Font);
            float total = textSize.Width + iconW + gap;
            float startX = (Width - total) / 2f;
            float centerY = Height / 2f;
            if (_icon != IconKind.None)
            {
                RectangleF iconRect = new RectangleF(startX, centerY - 8, 16, 16);
                DrawIcon(e.Graphics, iconRect, text);
            }
            using (SolidBrush brush = new SolidBrush(text))
            {
                e.Graphics.DrawString(Text, Font, brush, startX + iconW + gap,
                    centerY - textSize.Height / 2f + 1);
            }
        }

        private Color _borderColorOrFill()
        {
            return Color.FromArgb(100, _fillColor);
        }

        private void DrawIcon(Graphics g, RectangleF rect, Color color)
        {
            switch (_icon)
            {
                case IconKind.Refresh:
                    IconPainter.Refresh(g, rect, color);
                    break;
                case IconKind.Arrow:
                    IconPainter.Arrow(g, rect, color, false);
                    break;
                case IconKind.Play:
                    using (SolidBrush brush = new SolidBrush(color))
                    {
                        PointF[] pts =
                        {
                            new PointF(rect.X + rect.Width * 0.32f, rect.Y + rect.Height * 0.18f),
                            new PointF(rect.X + rect.Width * 0.80f, rect.Y + rect.Height * 0.50f),
                            new PointF(rect.X + rect.Width * 0.32f, rect.Y + rect.Height * 0.82f)
                        };
                        g.FillPolygon(brush, pts);
                    }
                    break;
                case IconKind.Plus:
                    using (Pen pen = new Pen(color, 1.7f))
                    {
                        g.DrawLine(pen, rect.X + rect.Width * 0.2f, rect.Y + rect.Height / 2f,
                            rect.X + rect.Width * 0.8f, rect.Y + rect.Height / 2f);
                        g.DrawLine(pen, rect.X + rect.Width / 2f, rect.Y + rect.Height * 0.2f,
                            rect.X + rect.Width / 2f, rect.Y + rect.Height * 0.8f);
                    }
                    break;
                case IconKind.Folder:
                    using (Pen pen = new Pen(color, 1.5f))
                    {
                        g.DrawRectangle(pen, rect.X + 2, rect.Y + 4, rect.Width - 4, rect.Height - 7);
                        g.DrawLine(pen, rect.X + 2, rect.Y + 6, rect.X + 7, rect.Y + 6);
                        g.DrawLine(pen, rect.X + 7, rect.Y + 6, rect.X + 9, rect.Y + 3);
                    }
                    break;
                case IconKind.Restore:
                    using (Pen pen = new Pen(color, 1.6f))
                    {
                        g.DrawArc(pen, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4, 30, 280);
                        g.DrawLine(pen, rect.X + 2, rect.Y + 4, rect.X + 2, rect.Y + 9);
                        g.DrawLine(pen, rect.X + 2, rect.Y + 4, rect.X + 7, rect.Y + 4);
                    }
                    break;
                case IconKind.Grid:
                    using (Pen pen = new Pen(color, 1.4f))
                    {
                        float w = rect.Width / 2f - 2;
                        float h = rect.Height / 2f - 2;
                        g.DrawRectangle(pen, rect.X + 1, rect.Y + 1, w, h);
                        g.DrawRectangle(pen, rect.X + rect.Width / 2f + 1, rect.Y + 1, w, h);
                        g.DrawRectangle(pen, rect.X + 1, rect.Y + rect.Height / 2f + 1, w, h);
                        g.DrawRectangle(pen, rect.X + rect.Width / 2f + 1, rect.Y + rect.Height / 2f + 1, w, h);
                    }
                    break;
            }
        }
    }

    public class WindowButton : Control
    {
        public enum ButtonKind { Minimize, Maximize, Close }
        private ButtonKind _kind;
        private bool _hover;
        private bool _maximized;

        public WindowButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(46, 56);
        }

        public ButtonKind Kind
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        public bool IsMaximized
        {
            get { return _maximized; }
            set
            {
                if (_maximized == value) return;
                _maximized = value;
                Invalidate();
            }
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
            if (_hover)
            {
                Color fill = _kind == ButtonKind.Close ? Color.FromArgb(196, 64, 74) : Color.FromArgb(27, 34, 45);
                using (SolidBrush brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            Color color = _hover ? Theme.White : Theme.Muted;
            using (Pen pen = new Pen(color, 1.35f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float cx = Width / 2f;
                float cy = Height / 2f;
                if (_kind == ButtonKind.Minimize)
                {
                    e.Graphics.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                }
                else if (_kind == ButtonKind.Maximize)
                {
                    if (_maximized)
                    {
                        e.Graphics.DrawRectangle(pen, cx - 6, cy - 4, 9, 9);
                        e.Graphics.DrawRectangle(pen, cx - 3, cy - 7, 9, 9);
                    }
                    else
                    {
                        e.Graphics.DrawRectangle(pen, cx - 5, cy - 5, 10, 10);
                    }
                }
                else
                {
                    e.Graphics.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                    e.Graphics.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                }
            }
        }
    }

    public class ToggleSwitch : Control
    {
        private bool _checked;
        private bool _hover;
        private Color _accent = Theme.Green;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(44, 24);
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public Color AccentColor
        {
            get { return _accent; }
            set { _accent = value; Invalidate(); }
        }

        public void SetCheckedSilent(bool value)
        {
            _checked = value;
            Invalidate();
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
            if (e.Button == MouseButtons.Left) Checked = !Checked;
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            float h = Math.Min(Height - 2, 24);
            float w = h * 1.85f;
            RectangleF track = new RectangleF((Width - w) / 2f, (Height - h) / 2f, w, h);
            Color fill = _checked ? (_hover ? Theme.Mix(_accent, Color.White, 0.1) : _accent)
                : (_hover ? Color.FromArgb(57, 67, 81) : Color.FromArgb(44, 53, 66));
            using (SolidBrush brush = new SolidBrush(fill))
                Theme.FillRounded(e.Graphics, brush, track, h / 2f);
            float thumb = h - 6;
            float x = _checked ? track.Right - thumb - 3 : track.X + 3;
            using (SolidBrush brush = new SolidBrush(_checked ? Color.FromArgb(12, 20, 17) : Color.FromArgb(205, 214, 226)))
                e.Graphics.FillEllipse(brush, x, track.Y + 3, thumb, thumb);
        }
    }

    public class NavButton : Control
    {
        private bool _hover;
        private bool _active;
        private int _iconIndex;
        private string _caption = "";

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 44;
            Font = Theme.FontBody;
        }

        public bool Active
        {
            get { return _active; }
            set { _active = value; Invalidate(); }
        }

        public int IconIndex
        {
            get { return _iconIndex; }
            set { _iconIndex = value; Invalidate(); }
        }

        public string Caption
        {
            get { return _caption; }
            set { _caption = value; Invalidate(); }
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
            if (_active || _hover)
            {
                RectangleF bg = new RectangleF(0, 1, Width - 1, Height - 2);
                using (SolidBrush brush = new SolidBrush(_active ? Color.FromArgb(27, 35, 46) : Color.FromArgb(20, 26, 35)))
                    Theme.FillRounded(e.Graphics, brush, bg, 10);
            }
            if (_active)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Yellow))
                    Theme.FillRounded(e.Graphics, brush, new RectangleF(0, 12, 3, Height - 24), 1.5f);
            }
            Color color = _active ? Theme.Text : (_hover ? Theme.Text : Theme.Muted);
            RectangleF iconRect = new RectangleF(15, Height / 2f - 8, 16, 16);
            IconPainter.Nav(e.Graphics, _iconIndex, iconRect, _active ? Theme.Yellow : color);
            using (SolidBrush brush = new SolidBrush(color))
                e.Graphics.DrawString(_caption, Font, brush, 42, Height / 2f - e.Graphics.MeasureString(_caption, Font).Height / 2f + 1);
        }
    }

    public class SearchBox : Control
    {
        private const int EM_SETCUEBANNER = 0x1501;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private readonly TextBox _textBox;
        private bool _focused;
        private bool _hover;
        private string _placeholder = "搜索壁纸…";

        public SearchBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(250, 38);
            _textBox = new TextBox();
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.BackColor = Color.FromArgb(16, 21, 29);
            _textBox.ForeColor = Theme.Text;
            _textBox.Font = Theme.FontSmall;
            _textBox.Location = new Point(37, 11);
            _textBox.Width = Width - 63;
            _textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _textBox.TextChanged += delegate
            {
                Invalidate();
                OnTextChanged(EventArgs.Empty);
            };
            _textBox.GotFocus += delegate { _focused = true; Invalidate(); };
            _textBox.LostFocus += delegate { _focused = false; Invalidate(); };
            _textBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) _textBox.Text = "";
            };
            _textBox.HandleCreated += delegate { ApplyCue(); };
            Controls.Add(_textBox);
            Cursor = Cursors.IBeam;
            Click += delegate { _textBox.Focus(); };
        }

        public override string Text
        {
            get { return _textBox.Text; }
            set { _textBox.Text = value; }
        }

        public TextBox InnerTextBox
        {
            get { return _textBox; }
        }

        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value;
                ApplyCue();
                Invalidate();
            }
        }

        private void ApplyCue()
        {
            try
            {
                if (_textBox != null && _textBox.IsHandleCreated)
                    SendMessage(_textBox.Handle, EM_SETCUEBANNER, (IntPtr)1, _placeholder);
            }
            catch
            {
            }
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_textBox != null) _textBox.Width = Width - 63;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color fill = Color.FromArgb(16, 21, 29);
            Color border = _focused ? Theme.Alpha(Theme.Yellow, 150) : (_hover ? Theme.Border : Theme.BorderSoft);
            using (SolidBrush brush = new SolidBrush(fill))
                Theme.FillRounded(e.Graphics, brush, rect, 10);
            using (Pen pen = new Pen(border, 1f))
                Theme.DrawRounded(e.Graphics, pen, rect, 10);
            using (Pen pen = new Pen(Theme.Dim, 1.4f))
            {
                float cx = 18;
                float cy = Height / 2f;
                e.Graphics.DrawEllipse(pen, cx - 5, cy - 5, 9, 9);
                e.Graphics.DrawLine(pen, cx + 3, cy + 3, cx + 7, cy + 7);
            }
        }
    }

    public class ChipStrip : Control
    {
        public class Chip
        {
            public string Text;
            public object Tag;
            public int Count;

            public Chip(string text, object tag)
            {
                Text = text;
                Tag = tag;
                Count = -1;
            }
        }

        private readonly List<Chip> _items = new List<Chip>();
        private readonly List<RectangleF> _rects = new List<RectangleF>();
        private int _selectedIndex;
        private int _hoverIndex = -1;
        private int _gap = 7;
        private bool _showCounts = true;
        private bool _wrap;

        public event EventHandler SelectedIndexChanged;

        public ChipStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 32;
            Cursor = Cursors.Hand;
        }

        public List<Chip> Items
        {
            get { return _items; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                Invalidate();
                EventHandler handler = SelectedIndexChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public void SetSelectedSilent(int index)
        {
            _selectedIndex = index;
            Invalidate();
        }

        public bool ShowCounts
        {
            get { return _showCounts; }
            set { _showCounts = value; Invalidate(); }
        }

        public bool Wrap
        {
            get { return _wrap; }
            set { _wrap = value; Invalidate(); }
        }

        public Chip SelectedChip
        {
            get
            {
                if (_selectedIndex >= 0 && _selectedIndex < _items.Count) return _items[_selectedIndex];
                return null;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int found = HitTest(e.Location);
            if (found != _hoverIndex)
            {
                _hoverIndex = found;
                Cursor = found >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int found = HitTest(e.Location);
                if (found >= 0) SelectedIndex = found;
            }
            base.OnMouseUp(e);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < _rects.Count; i++)
                if (_rects[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            _rects.Clear();
            float x = 0;
            float y = 2;
            float rowHeight = _wrap ? Math.Max(22, (Height - 5) / 2f) : Height - 4;
            for (int i = 0; i < _items.Count; i++)
            {
                Chip chip = _items[i];
                string text = chip.Text;
                if (_showCounts && chip.Count >= 0) text = text + "  " + chip.Count;
                SizeF size = e.Graphics.MeasureString(text, Theme.FontSmall);
                float w = Math.Max(58, size.Width + 24);
                if (w > Width) w = Width;
                if (_wrap && x > 0 && x + w > Width)
                {
                    x = 0;
                    y += rowHeight + 3;
                }
                else if (!_wrap && x > 0 && x + w > Width)
                {
                    break;
                }
                RectangleF rect = new RectangleF(x, y, w, rowHeight);
                _rects.Add(rect);
                bool selected = i == _selectedIndex;
                Color fill = selected ? Theme.Yellow : (i == _hoverIndex ? Color.FromArgb(31, 39, 51) : Color.FromArgb(20, 26, 35));
                Color border = selected ? Theme.Yellow : (i == _hoverIndex ? Theme.Border : Theme.BorderSoft);
                using (SolidBrush brush = new SolidBrush(fill))
                    Theme.FillRounded(e.Graphics, brush, rect, 8);
                if (!selected)
                {
                    using (Pen pen = new Pen(border, 1f))
                        Theme.DrawRounded(e.Graphics, pen, rect, 8);
                }
                Color color = selected ? Color.FromArgb(18, 22, 28) : Theme.Muted;
                using (SolidBrush brush = new SolidBrush(color))
                    e.Graphics.DrawString(text, Theme.FontSmall, brush, x + 12,
                        y + rowHeight / 2f - size.Height / 2f + 1);
                x += w + _gap;
            }
        }
    }

    public class MinutesInlineEditor : Control
    {
        private readonly Timer _fade = new Timer();
        private readonly Timer _caretTimer = new Timer();
        private bool _showCaret;
        private string _value = "";
        private int _alpha = 255;
        private int _fadeTarget;
        private int _numberLeft;
        private int _numberWidth = 34;
        private int _suffixLeft;

        public event EventHandler ValueChanged;

        public MinutesInlineEditor()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Default;
            TabStop = false;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                SizeF prefix = g.MeasureString("每隔", Theme.FontSmall);
                SizeF suffix = g.MeasureString("分钟自动更换", Theme.FontSmall);
                _numberLeft = (int)Math.Ceiling(prefix.Width) + 5;
                _suffixLeft = _numberLeft + _numberWidth + 5;
                Width = (int)Math.Ceiling(_suffixLeft + suffix.Width) + 2;
                Height = (int)Math.Ceiling(Math.Max(prefix.Height, suffix.Height)) + 4;
            }
            _fade.Interval = 16;
            _fade.Tick += delegate
            {
                if (_fadeTarget > _alpha) _alpha = Math.Min(255, _alpha + 30);
                else _alpha = Math.Max(0, _alpha - 30);
                if (_alpha == _fadeTarget)
                {
                    _fade.Stop();
                    if (_alpha == 0) Visible = false;
                }
                Invalidate();
            };
            _caretTimer.Interval = 700;
            _caretTimer.Tick += delegate
            {
                _caretTimer.Stop();
                _showCaret = false;
                Invalidate();
            };
        }

        public string Value
        {
            get { return _value; }
            set
            {
                string digits = "";
                if (value != null)
                    foreach (char c in value)
                        if (Char.IsDigit(c) && digits.Length < 4) digits += c;
                _value = digits;
                Invalidate();
            }
        }

        public void FadeIn()
        {
            Visible = true;
            _alpha = 0;
            _fadeTarget = 255;
            _fade.Stop();
            _fade.Start();
            Invalidate();
        }

        public void FadeOut()
        {
            if (!Visible) return;
            _fadeTarget = 0;
            _fade.Stop();
            _fade.Start();
        }

        public void SnapTo(bool visible)
        {
            _fade.Stop();
            _alpha = visible ? 255 : 0;
            Visible = visible;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.X < _numberLeft || e.X > _numberLeft + _numberWidth) return;
            Focus();
            _showCaret = true;
            _caretTimer.Stop();
            _caretTimer.Start();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = e.X >= _numberLeft && e.X <= _numberLeft + _numberWidth
                ? Cursors.IBeam : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Cursor = Cursors.Default;
            base.OnMouseLeave(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _showCaret = true;
            _caretTimer.Stop();
            _caretTimer.Start();
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _showCaret = false;
            _caretTimer.Stop();
            Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (Char.IsControl(e.KeyChar)) return;
            if (!Char.IsDigit(e.KeyChar) || _value.Length >= 4)
            {
                e.Handled = true;
                return;
            }
            _value += e.KeyChar;
            e.Handled = true;
            RaiseChanged();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter)
            {
                _showCaret = false;
                _caretTimer.Stop();
                Invalidate();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode != Keys.Back || _value.Length == 0) return;
            _value = _value.Substring(0, _value.Length - 1);
            e.SuppressKeyPress = true;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Invalidate();
            _showCaret = true;
            _caretTimer.Stop();
            _caretTimer.Start();
            EventHandler handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_alpha <= 0) return;
            Theme.Setup(e.Graphics);
            Color text = Color.FromArgb(_alpha, Theme.Text);
            Color dim = Color.FromArgb(_alpha, Theme.Dim);
            Color line = Color.FromArgb(_alpha, Theme.Border);
            Graphics g = e.Graphics;
            float boxLeft = _numberLeft;
            float boxWidth = _numberWidth;
            float suffixLeft = _suffixLeft;
            float textY = 1;
            using (SolidBrush textBrush = new SolidBrush(text))
            using (SolidBrush dimBrush = new SolidBrush(dim))
            using (Pen linePen = new Pen(line, 1.2f))
            {
                g.DrawString("每隔", Theme.FontSmall, dimBrush, 0, textY);
                g.DrawString("分钟自动更换", Theme.FontSmall, dimBrush, suffixLeft, textY);
                g.DrawLine(linePen, boxLeft, Height - 4, boxLeft + boxWidth, Height - 4);
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    RectangleF numberRect = new RectangleF(boxLeft, 0, boxWidth, Height - 5);
                    g.DrawString(_value, Theme.FontBold, textBrush, numberRect, format);
                    if (Focused && _showCaret && _alpha > 180)
                    {
                        float valueWidth = String.IsNullOrEmpty(_value)
                            ? 0 : g.MeasureString(_value, Theme.FontBold).Width;
                        float caretX = boxLeft + (boxWidth - valueWidth) / 2f + valueWidth + 1;
                        using (Pen caret = new Pen(Theme.Yellow, 1.4f))
                            g.DrawLine(caret, caretX, 4, caretX, Height - 6);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fade.Dispose();
                _caretTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class VolumeSlider : Control
    {
        private int _value = 100;
        private bool _muted;
        private bool _dragging;
        private bool _hover;

        public event EventHandler ValueChanged;
        public event EventHandler VolumeCommitted;
        public event EventHandler MuteChanged;

        public VolumeSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(160, 26);
            Cursor = Cursors.Hand;
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int next = Math.Max(0, Math.Min(100, value));
                if (_value == next) return;
                _value = next;
                Invalidate();
                EventHandler handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public bool Muted
        {
            get { return _muted; }
            set
            {
                if (_muted == value) return;
                _muted = value;
                Invalidate();
                EventHandler handler = MuteChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public void SetSilent(int value, bool muted)
        {
            _value = Math.Max(0, Math.Min(100, value));
            _muted = muted;
            Invalidate();
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (e.X < 23)
                {
                    Muted = !Muted;
                }
                else
                {
                    _dragging = true;
                    Capture = true;
                    SetFromX(e.X);
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                SetFromX(e.X);
                EventHandler handler = VolumeCommitted;
                if (handler != null) handler(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        private void SetFromX(int x)
        {
            float left = 29;
            float right = Math.Max(left + 10, Width - 10);
            float ratio = (x - left) / (right - left);
            Value = (int)Math.Round(Math.Max(0, Math.Min(1, ratio)) * 100);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            Color accent = _muted ? Theme.Dim : Theme.TimeColor(TimeSlot.Dusk);
            float cy = Height / 2f;
            using (SolidBrush brush = new SolidBrush(_muted ? Theme.Dim : Theme.Muted))
            {
                e.Graphics.FillRectangle(brush, 2, cy - 4, 5, 8);
                PointF[] cone =
                {
                    new PointF(7, cy - 4), new PointF(14, cy - 9),
                    new PointF(14, cy + 9), new PointF(7, cy + 4)
                };
                e.Graphics.FillPolygon(brush, cone);
            }
            if (_muted)
            {
                using (Pen pen = new Pen(Theme.Red, 1.6f))
                    e.Graphics.DrawLine(pen, 2, cy + 7, 18, cy - 8);
            }
            else
            {
                using (Pen pen = new Pen(Theme.Alpha(accent, 200), 1.5f))
                {
                    e.Graphics.DrawArc(pen, 12, cy - 6, 10, 12, -55, 110);
                    if (_value > 55) e.Graphics.DrawArc(pen, 14, cy - 10, 15, 20, -50, 100);
                }
            }

            float left = 29;
            float right = Math.Max(left + 10, Width - 10);
            float knobX = left + (right - left) * _value / 100f;
            using (SolidBrush track = new SolidBrush(Color.FromArgb(37, 46, 59)))
                Theme.FillRounded(e.Graphics, track, new RectangleF(left, cy - 3, right - left, 6), 3);
            using (SolidBrush fill = new SolidBrush(Theme.Alpha(accent, _muted ? 80 : 235)))
                Theme.FillRounded(e.Graphics, fill, new RectangleF(left, cy - 3, Math.Max(4, knobX - left), 6), 3);
            float r = _hover || _dragging ? 7 : 6;
            using (SolidBrush knob = new SolidBrush(_muted ? Theme.Dim : Theme.Text))
                e.Graphics.FillEllipse(knob, knobX - r, cy - r, r * 2, r * 2);
        }
    }

    public class TimeBars : Control
    {
        private WallpaperItem _item;

        public TimeBars()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public WallpaperItem Wallpaper
        {
            get { return _item; }
            set { _item = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            if (_item == null) return;
            double max = 0.01;
            foreach (TimeSlot slot in TimeSlotInfo.All)
            {
                double score = _item.VisualTimeScore(slot);
                if (score > max) max = score;
            }
            float gap = 20;
            float colW = (Width - gap * 3) / 4f;
            for (int i = 0; i < 4; i++)
            {
                TimeSlot slot = TimeSlotInfo.All[i];
                float x = i * (colW + gap);
                double score = _item.VisualTimeScore(slot);
                float ratio = (float)Math.Max(0.03, Math.Min(1, score / max));
                bool top = slot == _item.AutoPrimaryTime;
                Color color = Theme.TimeColor(slot);
                using (SolidBrush brush = new SolidBrush(top ? Theme.Text : Theme.Muted))
                    e.Graphics.DrawString(TimeSlotInfo.Name(slot), Theme.FontTiny, brush, x, 0);
                RectangleF track = new RectangleF(x, 19, colW - 30, 5);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(32, 40, 52)))
                    Theme.FillRounded(e.Graphics, brush, track, 2.5f);
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(color, top ? 240 : 155)))
                    Theme.FillRounded(e.Graphics, brush, new RectangleF(x, 19,
                        Math.Max(3, (colW - 30) * ratio), 5), 2.5f);
                using (SolidBrush brush = new SolidBrush(top ? color : Theme.Dim))
                    e.Graphics.DrawString(score.ToString("0.0"), Theme.FontTiny, brush, x + colW - 28, 0);
                Theme.DrawText(e.Graphics, TimeSlotInfo.Range(slot), Theme.FontTiny, Theme.Dim, x, 30);
            }
        }
    }

    public class ScoreBars : Control
    {
        private WallpaperItem _item;
        private bool _compact;
        private int _columns = 2;

        public ScoreBars()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public WallpaperItem Wallpaper
        {
            get { return _item; }
            set { _item = value; Invalidate(); }
        }

        public bool Compact
        {
            get { return _compact; }
            set { _compact = value; Invalidate(); }
        }

        public int Columns
        {
            get { return _columns; }
            set
            {
                _columns = Math.Max(1, Math.Min(4, value));
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            if (_item == null) return;
            double max = 0.01;
            foreach (WeatherKind kind in WeatherKindInfo.All)
            {
                double score = _item.VisualScore(kind);
                if (score > max) max = score;
            }
            int count = 8;
            int cols = _compact ? 2 : _columns;
            int rows = (count + cols - 1) / cols;
            float colGap = 20;
            float colW = (Width - colGap * (cols - 1)) / (float)cols;
            float rowH = Math.Max(24, (Height - 6) / (float)rows);
            for (int i = 0; i < count; i++)
            {
                WeatherKind kind = WeatherKindInfo.All[i];
                int col = i % cols;
                int row = i / cols;
                float x = col * (colW + colGap);
                float y = row * rowH + 2;
                double score = _item.VisualScore(kind);
                float ratio = (float)Math.Max(0.03, Math.Min(1, score / max));
                bool top = kind == _item.AutoPrimaryWeather;
                Color color = Theme.Accent(kind);
                using (SolidBrush brush = new SolidBrush(top ? Theme.Text : Theme.Muted))
                    e.Graphics.DrawString(WeatherKindInfo.Name(kind), Theme.FontTiny, brush, x, y);
                float barX = x + 42;
                float barW = colW - 72;
                RectangleF track = new RectangleF(barX, y + 5, barW, 5);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(32, 40, 52)))
                    Theme.FillRounded(e.Graphics, brush, track, 2.5f);
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(color, top ? 240 : 150)))
                    Theme.FillRounded(e.Graphics, brush, new RectangleF(barX, y + 5, Math.Max(3, barW * ratio), 5), 2.5f);
                using (SolidBrush brush = new SolidBrush(top ? color : Theme.Dim))
                {
                    string value = score.ToString("0.0");
                    SizeF size = e.Graphics.MeasureString(value, Theme.FontTiny);
                    e.Graphics.DrawString(value, Theme.FontTiny, brush, x + colW - size.Width - 2, y);
                }
            }
        }
    }

    public class StartupOverlay : Control
    {
        public StartupOverlay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Dock = DockStyle.Fill;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Setup(e.Graphics);
            using (SolidBrush bg = new SolidBrush(Theme.Bg))
                e.Graphics.FillRectangle(bg, ClientRectangle);
            float cx = Width / 2f;
            float cy = Height / 2f;
            string title = "SkyMatch";
            SizeF ts = e.Graphics.MeasureString(title, Theme.FontTitle);
            Theme.DrawText(e.Graphics, title, Theme.FontTitle, Theme.Text, cx - ts.Width / 2f, cy - 58);
            using (SolidBrush accent = new SolidBrush(Theme.Yellow))
                Theme.FillRounded(e.Graphics, accent, new RectangleF(cx - 26, cy + 2, 52, 3), 1.5f);
            string hint = "正在加载壁纸与天气…";
            SizeF hs = e.Graphics.MeasureString(hint, Theme.FontSmall);
            Theme.DrawText(e.Graphics, hint, Theme.FontSmall, Theme.Muted, cx - hs.Width / 2f, cy + 22);
            base.OnPaint(e);
        }
    }
}
