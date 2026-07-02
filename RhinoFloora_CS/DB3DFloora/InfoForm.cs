using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace DB3DFloora
{
    /// <summary>外掛說明：使用方式＋作者介紹，全部內容靠左對齊（對照 Python 版 InfoForm）。
    /// 注意：介面功能有變動時，下面 UsageGroups 的內容也要跟著同步更新。</summary>
    public class InfoForm : Form
    {
        private const int MaxTextures = 5;

        private static readonly (string Subtitle, string[] Lines)[] UsageGroups =
        {
            ("1. 選圖案與尺寸", new[]
            {
                "從「圖案」縮圖選擇鋪貼樣式",
                "在「尺寸與縫隙」設定長寬、縫寬、縫深",
                "部分圖案還會有錯縫%／斜紋角度／編織數、旋轉角度、起始點",
            }),
            ("2. 設定材質", new[]
            {
                "上色模式：目前材質／自訂顏色／貼圖材質",
                $"貼圖材質最多可選 {MaxTextures} 張圖片，縮圖右上角 x 可個別移除",
            }),
            ("3. 紋理（貼圖材質模式）", new[]
            {
                "對齊邊／隨機位置／隨機旋轉",
            }),
            ("4. 效果", new[]
            {
                "倒斜角、隨機缺陷、背面、獨立群組",
            }),
            ("5. 選面與預覽", new[]
            {
                "按「選擇物件」選面，移動滑鼠即時預覽鋪磚位置（純畫面顯示，不會產生真的線段）",
                "左鍵點擊決定起磚點，按 Esc／右鍵取消不留下任何東西",
                "起磚點確定後仍可持續調整參數，預覽會即時更新",
            }),
            ("6. 產生與管理", new[]
            {
                "按「產生磚塊」確認並生成正式磚片",
                "「復原」可撤銷最近 3 次操作；「重設」回到預設值",
            }),
            ("7. 材料計算機", new[]
            {
                "統計已產生磚片的面積、估算用量，並可匯出 CSV／圖片",
            }),
        };

        public InfoForm()
        {
            Title = "外掛說明";
            Topmost = true;
            Resizable = true;
            BackgroundColor = UiStyle.CBg;
            Padding = new Padding(8);
            try
            {
                var mainWindow = UiStyle.RhinoMainWindow();
                if (mainWindow != null)
                    Owner = mainWindow;
            }
            catch { }
            Build();
        }

        private StackLayout LinkRow(string labelText, string url)
        {
            var lbl = UiStyle.MakeLabel(labelText, null, UiStyle.CTextSub, TextAlignment.Left);
            try
            {
                var link = new LinkButton { Text = url };
                link.Click += (s, e) => UiStyle.OpenUrl(url);
                return UiStyle.HRow(lbl, link, null);
            }
            catch
            {
                return UiStyle.HRow(lbl, UiStyle.MakeLabel(url, null, null, TextAlignment.Left), null);
            }
        }

        private Label BodyLabel(string text)
        {
            var lbl = UiStyle.MakeLabel(text, null, null, TextAlignment.Left);
            try
            {
                lbl.Wrap = WrapMode.Word;
                lbl.Width = 320;
            }
            catch { }
            return lbl;
        }

        private void Build()
        {
            var outer = UiStyle.VStack();
            var sectionFont = UiStyle.F("section");
            var titleFont = UiStyle.F("title");
            var subheadFont = UiStyle.F("subhead");

            var title = UiStyle.MakeLabel("DB3D Floora for Rhino", titleFont, UiStyle.CAccent, TextAlignment.Left);
            UiStyle.AddRow(outer, title);
            UiStyle.AddRow(outer, UiStyle.Hr());

            UiStyle.AddRow(outer, UiStyle.SectionLabel("使用方式", sectionFont, UiStyle.CAccent));
            foreach (var (subtitle, lines) in UsageGroups)
            {
                UiStyle.AddRow(outer, UiStyle.MakeLabel(subtitle, subheadFont, UiStyle.CText, TextAlignment.Left));
                foreach (var line in lines)
                    UiStyle.AddRow(outer, BodyLabel("‧ " + line));
            }

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.SectionLabel("作者介紹", sectionFont, UiStyle.CAccent));
            UiStyle.AddRow(outer, UiStyle.MakeLabel("原始外掛：DB3D-Floora（SketchUp 版）", null, null, TextAlignment.Left));
            UiStyle.AddRow(outer, UiStyle.MakeLabel("製作：DB3D.RENDER", null, UiStyle.CTextSub, TextAlignment.Left));
            outer.Items.Add(new StackLayoutItem(LinkRow("官網：", "https://www.db3drender.com/")));
            outer.Items.Add(new StackLayoutItem(LinkRow("Instagram：", "https://www.instagram.com/db3d.render/")));

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.MakeLabel("Rhino 版製作：Onon.Nihow", null, UiStyle.CAccent, TextAlignment.Left));

            Content = outer;
        }
    }
}
