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

        private static (string Subtitle, string[] Lines)[] UsageGroups => new[]
        {
            (Strings.T("info.usage.1.title"), new[]
            {
                Strings.T("info.usage.1.a"),
                Strings.T("info.usage.1.b"),
                Strings.T("info.usage.1.c"),
            }),
            (Strings.T("info.usage.2.title"), new[]
            {
                Strings.T("info.usage.2.a"),
                Strings.T("info.usage.2.b", MaxTextures),
            }),
            (Strings.T("info.usage.3.title"), new[]
            {
                Strings.T("info.usage.3.a"),
            }),
            (Strings.T("info.usage.4.title"), new[]
            {
                Strings.T("info.usage.4.a"),
            }),
            (Strings.T("info.usage.5.title"), new[]
            {
                Strings.T("info.usage.5.a"),
                Strings.T("info.usage.5.b"),
                Strings.T("info.usage.5.c"),
            }),
            (Strings.T("info.usage.6.title"), new[]
            {
                Strings.T("info.usage.6.a"),
                Strings.T("info.usage.6.b"),
            }),
            (Strings.T("info.usage.7.title"), new[]
            {
                Strings.T("info.usage.7.a"),
            }),
        };

        public InfoForm()
        {
            Title = Strings.T("info.title");
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

            var title = UiStyle.MakeLabel(Strings.T("info.appName"), titleFont, UiStyle.CAccent, TextAlignment.Left);
            UiStyle.AddRow(outer, title);

            string versionText = UiStyle.PluginVersionText();
            if (!string.IsNullOrEmpty(versionText))
                UiStyle.AddRow(outer, UiStyle.MakeLabel(versionText, null, UiStyle.CTextSub, TextAlignment.Left));

            UiStyle.AddRow(outer, UiStyle.Hr());

            UiStyle.AddRow(outer, UiStyle.SectionLabel(Strings.T("info.usage"), sectionFont, UiStyle.CAccent));
            foreach (var (subtitle, lines) in UsageGroups)
            {
                UiStyle.AddRow(outer, UiStyle.MakeLabel(subtitle, subheadFont, UiStyle.CText, TextAlignment.Left));
                foreach (var line in lines)
                    UiStyle.AddRow(outer, BodyLabel("‧ " + line));
            }

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.SectionLabel(Strings.T("info.authors"), sectionFont, UiStyle.CAccent));
            UiStyle.AddRow(outer, UiStyle.MakeLabel(Strings.T("info.originalPlugin"), null, null, TextAlignment.Left));
            UiStyle.AddRow(outer, UiStyle.MakeLabel(Strings.T("info.madeBy"), null, UiStyle.CTextSub, TextAlignment.Left));
            outer.Items.Add(new StackLayoutItem(LinkRow(Strings.T("info.website"), "https://www.db3drender.com/")));
            outer.Items.Add(new StackLayoutItem(LinkRow(Strings.T("info.instagram"), "https://www.instagram.com/db3d.render/")));

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.MakeLabel(Strings.T("info.rhinoPort"), null, UiStyle.CAccent, TextAlignment.Left));
            outer.Items.Add(new StackLayoutItem(LinkRow(Strings.T("info.github"), "https://github.com/ZionW/db3d-floora-rhino")));

            Content = outer;
        }
    }
}
