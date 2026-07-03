using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace DB3DFloora
{
    /// <summary>底層幾何工具：取得選取物件的邊界/平面、內縮矩形、把 UV 多邊形裁切到邊界內。
    /// 對照 Python 版 geometry.py。</summary>
    public static class GeometryUtil
    {
        public const double Tol = 0.001;

        /// <summary>給定使用者選取的 ObjRef，回傳 (boundaryCurve, plane)；取不到平面時回傳 (null, default)。</summary>
        public static (Curve Boundary, Plane Plane) GetBoundaryAndPlane(ObjRef objRef)
        {
            var face = objRef.Face();
            if (face != null)
            {
                if (!face.TryGetPlane(out var plane, Tol))
                    return (null, default);
                if (face.OrientationIsReversed)
                {
                    // BrepFace.TryGetPlane 回傳的是底層 Surface 自己的參數化法向，不會考慮
                    // 這個面本身的 OrientationIsReversed 旗標。很多「其他角度」的面（旋轉、
                    // 陣列等操作產生的）剛好是方向被反轉過的面，不修正的話拿到的法向會反過來，
                    // 導致磚塊往面「裡面」擠出、跟原本選取的曲面互相交集，而不是貼著面往外長。
                    // 翻轉 Y 軸維持右手座標系（Z 軸＝法向會跟著翻正），U 軸（X）方向不變。
                    plane = new Plane(plane.Origin, plane.XAxis, -plane.YAxis);
                }
                var loop = face.OuterLoop;
                if (loop == null)
                    return (null, default);
                var crv = loop.To3dCurve();
                if (crv == null)
                    return (null, default);
                if (!crv.IsClosed)
                    crv.MakeClosed(Tol);
                return (crv, plane);
            }

            var curve = objRef.Curve();
            if (curve != null)
            {
                if (!curve.IsClosed)
                    return (null, default);
                if (!curve.TryGetPlane(out var plane2, Tol))
                    return (null, default);
                return (curve, plane2);
            }

            return (null, default);
        }

        /// <summary>矩形的 4 個角點（UV 座標），四邊各內縮 inset（用於留縫寬）。</summary>
        public static List<(double U, double V)> Rect(double u0, double v0, double w, double h, double inset)
        {
            return new List<(double, double)>
            {
                (u0 + inset, v0 + inset),
                (u0 + w - inset, v0 + inset),
                (u0 + w - inset, v0 + h - inset),
                (u0 + inset, v0 + h - inset),
            };
        }

        public static Point3d Centroid(Curve curve)
        {
            var amp = AreaMassProperties.Compute(curve);
            if (amp != null)
                return amp.Centroid;
            return curve.GetBoundingBox(true).Center;
        }

        private static bool BBoxOverlap(BoundingBox a, BoundingBox b)
        {
            return !(a.Max.X < b.Min.X || a.Min.X > b.Max.X ||
                      a.Max.Y < b.Min.Y || a.Min.Y > b.Max.Y ||
                      a.Max.Z < b.Min.Z || a.Min.Z > b.Max.Z);
        }

        private static Curve[] BooleanIntersection(Curve a, Curve b)
        {
            try
            {
                return Curve.CreateBooleanIntersection(a, b, Tol);
            }
            catch
            {
                return Curve.CreateBooleanIntersection(a, b);
            }
        }

        /// <summary>把一組 UV 多邊形映射到 gridPlane 所在的 3D 平面，並裁切到 boundaryCurve 範圍內。</summary>
        public static List<Curve> ClipPolysToBoundary(
            IEnumerable<List<(double U, double V)>> polysUv, Plane gridPlane, Curve boundaryCurve)
        {
            var expanded = ExpandedBoundingBox(boundaryCurve);
            var results = new List<Curve>();
            foreach (var polyUv in polysUv)
            {
                if (polyUv.Count < 3)
                    continue;

                var pts3d = polyUv.Select(p => gridPlane.PointAt(p.U, p.V)).ToList();
                var cbb = new BoundingBox(pts3d);
                if (!BBoxOverlap(cbb, expanded))
                    continue;

                pts3d.Add(pts3d[0]);
                PolylineCurve tileCurve;
                try
                {
                    tileCurve = new PolylineCurve(pts3d);
                }
                catch
                {
                    continue;
                }
                if (tileCurve == null || !tileCurve.IsValid)
                    continue;

                ClipOneCurveToBoundary(tileCurve, boundaryCurve, results);
            }
            return results;
        }

        /// <summary>把一組已經是真正 Curve（例如含弧線的 PolyCurve）的磚片裁切到 boundaryCurve 範圍內；
        /// 對照 ClipPolysToBoundary，但不會先攤平成多邊形，弧線在裁切後仍然是弧線／NURBS，不會變成折線。</summary>
        public static List<Curve> ClipCurvesToBoundary(IEnumerable<Curve> tileCurves, Curve boundaryCurve)
        {
            var expanded = ExpandedBoundingBox(boundaryCurve);
            var results = new List<Curve>();
            foreach (var tileCurve in tileCurves)
            {
                if (tileCurve == null || !tileCurve.IsValid)
                    continue;
                var cbb = tileCurve.GetBoundingBox(true);
                if (!BBoxOverlap(cbb, expanded))
                    continue;
                ClipOneCurveToBoundary(tileCurve, boundaryCurve, results);
            }
            return results;
        }

        private static BoundingBox ExpandedBoundingBox(Curve boundaryCurve)
        {
            var bbox = boundaryCurve.GetBoundingBox(true);
            var expanded = new BoundingBox(bbox.Min, bbox.Max);
            expanded.Inflate(Math.Max(bbox.Diagonal.Length * 0.001, 0.01));
            return expanded;
        }

        private static void ClipOneCurveToBoundary(Curve tileCurve, Curve boundaryCurve, List<Curve> results)
        {
            Curve[] pieces;
            try
            {
                pieces = BooleanIntersection(tileCurve, boundaryCurve);
            }
            catch
            {
                pieces = null;
            }
            if (pieces == null || pieces.Length == 0)
                return;

            foreach (var c in pieces)
            {
                var amp = AreaMassProperties.Compute(c);
                if (amp != null && amp.Area > 1e-4)
                    results.Add(c);
            }
        }
    }
}
