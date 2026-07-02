using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;

namespace DB3DFloora
{
    /// <summary>材料計算機（對照 Python 版 CalculatorForm）：圖層統計＋磚片估算，可匯出 CSV／圖片。</summary>
    public class CalculatorForm : Form
    {
        private List<LayerStat> _stats;
        private EstimateResult _lastEstimate;
        private double _totalArea;

        private NumericStepper _lenBox;
        private NumericStepper _widBox;
        private NumericStepper _wasteBox;
        private NumericStepper _pcsBox;
        private NumericStepper _priceBoxBox;
        private NumericStepper _pricePingBox;
        private Label _resultLabel;

        public CalculatorForm(List<LayerStat> stats)
        {
            Title = "材料計算機";
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
            Build(stats);
        }

        private void Build(List<LayerStat> stats)
        {
            var outer = UiStyle.VStack();
            var titleFont = UiStyle.F("title");
            var sectionFont = UiStyle.F("section");

            var title = UiStyle.MakeLabel("材料計算機", titleFont, UiStyle.CAccent, TextAlignment.Left);
            UiStyle.AddRow(outer, title);
            UiStyle.AddRow(outer, UiStyle.Hr());

            double totalArea = stats.Sum(s => s.AreaM2);
            double totalPing = totalArea / CalculatorUtil.M2PerPing;
            _totalArea = totalArea;

            UiStyle.AddRow(outer, UiStyle.SectionLabel("圖層統計", sectionFont, UiStyle.CAccent));
            if (stats.Count == 0)
            {
                UiStyle.AddRow(outer, UiStyle.MakeLabel("目前模型中沒有 Floora 產生的磚片圖層", null, UiStyle.CTextSub, TextAlignment.Left));
            }
            foreach (var s in stats)
            {
                UiStyle.AddRow(outer, UiStyle.MakeLabel(
                    $"{s.Name}：{s.Count} 片，{s.AreaM2:F2} m²（{s.AreaPing:F2} 坪）", null, null, TextAlignment.Left));
            }
            UiStyle.AddRow(outer, UiStyle.MakeLabel(
                $"總計：{totalArea:F2} m²（{totalPing:F2} 坪）", null, UiStyle.CAccent, TextAlignment.Left));

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.SectionLabel("磚片估算", sectionFont, UiStyle.CAccent));

            NumericStepper NumField(double defaultVal)
            {
                return new NumericStepper { DecimalPlaces = 2, MinValue = 0, MaxValue = 1000000, Value = defaultVal, Width = 100 };
            }

            _lenBox = NumField(60.0);
            _widBox = NumField(60.0);
            _wasteBox = NumField(8.0);
            _pcsBox = NumField(4.0);
            _priceBoxBox = NumField(0.0);
            _pricePingBox = NumField(0.0);

            Label FieldLabel(string text)
            {
                var lbl = UiStyle.MakeLabel(text);
                lbl.Width = UiStyle.FieldLabelWidth;
                return lbl;
            }

            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel("磚片長 (cm)"), _lenBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel("磚片寬 (cm)"), _widBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel("耗損率 (%)"), _wasteBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel("每箱片數"), _pcsBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel("每箱價格"), _priceBoxBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel("每坪價格"), _pricePingBox, null)));

            _resultLabel = UiStyle.MakeLabel("");
            var btn = UiStyle.MakeButton("計算", true);
            btn.Click += (s, e) => Recalc(totalArea);
            UiStyle.AddRow(outer, btn);
            UiStyle.AddRow(outer, _resultLabel);

            UiStyle.AddRow(outer, UiStyle.Hr());
            var btnCsv = UiStyle.MakeButton("匯出 CSV");
            btnCsv.Click += OnExportCsv;
            var btnImg = UiStyle.MakeButton("匯出圖片");
            btnImg.Click += OnExportImage;
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(btnCsv, btnImg)));

            Content = outer;
            _stats = stats;
            _lastEstimate = null;
            Recalc(totalArea);
        }

        private void Recalc(double totalArea)
        {
            var r = CalculatorUtil.Estimate(
                totalArea, _lenBox.Value, _widBox.Value, _wasteBox.Value,
                _pcsBox.Value, _priceBoxBox.Value, _pricePingBox.Value);
            _lastEstimate = r;
            if (r == null)
            {
                _resultLabel.Text = "請輸入有效的磚片尺寸";
                return;
            }
            _resultLabel.Text =
                $"含耗損面積：{r.NeededAreaM2:F2} m²\n需求片數：{r.TileCount} 片\n需求箱數：{r.Boxes} 箱\n" +
                $"估價（依箱）：{r.PriceByBox:F0}\n估價（依坪）：{r.PriceByPing:F0}";
        }

        private void OnExportCsv(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog { Title = "匯出 CSV", FileName = "floora_計算結果.csv" };
            var f = new FileFilter("CSV 檔案", new[] { ".csv" });
            dlg.Filters.Add(f);
            dlg.CurrentFilter = f;
            var result = dlg.ShowDialog(this);
            if (result != DialogResult.Ok)
                return;
            string path = dlg.FileName;
            if (!path.ToLower().EndsWith(".csv"))
                path += ".csv";
            try
            {
                CalculatorUtil.WriteCsv(path, _stats, _lastEstimate);
                MessageBox.Show(this, $"已匯出：\n{path}", "DB3D-Floora");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"匯出失敗：{ex.Message}", "DB3D-Floora");
            }
        }

        private void OnExportImage(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog { Title = "匯出圖片", FileName = "floora_計算結果.png" };
            var f = new FileFilter("PNG 圖片", new[] { ".png" });
            dlg.Filters.Add(f);
            dlg.CurrentFilter = f;
            var result = dlg.ShowDialog(this);
            if (result != DialogResult.Ok)
                return;
            string path = dlg.FileName;
            if (!path.ToLower().EndsWith(".png"))
                path += ".png";
            try
            {
                RenderResultImage(path);
                MessageBox.Show(this, $"已匯出：\n{path}", "DB3D-Floora");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"匯出失敗：{ex.Message}", "DB3D-Floora");
            }
        }

        private void RenderResultImage(string path)
        {
            var titleFont = new Font(FontFamilies.Sans, 13, FontStyle.Bold);
            var bodyFont = new Font(FontFamilies.Sans, 11);

            var lines = new List<string> { "DB3D Floora 材質計算結果" };
            foreach (var s in _stats)
                lines.Add($"{s.Name}：{s.Count} 片，{s.AreaM2:F2} m²（{s.AreaPing:F2} 坪）");
            double totalArea = _stats.Sum(s => s.AreaM2);
            double totalPing = totalArea / CalculatorUtil.M2PerPing;
            lines.Add($"總計：{totalArea:F2} m²（{totalPing:F2} 坪）");
            if (_lastEstimate != null)
            {
                var r = _lastEstimate;
                lines.Add("");
                lines.Add($"含耗損面積：{r.NeededAreaM2:F2} m²");
                lines.Add($"需求片數：{r.TileCount} 片");
                lines.Add($"需求箱數：{r.Boxes} 箱");
                lines.Add($"估價（依箱）：{r.PriceByBox:F0}");
                lines.Add($"估價（依坪）：{r.PriceByPing:F0}");
            }

            int width = 480, lineH = 24, topH = 50, pad = 20;
            int height = topH + pad * 2 + lineH * lines.Count;
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppRgba))
            {
                using (var g = new Graphics(bmp))
                {
                    g.AntiAlias = true;
                    g.Clear(UiStyle.CBg);
                    g.DrawText(titleFont, new SolidBrush(UiStyle.CAccent), pad, pad, lines[0]);
                    float y = pad + topH;
                    var bodyBrush = new SolidBrush(UiStyle.CText);
                    for (int i = 1; i < lines.Count; i++)
                    {
                        g.DrawText(bodyFont, bodyBrush, pad, y, lines[i]);
                        y += lineH;
                    }
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
