using System;
using System.Collections.Generic;
using System.Linq;

namespace DB3DFloora
{
    using UvPoly = List<(double U, double V)>;

    /// <summary>為每種圖案取樣出一組縮小版的 UV 多邊形，供選圖案的縮圖使用（對照 Python 版 icons.py）。
    /// 直接重用 Patterns 的純數學生成函式，確保縮圖跟實際產生的圖案長相一致。</summary>
    public static class IconsUtil
    {
        public const double View = 20.0;
        public const double TargetCell = 17.0;

        public static List<UvPoly> SamplePolygons(string patternId)
        {
            var opts = Defaults.DefaultsFor(patternId);
            double baseDim = opts.Gy != 0 ? opts.Gy : opts.Gx;
            double scale = TargetCell / Math.Max(Math.Max(opts.Gx, baseDim), 1.0);
            double gx = opts.Gx * scale;
            double gy = baseDim * scale;
            gy = Math.Max(gy, gx / 6.0);
            double gw = Math.Max(opts.Gw * scale, 0.5);

            double diag = View * 1.15;
            List<UvPoly> raw;

            if (patternId == "Wood")
            {
                raw = Patterns.WoodPolys(diag, gx, gy, gw);
            }
            else if (patternId == "Tweed")
            {
                raw = Patterns.TweedPolys(diag, gx, gy, gw, opts.Twa);
            }
            else if (patternId == "IrPoly")
            {
                raw = Patterns.IrregularPolygonPolys(diag, gx, gw);
            }
            else
            {
                if (!Patterns.PatternCellFuncs.TryGetValue(patternId, out var fn))
                    return new List<UvPoly>();

                double cellGx = gx, cellGy = gy;
                if (patternId == "BsktWv")
                {
                    int bwb = Math.Max(2, opts.Bwb);
                    cellGx = cellGy = bwb * gy;
                }
                else if (patternId == "Diamonds")
                {
                    cellGx = cellGy = gx;
                }
                else if (patternId == "Hexgon")
                {
                    cellGx = cellGy = 1.5 * (gx / 2.0);
                }
                else if (patternId == "Octgon")
                {
                    cellGx = cellGy = gx + gw;
                }
                else if (patternId == "FanTil")
                {
                    cellGx = 2.0 * gx;
                    cellGy = gx;
                }
                else if (Patterns.HopscotchLayouts.TryGetValue(patternId, out var layout))
                {
                    cellGx = cellGy = layout.Period * gx;
                }

                int n = (int)(diag / Math.Max(Math.Min(cellGx, cellGy), 1.0)) + 2;
                raw = new List<UvPoly>();
                for (int i = -n; i <= n; i++)
                {
                    for (int j = -n; j <= n; j++)
                    {
                        raw.AddRange(fn(i, j, gx, gy, gw, opts));
                    }
                }
            }

            if (Patterns.PatternExtraRot.TryGetValue(patternId, out var extraRot) && extraRot != 0.0)
            {
                double r = extraRot * Math.PI / 180.0;
                double c = Math.Cos(r), s = Math.Sin(r);
                raw = raw.Select(poly => poly.Select(p => (U: p.U * c - p.V * s, V: p.U * s + p.V * c)).ToList()).ToList();
            }

            var kept = new List<UvPoly>();
            double pad = View + 2.0;
            foreach (var poly in raw)
            {
                if (poly.Count == 0)
                    continue;
                double cx = poly.Average(p => p.U);
                double cy = poly.Average(p => p.V);
                if (cx >= -pad && cx <= pad && cy >= -pad && cy <= pad)
                    kept.Add(poly);
            }
            return kept;
        }
    }
}
