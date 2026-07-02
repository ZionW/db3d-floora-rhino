using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rhino.Geometry;

namespace DB3DFloora
{
    public class LayerStat
    {
        public string Name;
        public int Count;
        public double AreaM2;
        public double AreaPing;
    }

    public class EstimateResult
    {
        public double NeededAreaM2;
        public int TileCount;
        public int Boxes;
        public double PriceByBox;
        public double PriceByPing;
    }

    /// <summary>材質/用量計算機（對照 Python 版 calculator.py）：掃描 Floora_ 開頭圖層統計面積、
    /// 依磚片尺寸/耗損率/每箱片數/單價估算所需片數、箱數與價格。</summary>
    public static class CalculatorUtil
    {
        public const double M2PerPing = 3.30579;

        public static List<LayerStat> LayerAreaStats(Rhino.RhinoDoc doc, string prefix = "Floora_", HashSet<string> exclude = null)
        {
            exclude = exclude ?? new HashSet<string> { "Floora_Preview" };
            var stats = new Dictionary<string, (int Count, double Area)>();

            foreach (var obj in doc.Objects)
            {
                Rhino.DocObjects.Layer layer;
                try
                {
                    layer = doc.Layers[obj.Attributes.LayerIndex];
                }
                catch
                {
                    continue;
                }
                string name = layer?.Name ?? "";
                if (!name.StartsWith(prefix) || exclude.Contains(name))
                    continue;

                double area = 0.0;
                try
                {
                    var amp = AreaMassProperties.Compute(new[] { obj.Geometry });
                    if (amp != null)
                        area = amp.Area;
                }
                catch
                {
                    area = 0.0;
                }
                if (area <= 0)
                    continue;

                if (!stats.TryGetValue(name, out var entry))
                    entry = (0, 0.0);
                entry.Count += 1;
                entry.Area += area;
                stats[name] = entry;
            }

            double scale = SqrUnitToSqm(doc);
            var results = stats.Select(kv =>
            {
                double areaM2 = kv.Value.Area * scale;
                return new LayerStat
                {
                    Name = kv.Key,
                    Count = kv.Value.Count,
                    AreaM2 = Math.Round(areaM2, 4),
                    AreaPing = Math.Round(areaM2 / M2PerPing, 4),
                };
            }).ToList();
            results.Sort((a, b) => b.AreaM2.CompareTo(a.AreaM2));
            return results;
        }

        private static double SqrUnitToSqm(Rhino.RhinoDoc doc)
        {
            double toM = Rhino.RhinoMath.UnitScale(doc.ModelUnitSystem, Rhino.UnitSystem.Meters);
            return toM * toM;
        }

        public static EstimateResult Estimate(double areaM2, double tileLenCm, double tileWidCm, double wastePct,
            double pcsPerBox, double pricePerBox, double pricePerPing)
        {
            double tileAreaM2 = (tileLenCm / 100.0) * (tileWidCm / 100.0);
            if (tileAreaM2 <= 0)
                return null;
            double neededArea = areaM2 * (1.0 + wastePct / 100.0);
            int tileCount = (int)Math.Ceiling(neededArea / tileAreaM2);
            int boxes = pcsPerBox > 0 ? (int)Math.Ceiling(tileCount / pcsPerBox) : 0;
            double priceByBox = boxes * pricePerBox;
            double priceByPing = (areaM2 / M2PerPing) * pricePerPing;
            return new EstimateResult
            {
                NeededAreaM2 = Math.Round(neededArea, 4),
                TileCount = tileCount,
                Boxes = boxes,
                PriceByBox = Math.Round(priceByBox, 2),
                PriceByPing = Math.Round(priceByPing, 2),
            };
        }

        /// <summary>把圖層統計＋估算結果寫成 CSV（帶 BOM，Excel 開啟中文不會亂碼）。</summary>
        public static void WriteCsv(string path, List<LayerStat> stats, EstimateResult estimateResult)
        {
            var lines = new List<string> { "圖層,片數,面積(m2),面積(坪)" };
            double totalArea = 0.0, totalPing = 0.0;
            foreach (var s in stats)
            {
                lines.Add($"{s.Name},{s.Count},{s.AreaM2:F4},{s.AreaPing:F4}");
                totalArea += s.AreaM2;
                totalPing += s.AreaPing;
            }
            lines.Add($"總計,,{totalArea:F4},{totalPing:F4}");

            if (estimateResult != null)
            {
                lines.Add("");
                lines.Add("項目,數值");
                lines.Add($"含耗損面積(m2),{estimateResult.NeededAreaM2:F4}");
                lines.Add($"需求片數,{estimateResult.TileCount}");
                lines.Add($"需求箱數,{estimateResult.Boxes}");
                lines.Add($"估價(依箱),{estimateResult.PriceByBox:F0}");
                lines.Add($"估價(依坪),{estimateResult.PriceByPing:F0}");
            }

            var utf8Bom = new UTF8Encoding(true);
            File.WriteAllText(path, string.Join("\r\n", lines), utf8Bom);
        }
    }
}
