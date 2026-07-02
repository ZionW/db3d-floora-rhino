using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace DB3DFloora
{
    /// <summary>設計代幣（日本傳統色命名色票）＋共用小工具（對照 Python 版 ui.py 檔案開頭與模組層級函式）。
    /// 版面用「外層垂直 StackLayout ＋ 每列各自一個水平 StackLayout」組成，不用 DynamicLayout
    /// （不同列欄位數不一致時，DynamicLayout 的共用欄寬機制會讓視窗寬度暴增）。</summary>
    public static class UiStyle
    {
        public static readonly Color TokenShironeri = Color.FromArgb(0xFC, 0xFC, 0xFA);
        public static readonly Color TokenUsuzumi = Color.FromArgb(0xF4, 0xF5, 0xF6);
        public static readonly Color TokenHaizakura = Color.FromArgb(0xE4, 0xE7, 0xEB);
        public static readonly Color TokenHanada = Color.FromArgb(0x2A, 0x5F, 0x8A);
        public static readonly Color TokenAsagi = Color.FromArgb(0xDC, 0xEA, 0xF3);
        public static readonly Color TokenSumi = Color.FromArgb(0x33, 0x38, 0x3F);
        public static readonly Color TokenNezumi = Color.FromArgb(0x8B, 0x94, 0xA0);

        public static readonly Color CBg = TokenShironeri;
        public static readonly Color CPanel = TokenUsuzumi;
        public static readonly Color CBorder = TokenHaizakura;
        public static readonly Color CAccent = TokenHanada;
        public static readonly Color CAccentLight = TokenAsagi;
        public static readonly Color CAccentHover = Color.FromArgb(0x6F, 0x9C, 0xC2);
        public static readonly Color CText = TokenSumi;
        public static readonly Color CTextSub = TokenNezumi;
        public static readonly Color CIconFill = Color.FromArgb(0xA6, 0xC2, 0xD8);
        public static readonly Color CIconFillSel = TokenHanada;
        public static readonly Color CIconStroke = Color.FromArgb(0x78, 0x96, 0xB4);
        public static readonly Color CIconStrokeSel = Color.FromArgb(0x15, 0x38, 0x60);

        public static readonly Color DefaultTileColor = Color.FromArgb(0xD3, 0xD3, 0xD3);

        private static readonly Dictionary<string, (int Size, bool Bold)> FontSpecs =
            new Dictionary<string, (int, bool)>
            {
                {"title", (11, true)},
                {"section", (9, true)},
                {"subhead", (8, true)},
                {"body", (8, false)},
                {"caption", (7, false)},
            };

        public const int FieldLabelWidth = 70;

        public static Font F(string key)
        {
            var (size, bold) = FontSpecs[key];
            var style = bold ? FontStyle.Bold : FontStyle.None;
            return new Font(FontFamilies.Sans, size, style);
        }

        public static Label MakeLabel(string text, Font font = null, Color? color = null, TextAlignment? align = null)
        {
            var lbl = new Label { Text = text };
            if (font != null)
                lbl.Font = font;
            if (color.HasValue)
                lbl.TextColor = color.Value;
            if (align.HasValue)
                lbl.TextAlignment = align.Value;
            return lbl;
        }

        public static Label SectionLabel(string text, Font font, Color color)
        {
            return MakeLabel(text, font, color, TextAlignment.Left);
        }

        public static Button MakeButton(string text, bool primary = false)
        {
            var btn = new Button { Text = text };
            if (primary)
            {
                try
                {
                    btn.BackgroundColor = CAccent;
                    btn.TextColor = Colors.White;
                }
                catch
                {
                    // 部分平台不支援按鈕底色，忽略即可
                }
            }
            return btn;
        }

        public static Panel Hr()
        {
            return new Panel { Height = 1, BackgroundColor = CBorder };
        }

        public static StackLayout VStack()
        {
            return new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 3,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
        }

        /// <summary>水平排一列控制項；傳 null 會變成可伸縮的空白區塊（讓其餘控制項靠左對齊，不被拉伸）。</summary>
        public static StackLayout HRow(params Control[] controls)
        {
            var s = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 };
            foreach (var c in controls)
            {
                if (c == null)
                    s.Items.Add(new StackLayoutItem(new Panel(), true));
                else
                    s.Items.Add(new StackLayoutItem(c));
            }
            return s;
        }

        /// <summary>注意：這裡故意不傳 expand=true。StackLayoutItem 的 expand 是沿著主軸（垂直）方向，
        /// 若視窗有多餘空間，expand=true 的每一列都會被拉伸（連 1px 的分隔線、按鈕都會被拉高）。
        /// 維持預設（不 expand）才會保持原本高度。</summary>
        public static void AddRow(StackLayout outer, Control control)
        {
            outer.Items.Add(new StackLayoutItem(control));
        }

        /// <summary>取得 Rhino 主視窗（Eto Form）。優先取得目前文件對應的主視窗（較可靠），
        /// 拿不到時退回全域主視窗；任何環境不支援時安靜地回傳 null，不影響外掛其他功能。</summary>
        public static Window RhinoMainWindow()
        {
            try
            {
                var doc = Rhino.RhinoDoc.ActiveDoc;
                if (doc != null)
                {
                    var win = Rhino.UI.RhinoEtoApp.MainWindowForDocument(doc);
                    if (win != null)
                        return win;
                }
            }
            catch
            {
                // 忽略，改用全域主視窗
            }
            try
            {
                return Rhino.UI.RhinoEtoApp.MainWindow;
            }
            catch
            {
                return null;
            }
        }

        public static System.Drawing.Color ToSysColor(Color etoColor)
        {
            return System.Drawing.Color.FromArgb(etoColor.Rb, etoColor.Gb, etoColor.Bb);
        }

        public static void OpenUrl(string url)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
            }
            catch
            {
                // 開啟瀏覽器失敗不影響外掛其他功能
            }
        }
    }
}
