using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace DB3DFloora
{
    /// <summary>單一圖案的縮圖選取按鈕：用實際拼貼演算法畫出縮圖，點擊即選取該圖案（對照 Python 版 PatternTile）。</summary>
    public class PatternTile : Drawable
    {
        public const int TileW = 48;
        public const int TileH = 54;
        public const int IconArea = 26;

        public string PatternId { get; }
        public string LabelText { get; }
        public bool Selected { get; private set; }
        public bool Hovered { get; private set; }

        private readonly Action<string> _onSelect;
        private readonly List<List<(double U, double V)>> _polys;

        public PatternTile(string patternId, string labelText, Action<string> onSelect)
        {
            PatternId = patternId;
            LabelText = labelText;
            _onSelect = onSelect;
            Size = new Size(TileW, TileH);
            _polys = IconsUtil.SamplePolygons(patternId);
            Paint += OnPaint;
            MouseUp += OnMouseUp;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            try { Cursor = Cursors.Pointer; } catch { }
        }

        public void SetSelected(bool val)
        {
            if (Selected != val)
            {
                Selected = val;
                Invalidate();
            }
        }

        // _onSelect 最終會觸發 Rhino 互動選取（阻塞的 GetObject）。用 MouseUp（點擊放開的那一刻）
        // 而不是 MouseDown 觸發：MouseDown 當下 OS 的滑鼠按下/放開手勢還沒跑完，這時候就切進阻塞
        // 迴圈，在 Windows 上這個時間點有時候拿不到，導致「看起來沒反應」甚至完全叫不出選取流程；
        // 等 MouseUp（整個點擊手勢確定已經結束）再觸發，跨平台行為才會一致。之前試過改用
        // AsyncInvoke 延後執行，但那個時機依賴各平台訊息佇列的實作細節，Windows 上不夠可靠，
        // 改成直接用 MouseUp 這個原生事件本身來說明「點擊已經完成」比較穩妥。
        private void OnMouseUp(object sender, MouseEventArgs e) => _onSelect(PatternId);
        private void OnMouseEnter(object sender, MouseEventArgs e) { Hovered = true; Invalidate(); }
        private void OnMouseLeave(object sender, MouseEventArgs e) { Hovered = false; Invalidate(); }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.AntiAlias = true;
            var bg = Selected ? UiStyle.CAccentLight : UiStyle.CPanel;
            Color border;
            float borderW;
            if (Selected) { border = UiStyle.CAccent; borderW = 1.4f; }
            else if (Hovered) { border = UiStyle.CAccentHover; borderW = 1.2f; }
            else { border = UiStyle.CBorder; borderW = 1.0f; }

            g.Clear(UiStyle.CBg);
            g.FillRectangle(new SolidBrush(bg), 1, 1, TileW - 2, TileH - 2);
            g.DrawRectangle(new Pen(border, borderW), 1, 1, TileW - 2, TileH - 2);

            var iconFill = Selected ? UiStyle.CIconFillSel : UiStyle.CIconFill;
            var iconStroke = Selected ? UiStyle.CIconStrokeSel : UiStyle.CIconStroke;
            double half = IconArea / 2.0;
            double cx = TileW / 2.0;
            double cy = 4 + half;
            double view = IconsUtil.View;

            try { g.SetClip(new RectangleF(2, 2, TileW - 4, IconArea + 6)); } catch { }

            var brush = new SolidBrush(iconFill);
            var pen = new Pen(iconStroke, 0.8f);
            foreach (var poly in _polys)
            {
                if (poly.Count < 3)
                    continue;
                var pts = new PointF[poly.Count];
                for (int i = 0; i < poly.Count; i++)
                {
                    var (x, y) = poly[i];
                    pts[i] = new PointF((float)(cx + (x / view) * half), (float)(cy - (y / view) * half));
                }
                g.FillPolygon(brush, pts);
                g.DrawPolygon(pen, pts);
            }

            try { g.ResetClip(); } catch { }

            var font = UiStyle.F("caption");
            var textColor = Selected ? UiStyle.CAccent : UiStyle.CText;
            var tb = new SolidBrush(textColor);
            var sz = g.MeasureString(font, LabelText);
            float tx = (TileW - sz.Width) / 2.0f;
            float ty = TileH - sz.Height - 3;
            g.DrawText(font, tb, tx, ty, LabelText);
        }
    }
}
