using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace DB3DFloora
{
    using UvPoly = List<(double U, double V)>;

    /// <summary>18 種圖案的生成演算法（對照 Python 版 patterns.py，非逐行翻譯，
    /// 而是用標準拼貼構造法重新實作，跟原始 SketchUp 版的手刻幾何不同）。</summary>
    public static class Patterns
    {
        public const int MaxGridSteps = 140;

        /// <summary>這些圖案在方格網格上額外疊加一個整體旋轉角度，做出對角紋理的視覺效果。</summary>
        public static readonly Dictionary<string, double> PatternExtraRot = new Dictionary<string, double>
        {
            {"Diamonds", 45.0},
            {"Hbone", 45.0},
            {"Chevrn", 45.0},
        };

        public delegate List<UvPoly> CellFunc(int i, int j, double gx, double gy, double gw, TileOptions opts);

        private static TileOptions WithR2r(TileOptions opts, double r2r)
        {
            var d = opts.Clone();
            d.R2r = r2r;
            return d;
        }

        // ---- 各圖案的「單一格子」產生函式：輸入格子索引 (i, j)，輸出一或多個 UV 多邊形 ----

        public static List<UvPoly> CellOffsetRect(int i, int j, double gx, double gy, double gw, TileOptions opts)
        {
            double r2r = opts.R2r / 100.0;
            double shift = gx != 0 ? Mod(j * r2r * gx, gx) : 0.0;
            double u0 = i * gx - shift;
            double v0 = j * gy;
            return new List<UvPoly> { GeometryUtil.Rect(u0, v0, gx, gy, gw / 2.0) };
        }

        public static List<UvPoly> CellWedge(int i, int j, double gx, double gy, double gw, TileOptions opts)
        {
            double r2r = opts.R2r / 100.0;
            double shift = gx != 0 ? Mod(j * r2r * gx, gx) : 0.0;
            double u0 = i * gx - shift;
            double v0 = j * gy;
            var rect = GeometryUtil.Rect(u0, v0, gx, gy, gw / 2.0);
            (double U, double V) p0 = rect[0], p1 = rect[1], p2 = rect[2], p3 = rect[3];
            return new List<UvPoly>
            {
                new UvPoly { p0, p1, p2 },
                new UvPoly { p0, p2, p3 },
            };
        }

        public static List<UvPoly> CellBasketweave(int i, int j, double gx, double gy, double gw, TileOptions opts)
        {
            int bwb = Math.Max(2, opts.Bwb);
            double block = bwb * gy;
            double u0 = i * block;
            double v0 = j * block;
            bool horizontal = (i + j) % 2 == 0;
            double hw = gw / 2.0;
            var polys = new List<UvPoly>();
            for (int k = 0; k < bwb; k++)
            {
                if (horizontal)
                    polys.Add(GeometryUtil.Rect(u0, v0 + k * gy, block, gy, hw));
                else
                    polys.Add(GeometryUtil.Rect(u0 + k * gy, v0, gy, block, hw));
            }
            return polys;
        }

        public static List<UvPoly> CellHexagon(int i, int j, double gx, double gy, double gw, TileOptions opts)
        {
            double s = gx / 2.0;
            double apothem = s * Math.Sqrt(3) / 2.0;
            double inset = (gw / 2.0) / Math.Cos(Math.PI / 6.0);
            double r = Math.Max(s - inset, 0.01);
            double colW = 1.5 * s;
            double rowH = 2 * apothem;
            double cu = i * colW;
            double cv = j * rowH + (i % 2 != 0 ? apothem : 0.0);
            var pts = new UvPoly();
            for (int k = 0; k < 6; k++)
            {
                double ang = k * Math.PI / 3.0; // flat-top hexagon，跟下面 colW/rowH 間距對應
                pts.Add((cu + r * Math.Cos(ang), cv + r * Math.Sin(ang)));
            }
            return new List<UvPoly> { pts };
        }

        public static List<UvPoly> CellOctagon(int i, int j, double gx, double gy, double gw, TileOptions opts)
        {
            double cell = gx + gw;
            double apothem = gx / 2.0;
            double r = apothem / Math.Cos(Math.PI / 8.0);
            double cu = i * cell;
            double cv = j * cell;
            var pts = new UvPoly();
            for (int k = 0; k < 8; k++)
            {
                double ang = Math.PI / 8.0 + k * Math.PI / 4.0;
                pts.Add((cu + r * Math.Cos(ang), cv + r * Math.Sin(ang)));
            }
            return new List<UvPoly> { pts };
        }

        public static List<UvPoly> CellIBlock(int i, int j, double gx, double gy, double gw, TileOptions opts)
        {
            double shift = (j % 2 != 0 ? 1 : 0) * (gx / 2.0);
            double u0 = i * gx - shift;
            double v0 = j * gy;
            double hw = gw / 2.0;
            double nd = gy * 0.22;
            double nw = gx * 0.28;
            double x0 = u0 + hw, x1 = u0 + gx - hw;
            double y0 = v0 + hw, y1 = v0 + gy - hw;
            double ym0 = y0 + nd, ym1 = y1 - nd;
            if (ym0 >= ym1 || nw * 2 >= (x1 - x0))
                return new List<UvPoly> { GeometryUtil.Rect(u0, v0, gx, gy, hw) };

            var pts = new UvPoly
            {
                (x0, y0), (x0 + nw, y0), (x0 + nw, ym0), (x1 - nw, ym0), (x1 - nw, y0),
                (x1, y0), (x1, y1),
                (x1 - nw, y1), (x1 - nw, ym1), (x0 + nw, ym1), (x0 + nw, y1),
                (x0, y1),
            };
            return new List<UvPoly> { pts };
        }

        public static List<UvPoly> CellDiamond(int i, int j, double gx, double gy, double gw, TileOptions opts)
        {
            return CellOffsetRect(i, j, gx, gx, gw, WithR2r(opts, 0.0));
        }

        /// <summary>扇形磚（魚鱗/扇貝紋）：半圓形磚片，隔行錯位排列使弧邊相互交扣。</summary>
        public static List<UvPoly> CellFanTile(int i, int j, double gx, double gy, double gw, TileOptions opts)
        {
            double r = gx;
            double rowH = r;
            double shift = (j % 2 != 0 ? 1 : 0) * r;
            double cu = i * 2 * r - shift;
            double cv = j * rowH;
            double hw = gw / 2.0;
            const int segs = 16;
            double rad = Math.Max(r - hw, 0.05);
            var center = (X: cu, Y: cv + rowH);
            var pts = new UvPoly();
            for (int k = 0; k <= segs; k++)
            {
                double ang = Math.PI - (Math.PI * k / segs);
                pts.Add((center.X + rad * Math.Cos(ang), center.Y - rad * Math.Sin(ang)));
            }
            return new List<UvPoly> { pts };
        }

        /// <summary>「大小格子」系列：一個週期內放一片大方磚＋數片小方磚，(col,row,span) 皆以「小格」為單位。</summary>
        public static readonly Dictionary<string, (int Period, (int Col, int Row, int Span)[] Big, (int Col, int Row, int Span)[] Small)> HopscotchLayouts =
            new Dictionary<string, (int, (int, int, int)[], (int, int, int)[])>
            {
                {"HpScth1", (3, new[] {(0, 0, 2)}, new[] {(2, 0, 1), (2, 1, 1), (0, 2, 1), (1, 2, 1), (2, 2, 1)})},
                {"HpScth2", (3, new[] {(1, 1, 2)}, new[] {(0, 0, 1), (1, 0, 1), (2, 0, 1), (0, 1, 1), (0, 2, 1)})},
                {"HpScth3", (3, new[] {(0, 1, 2)}, new[] {(0, 0, 1), (1, 0, 1), (2, 0, 1), (2, 1, 1), (2, 2, 1)})},
                {"HpScth4", (4, new[] {(0, 0, 3)}, new[] {(3, 0, 1), (3, 1, 1), (3, 2, 1), (0, 3, 1), (1, 3, 1), (2, 3, 1), (3, 3, 1)})},
            };

        public static List<UvPoly> CellHopscotch(int i, int j, double gx, double gy, double gw, TileOptions opts, string patternId)
        {
            var (period, big, small) = HopscotchLayouts[patternId];
            double unit = gx;
            double hw = gw / 2.0;
            double u0 = i * period * unit;
            double v0 = j * period * unit;
            var polys = new List<UvPoly>();
            foreach (var (col, row, span) in big.Concat(small))
                polys.Add(GeometryUtil.Rect(u0 + col * unit, v0 + row * unit, span * unit, span * unit, hw));
            return polys;
        }

        public static readonly Dictionary<string, CellFunc> PatternCellFuncs = BuildCellFuncs();

        private static Dictionary<string, CellFunc> BuildCellFuncs()
        {
            var d = new Dictionary<string, CellFunc>
            {
                {"Brick", CellOffsetRect},
                {"Tile", CellOffsetRect},
                {"Wedge", CellWedge},
                {"Hbone", (i, j, gx, gy, gw, opts) => CellOffsetRect(i, j, gx, gy, gw, WithR2r(opts, 50.0))},
                {"Chevrn", (i, j, gx, gy, gw, opts) => CellOffsetRect(i, j, gx, gy, gw, WithR2r(opts, 50.0))},
                {"BsktWv", CellBasketweave},
                {"Hexgon", CellHexagon},
                {"Octgon", CellOctagon},
                {"I_Block", CellIBlock},
                {"Diamonds", CellDiamond},
                {"FanTil", CellFanTile},
            };
            foreach (var pid in HopscotchLayouts.Keys)
            {
                var capturedId = pid;
                d[pid] = (i, j, gx, gy, gw, opts) => CellHopscotch(i, j, gx, gy, gw, opts, capturedId);
            }
            return d;
        }

        private static int GridCount(double diag, double step)
        {
            step = Math.Max(step, 0.1);
            return Math.Min((int)(diag / step) + 3, MaxGridSteps);
        }

        private static readonly Random Rng = new Random();

        private static double Mod(double a, double b)
        {
            double r = a % b;
            return r < 0 ? r + b : r;
        }

        public static List<UvPoly> WoodPolys(double diag, double gx, double gy, double gw)
        {
            double hw = gw / 2.0;
            int rows = Math.Min((int)(diag / gy) + 3, MaxGridSteps);
            var polys = new List<UvPoly>();
            for (int r = -rows; r <= rows; r++)
            {
                double v0 = r * gy;
                double u = -diag - Rng.NextDouble() * gx;
                double limit = diag * 1.2;
                int guard = 0;
                while (u < limit && guard < 4000)
                {
                    double length = gx * (0.55 + Rng.NextDouble() * (1.5 - 0.55));
                    polys.Add(GeometryUtil.Rect(u, v0, length, gy, hw));
                    u += length;
                    guard++;
                }
            }
            return polys;
        }

        public static List<UvPoly> TweedPolys(double diag, double gx, double gy, double gw, double twa)
        {
            double hw = gw / 2.0;
            double shear = Math.Tan(twa * Math.PI / 180.0);
            int n = GridCount(diag, Math.Min(gx, gy));
            var polys = new List<UvPoly>();
            for (int i = -n; i <= n; i++)
            {
                for (int j = -n; j <= n; j++)
                {
                    var r = GeometryUtil.Rect(i * gx, j * gy, gx, gy, hw);
                    polys.Add(r.Select(p => (p.U + p.V * shear, p.V)).ToList());
                }
            }
            return polys;
        }

        // ---- 不規則多邊形（IrPoly）：以 Voronoi 圖為基礎，半平面裁切法直接算出各細胞多邊形 ----

        private const int IrpolyMaxN = 45;
        private const int IrpolyNeighborWindow = 4;

        private static (double X, double Y) LineIntersect((double X, double Y) p1, (double X, double Y) p2,
            double mx, double my, double nx, double ny)
        {
            double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
            double denom = dx * nx + dy * ny;
            if (Math.Abs(denom) < 1e-12)
                return p1;
            double t = ((mx - p1.X) * nx + (my - p1.Y) * ny) / denom;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return (p1.X + t * dx, p1.Y + t * dy);
        }

        /// <summary>保留多邊形中「離 site 比離 other 近」的那一半（用垂直平分線裁切）。</summary>
        private static List<(double X, double Y)> ClipHalfplane(List<(double X, double Y)> poly,
            (double X, double Y) site, (double X, double Y) other)
        {
            double mx = (site.X + other.X) / 2.0, my = (site.Y + other.Y) / 2.0;
            double nx = other.X - site.X, ny = other.Y - site.Y;

            bool Inside((double X, double Y) pt) => (pt.X - mx) * nx + (pt.Y - my) * ny <= 0;

            var result = new List<(double, double)>();
            int n = poly.Count;
            for (int idx = 0; idx < n; idx++)
            {
                var cur = poly[idx];
                var prev = poly[(idx - 1 + n) % n];
                bool curIn = Inside(cur);
                bool prevIn = Inside(prev);
                if (curIn)
                {
                    if (!prevIn)
                        result.Add(LineIntersect(prev, cur, mx, my, nx, ny));
                    result.Add(cur);
                }
                else if (prevIn)
                {
                    result.Add(LineIntersect(prev, cur, mx, my, nx, ny));
                }
            }
            return result;
        }

        private static (double X, double Y)? LineLineIntersect(
            double x1, double y1, double dx1, double dy1,
            double x2, double y2, double dx2, double dy2)
        {
            double denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < 1e-12)
                return null;
            double t = ((x2 - x1) * dy2 - (y2 - y1) * dx2) / denom;
            return (x1 + t * dx1, y1 + t * dy1);
        }

        /// <summary>將凸多邊形（Voronoi 細胞必為凸）整體內縮 dist，用於留出縫寬。</summary>
        private static List<(double X, double Y)> InsetPolygon(List<(double X, double Y)> poly, double dist)
        {
            if (dist <= 0 || poly.Count < 3)
                return poly;
            int n = poly.Count;
            double area2 = 0;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                area2 += a.X * b.Y - b.X * a.Y;
            }
            if (area2 < 0)
            {
                poly = Enumerable.Reverse(poly).ToList();
                n = poly.Count;
            }

            var edges = new List<(double Ox, double Oy, double Dx, double Dy)>();
            for (int i = 0; i < n; i++)
            {
                var p1 = poly[i];
                var p2 = poly[(i + 1) % n];
                double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                if (length < 1e-9)
                    continue;
                double nx = dy / length, ny = -dx / length;
                edges.Add((p1.X - nx * dist, p1.Y - ny * dist, dx, dy));
            }
            if (edges.Count < 3)
                return null;

            var newPts = new List<(double, double)>();
            int m = edges.Count;
            for (int i = 0; i < m; i++)
            {
                var e1 = edges[(i - 1 + m) % m];
                var e2 = edges[i];
                var pt = LineLineIntersect(e1.Ox, e1.Oy, e1.Dx, e1.Dy, e2.Ox, e2.Oy, e2.Dx, e2.Dy);
                if (pt == null)
                    return null;
                newPts.Add(pt.Value);
            }
            return newPts;
        }

        public static List<UvPoly> IrregularPolygonPolys(double diag, double gx, double gw)
        {
            double step = Math.Max(gx, 1.0);
            int n = Math.Min((int)(diag / step) + 2, IrpolyMaxN);
            int win = IrpolyNeighborWindow;

            var gridPts = new Dictionary<(int, int), (double X, double Y)>();
            for (int i = -n; i <= n; i++)
            {
                for (int j = -n; j <= n; j++)
                {
                    double jx = (Rng.NextDouble() * 0.7 - 0.35) * step;
                    double jy = (Rng.NextDouble() * 0.7 - 0.35) * step;
                    gridPts[(i, j)] = (i * step + jx, j * step + jy);
                }
            }

            double half = diag * 1.3;
            var bound = new List<(double, double)> { (-half, -half), (half, -half), (half, half), (-half, half) };
            double hw = gw / 2.0;

            var polys = new List<UvPoly>();
            foreach (var kv in gridPts)
            {
                var (i, j) = kv.Key;
                var p = kv.Value;
                var cell = new List<(double, double)>(bound);
                bool stop = false;
                for (int di = -win; di <= win && !stop; di++)
                {
                    for (int dj = -win; dj <= win; dj++)
                    {
                        if (di == 0 && dj == 0)
                            continue;
                        if (!gridPts.TryGetValue((i + di, j + dj), out var q))
                            continue;
                        cell = ClipHalfplane(cell, p, q);
                        if (cell.Count == 0)
                        {
                            stop = true;
                            break;
                        }
                    }
                }
                if (cell.Count >= 3)
                {
                    var inset = InsetPolygon(cell, hw);
                    if (inset != null)
                        polys.Add(inset.Select(pt => (pt.Item1, pt.Item2)).ToList());
                }
            }
            return polys;
        }

        private static Point3d ResolveOrigin(Curve boundaryCurve, Plane plane, BoundingBox bbox, string spt, Point3d? anchorPt)
        {
            if (spt == "pick" && anchorPt.HasValue)
                return anchorPt.Value;
            if (spt == "center")
                return GeometryUtil.Centroid(boundaryCurve);
            bool ok = plane.ClosestParameter(bbox.Min, out double u0, out double v0);
            if (!ok)
                return bbox.Min;
            return plane.PointAt(u0, v0);
        }

        /// <summary>主要進入點：依圖案 ID、選取的邊界/平面與選項，回傳裁切好的磁磚 Curve 清單。</summary>
        public static List<Curve> Generate(string patternId, Curve boundaryCurve, Plane plane, TileOptions opts, Point3d? anchorPt = null)
        {
            double gx = opts.Gx;
            double gy = opts.Gy;
            double gw = opts.Gw;
            double rot = opts.Rot + (PatternExtraRot.TryGetValue(patternId, out var extra) ? extra : 0.0);
            string spt = opts.Spt;

            if (gx <= 0 || gy <= 0)
                throw new ArgumentException("長度與寬度必須大於 0");

            var bbox = boundaryCurve.GetBoundingBox(true);
            double diag = bbox.Diagonal.Length;
            if (diag <= 0)
                throw new ArgumentException("無法取得選取物件的邊界");

            var origin = ResolveOrigin(boundaryCurve, plane, bbox, spt, anchorPt);
            var gridPlane = new Plane(origin, plane.XAxis, plane.YAxis);
            if (rot != 0)
                gridPlane.Rotate(rot * Math.PI / 180.0, gridPlane.ZAxis);

            List<UvPoly> raw;
            if (patternId == "Wood")
            {
                raw = WoodPolys(diag, gx, gy, gw);
            }
            else if (patternId == "Tweed")
            {
                raw = TweedPolys(diag, gx, gy, gw, opts.Twa);
            }
            else if (patternId == "IrPoly")
            {
                raw = IrregularPolygonPolys(diag, gx, gw);
            }
            else
            {
                if (!PatternCellFuncs.TryGetValue(patternId, out var cellFn))
                    throw new ArgumentException("未支援的圖案：" + patternId);

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
                else if (HopscotchLayouts.ContainsKey(patternId))
                {
                    int period = HopscotchLayouts[patternId].Period;
                    cellGx = cellGy = period * gx;
                }

                int n = GridCount(diag, Math.Min(cellGx, cellGy));
                raw = new List<UvPoly>();
                for (int i = -n; i <= n; i++)
                {
                    for (int j = -n; j <= n; j++)
                    {
                        raw.AddRange(cellFn(i, j, gx, gy, gw, opts));
                    }
                }
            }

            if (raw.Count > 30000)
                throw new ArgumentException("磚片數量過多，請放大磚片尺寸或縮小選取範圍");

            return GeometryUtil.ClipPolysToBoundary(raw, gridPlane, boundaryCurve);
        }
    }
}
