using System;
using Eto.Drawing;
using Eto.Forms;

namespace DB3DFloora
{
    /// <summary>已選材質圖片的縮圖，右上角有個別的 x 取消鈕（對照 Python 版 TextureThumb）。</summary>
    public class TextureThumb : Drawable
    {
        public const int ThumbSize = 44;
        private const int Badge = 14;

        public string Path { get; }
        private readonly Action<string> _onRemove;
        private Bitmap _bitmap;

        public TextureThumb(string path, Action<string> onRemove)
        {
            Path = path;
            _onRemove = onRemove;
            Size = new Size(ThumbSize, ThumbSize);
            try { _bitmap = new Bitmap(path); } catch { _bitmap = null; }
            Paint += OnPaint;
            MouseDown += OnMouseDown;
            try { Cursor = Cursors.Pointer; } catch { }
        }

        private RectangleF BadgeRect() => new RectangleF(ThumbSize - Badge, 0, Badge, Badge);

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.AntiAlias = true;
            g.Clear(UiStyle.CBg);
            var inner = new RectangleF(1, 1, ThumbSize - 2, ThumbSize - 2);
            if (_bitmap != null)
            {
                try { g.DrawImage(_bitmap, inner); }
                catch { g.FillRectangle(new SolidBrush(UiStyle.CPanel), inner); }
            }
            else
            {
                g.FillRectangle(new SolidBrush(UiStyle.CPanel), inner);
            }
            g.DrawRectangle(new Pen(UiStyle.CBorder, 1.0f), 1, 1, ThumbSize - 2, ThumbSize - 2);

            var badge = BadgeRect();
            g.FillEllipse(new SolidBrush(Color.FromArgb(0xB0, 0x30, 0x30)), badge);
            var font = UiStyle.F("subhead");
            var sz = g.MeasureString(font, "x");
            float tx = badge.X + (badge.Width - sz.Width) / 2.0f;
            float ty = badge.Y + (badge.Height - sz.Height) / 2.0f;
            g.DrawText(font, new SolidBrush(Colors.White), tx, ty, "x");
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (BadgeRect().Contains(e.Location))
                _onRemove(Path);
        }

        /// <summary>空的縮圖佔位格：跟 TextureThumb 一樣大小，維持縮圖區固定高度，
        /// 不管選幾張圖都不會變長變短。</summary>
        public static Drawable EmptySlot()
        {
            var d = new Drawable { Size = new Size(ThumbSize, ThumbSize) };
            d.Paint += (sender, e) =>
            {
                var g = e.Graphics;
                g.AntiAlias = true;
                g.Clear(UiStyle.CBg);
                var inner = new RectangleF(1, 1, ThumbSize - 2, ThumbSize - 2);
                g.FillRectangle(new SolidBrush(UiStyle.CPanel), inner);
                g.DrawRectangle(new Pen(UiStyle.CBorder, 1.0f), 1, 1, ThumbSize - 2, ThumbSize - 2);
            };
            return d;
        }
    }
}
