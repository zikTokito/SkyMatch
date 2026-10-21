using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SkyMatch
{
    public class BlockedWallpapersDialog : Form
    {
        private readonly AppController _controller;
        private readonly FlowLayoutPanel _grid;
        private readonly FadeTip _tip;
        private readonly Label _empty;
        private readonly List<WorkshopGridCard> _cards;
        private readonly ActionButton _close;

        public BlockedWallpapersDialog(AppController controller)
        {
            _controller = controller;
            _cards = new List<WorkshopGridCard>();
            Text = "屏蔽列表";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.FontBody;
            ClientSize = new Size(940, 680);

            Label title = new Label();
            title.Text = "已屏蔽的工坊壁纸";
            title.Font = new Font(Theme.FontTitle.FontFamily, 16f, FontStyle.Bold);
            title.ForeColor = Theme.Text;
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "点击卡片上的「取消屏蔽」恢复显示。";
            subtitle.Font = Theme.FontSmall;
            subtitle.ForeColor = Theme.Muted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(22, 49);
            Controls.Add(subtitle);

            _grid = new FlowLayoutPanel();
            _grid.Location = new Point(12, 76);
            _grid.Size = new Size(916, 540);
            _grid.AutoScroll = true;
            _grid.WrapContents = true;
            _grid.FlowDirection = FlowDirection.LeftToRight;
            _grid.BackColor = Theme.Bg;
            _grid.Padding = new Padding(8, 4, 8, 8);
            Controls.Add(_grid);

            _tip = new FadeTip();
            Controls.Add(_tip);
            _tip.BringToFront();

            _empty = new Label();
            _empty.AutoSize = false;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = Theme.Dim;
            _empty.Font = Theme.FontMedium;
            _empty.BackColor = Color.Transparent;
            _empty.Text = "还没有屏蔽任何壁纸。";
            _empty.Visible = false;
            Controls.Add(_empty);
            _empty.BringToFront();

            _close = new ActionButton();
            _close.Text = "关闭";
            _close.Pill = true;
            _close.Size = new Size(82, 36);
            _close.Location = new Point(838, 630);
            _close.Click += delegate { Close(); };
            Controls.Add(_close);

            RefreshCards();
        }

        private void RefreshCards()
        {
            foreach (WorkshopGridCard card in _cards) card.Dispose();
            _cards.Clear();
            _grid.Controls.Clear();
            _tip.HideTip();

            List<WallpaperItem> items = _controller.BlockedWallpapers;
            foreach (WallpaperItem item in items)
            {
                WorkshopGridCard card = new WorkshopGridCard(item, false);
                card.BlockLabel = "取消屏蔽";
                card.Size = new Size(282, 218);
                card.Margin = new Padding(0, 0, 10, 10);
                card.BlockRequested += delegate(WallpaperItem blocked)
                {
                    _controller.UnblockWallpaper(blocked.Id);
                    RefreshCards();
                };
                card.BlockHovered += delegate
                {
                    Rectangle anchor = card.RectangleToScreen(card.BlockBounds);
                    _tip.ShowBelow("取消后再次抓取就会显示在工坊中", this, anchor);
                };
                card.BlockUnhovered += delegate { _tip.HideTip(); };
                _cards.Add(card);
                _grid.Controls.Add(card);
            }

            bool empty = items.Count == 0;
            _empty.Visible = empty;
            _empty.SetBounds(20, 260, ClientSize.Width - 40, 80);
            if (empty) _empty.BringToFront();
        }
    }
}
