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
            Title = Strings.T("calc.title");
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

            var title = UiStyle.MakeLabel(Strings.T("calc.title"), titleFont, UiStyle.CAccent, TextAlignment.Left);
            UiStyle.AddRow(outer, title);
            UiStyle.AddRow(outer, UiStyle.Hr());

            double totalArea = stats.Sum(s => s.AreaM2);
            double totalPing = totalArea / CalculatorUtil.M2PerPing;
            _totalArea = totalArea;

            UiStyle.AddRow(outer, UiStyle.SectionLabel(Strings.T("calc.layerStats"), sectionFont, UiStyle.CAccent));
            if (stats.Count == 0)
            {
                UiStyle.AddRow(outer, UiStyle.MakeLabel(Strings.T("calc.noLayers"), null, UiStyle.CTextSub, TextAlignment.Left));
            }
            foreach (var s in stats)
            {
                UiStyle.AddRow(outer, UiStyle.MakeLabel(
                    Strings.T("calc.layerLine", s.Name, s.Count, s.AreaM2, s.AreaPing), null, null, TextAlignment.Left));
            }
            UiStyle.AddRow(outer, UiStyle.MakeLabel(
                Strings.T("calc.total", totalArea, totalPing), null, UiStyle.CAccent, TextAlignment.Left));

            UiStyle.AddRow(outer, UiStyle.Hr());
            UiStyle.AddRow(outer, UiStyle.SectionLabel(Strings.T("calc.estimate"), sectionFont, UiStyle.CAccent));

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

            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel(Strings.T("field.tileLen")), _lenBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel(Strings.T("field.tileWid")), _widBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel(Strings.T("field.wastePct")), _wasteBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel(Strings.T("field.pcsPerBox")), _pcsBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel(Strings.T("field.pricePerBox")), _priceBoxBox, null)));
            outer.Items.Add(new StackLayoutItem(UiStyle.HRow(FieldLabel(Strings.T("field.pricePerPing")), _pricePingBox, null)));

            _resultLabel = UiStyle.MakeLabel("");
            var btn = UiStyle.MakeButton(Strings.T("btn.calc"), true);
            btn.Click += (s, e) => Recalc(totalArea);
            UiStyle.AddRow(outer, btn);
            UiStyle.AddRow(outer, _resultLabel);

            UiStyle.AddRow(outer, UiStyle.Hr());
            var btnCsv = UiStyle.MakeButton(Strings.T("btn.exportCsv"));
            btnCsv.Click += OnExportCsv;
            var btnImg = UiStyle.MakeButton(Strings.T("btn.exportImage"));
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
                _resultLabel.Text = Strings.T("calc.invalidSize");
                return;
            }
            _resultLabel.Text = Strings.T("calc.result", r.NeededAreaM2, r.TileCount, r.Boxes, r.PriceByBox, r.PriceByPing);
        }

        private void OnExportCsv(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog { Title = Strings.T("dlg.exportCsv"), FileName = Strings.T("file.calcResultCsv") };
            var f = new FileFilter(Strings.T("dlg.csvFiles"), new[] { ".csv" });
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
                MessageBox.Show(this, Strings.T("msg.exported", path), Strings.T("dlg.appName"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Strings.T("msg.exportFailed", ex.Message), Strings.T("dlg.appName"));
            }
        }

        private void OnExportImage(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog { Title = Strings.T("dlg.exportImage"), FileName = Strings.T("file.calcResultPng") };
            var f = new FileFilter(Strings.T("dlg.pngFiles"), new[] { ".png" });
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
                MessageBox.Show(this, Strings.T("msg.exported", path), Strings.T("dlg.appName"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Strings.T("msg.exportFailed", ex.Message), Strings.T("dlg.appName"));
            }
        }

        private void RenderResultImage(string path)
        {
            var titleFont = new Font(FontFamilies.Sans, 13, FontStyle.Bold);
            var bodyFont = new Font(FontFamilies.Sans, 11);

            var lines = new List<string> { Strings.T("calc.resultImageTitle") };
            foreach (var s in _stats)
                lines.Add(Strings.T("calc.layerLine", s.Name, s.Count, s.AreaM2, s.AreaPing));
            double totalArea = _stats.Sum(s => s.AreaM2);
            double totalPing = totalArea / CalculatorUtil.M2PerPing;
            lines.Add(Strings.T("calc.total", totalArea, totalPing));
            if (_lastEstimate != null)
            {
                var r = _lastEstimate;
                lines.Add("");
                lines.AddRange(Strings.T("calc.result", r.NeededAreaM2, r.TileCount, r.Boxes, r.PriceByBox, r.PriceByPing).Split('\n'));
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
