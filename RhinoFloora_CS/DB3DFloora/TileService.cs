using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;

namespace DB3DFloora
{
    /// <summary>圖層管理、磚片實體建構（含倒斜角/隨機缺陷/背面）、選面。
    /// 對照 Python 版 ui.py 裡跟 Eto 介面無關的那些模組層級函式。</summary>
    public static class TileService
    {
        public static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            {"Brick", Color.FromArgb(196, 120, 80)},
            {"Tile", Color.FromArgb(200, 200, 200)},
            {"Wood", Color.FromArgb(150, 110, 60)},
            {"Wedge", Color.FromArgb(170, 150, 120)},
            {"Tweed", Color.FromArgb(180, 160, 140)},
            {"Hbone", Color.FromArgb(160, 120, 90)},
            {"Chevrn", Color.FromArgb(140, 100, 80)},
            {"BsktWv", Color.FromArgb(120, 140, 110)},
            {"HpScth1", Color.FromArgb(170, 130, 150)},
            {"HpScth2", Color.FromArgb(170, 130, 150)},
            {"HpScth3", Color.FromArgb(170, 130, 150)},
            {"HpScth4", Color.FromArgb(170, 130, 150)},
            {"Hexgon", Color.FromArgb(100, 140, 160)},
            {"Octgon", Color.FromArgb(150, 150, 180)},
            {"I_Block", Color.FromArgb(130, 130, 130)},
            {"Diamonds", Color.FromArgb(180, 140, 180)},
            {"FanTil", Color.FromArgb(120, 160, 190)},
            {"IrPoly", Color.FromArgb(140, 160, 140)},
        };

        private static readonly Random Rng = new Random();

        public static Color ColorFor(string patternId)
        {
            return Palette.TryGetValue(patternId, out var c) ? c : Color.FromArgb(180, 180, 180);
        }

        public static int FindLayer(Rhino.RhinoDoc doc, string name)
        {
            foreach (var lyr in doc.Layers)
            {
                if (!lyr.IsDeleted && lyr.Name == name)
                    return lyr.Index;
            }
            return -1;
        }

        public static int PatternLayer(Rhino.RhinoDoc doc, string patternId)
        {
            string name = "Floora_" + (Defaults.NamesTw.TryGetValue(patternId, out var n) ? n : patternId);
            int idx = FindLayer(doc, name);
            if (idx >= 0)
                return idx;
            var newLayer = new Layer { Name = name, Color = ColorFor(patternId) };
            return doc.Layers.Add(newLayer);
        }

        public static int PreviewLayer(Rhino.RhinoDoc doc)
        {
            const string name = "Floora_Preview";
            int idx = FindLayer(doc, name);
            if (idx >= 0)
                return idx;
            var newLayer = new Layer { Name = name, Color = Color.FromArgb(0x2A, 0x5F, 0x8A) };
            return doc.Layers.Add(newLayer);
        }

        /// <summary>把封閉平面曲線往內縮 dist（挑面積變小的那個方向），失敗回傳 null。</summary>
        private static Curve OffsetInward(Curve curve, Plane plane, double dist, double tol)
        {
            var origAmp = AreaMassProperties.Compute(curve);
            double? origArea = origAmp?.Area;
            Curve best = null;
            double? bestArea = null;
            foreach (var sign in new[] { 1.0, -1.0 })
            {
                Curve[] cand;
                try
                {
                    cand = curve.Offset(plane, dist * sign, tol, CurveOffsetCornerStyle.Sharp);
                }
                catch
                {
                    cand = null;
                }
                if (cand == null || cand.Length == 0)
                    continue;
                var c = cand[0];
                if (c == null)
                    continue;
                if (!c.IsClosed)
                    c.MakeClosed(tol);
                var amp = AreaMassProperties.Compute(c);
                if (amp == null || amp.Area <= 0)
                    continue;
                if (origArea.HasValue && amp.Area >= origArea.Value)
                    continue;
                if (!bestArea.HasValue || amp.Area > bestArea.Value)
                {
                    best = c;
                    bestArea = amp.Area;
                }
            }
            return best;
        }

        /// <summary>磚片頂緣倒斜角：頂面內縮成一圈斜角，斜角以下維持原本外緣尺寸直到底面。
        /// 任何一步失敗都回傳 null，呼叫端會自動退回原本的直角擠出。
        /// 幾何先照原本方式往下（-Z）蓋出「頂面在 z=0、底面在 z=-gd」的形狀，最後整塊沿法線方向平移 +gd，
        /// 讓底面貼齊選取的曲線／曲面（z=0）、磚體往外（+Z）長出去，不會鑽進選取物件裡。</summary>
        private static Brep BuildBeveledSolid(Curve curve, Plane plane, double gd, double bevelSize, double tol)
        {
            bevelSize = gd > tol ? Math.Min(bevelSize, gd - tol) : bevelSize;
            if (bevelSize <= tol)
                return null;
            var inner = OffsetInward(curve, plane, bevelSize, tol);
            if (inner == null)
                return null;

            var outerLower = curve.DuplicateCurve();
            outerLower.Transform(Transform.Translation(-plane.ZAxis * bevelSize));
            var outerBottom = curve.DuplicateCurve();
            outerBottom.Transform(Transform.Translation(-plane.ZAxis * gd));

            var pieces = new List<Brep>();
            var topCap = Brep.CreatePlanarBreps(inner, tol);
            if (topCap != null)
                pieces.AddRange(topCap);
            var bevelRing = Brep.CreateFromLoft(new[] { inner, outerLower }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            if (bevelRing != null)
                pieces.AddRange(bevelRing);

            double remaining = gd - bevelSize;
            Brep[] bottomCap;
            if (remaining > tol)
            {
                var wallRing = Brep.CreateFromLoft(new[] { outerLower, outerBottom }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (wallRing != null)
                    pieces.AddRange(wallRing);
                bottomCap = Brep.CreatePlanarBreps(outerBottom, tol);
            }
            else
            {
                bottomCap = Brep.CreatePlanarBreps(outerLower, tol);
            }
            if (bottomCap != null)
                pieces.AddRange(bottomCap);

            if (pieces.Count == 0)
                return null;
            Brep[] joined;
            try
            {
                joined = Brep.JoinBreps(pieces, tol);
            }
            catch
            {
                joined = null;
            }
            if (joined == null || joined.Length == 0)
                return null;
            var result = joined[0];
            result.Transform(Transform.Translation(plane.ZAxis * gd));
            return result;
        }

        /// <summary>隨機小角度傾斜整片磚，模擬鋪貼時常見的高低差/缺陷感。失敗就原樣傳回。</summary>
        private static Brep ApplyRandomDefect(Brep brep, double defectMin, double defectMax)
        {
            if (brep == null || defectMax <= 0)
                return brep;
            try
            {
                var bbox = brep.GetBoundingBox(true);
                var center = bbox.Center;
                var axis = new Vector3d(Rng.NextDouble() * 2 - 1, Rng.NextDouble() * 2 - 1, 0.0);
                if (!axis.Unitize())
                    return brep;
                double angleDeg = defectMin + Rng.NextDouble() * (defectMax - defectMin);
                if (angleDeg <= 0)
                    return brep;
                var xf = Transform.Rotation(angleDeg * Math.PI / 180.0, axis, center);
                brep.Transform(xf);
            }
            catch
            {
                // 傾斜失敗就用原本沒傾斜的磚片，不擋流程
            }
            return brep;
        }

        public class TileBuildOptions
        {
            public string PaintMode = "current"; // current / custom_color / texture
            public List<string> TexturePaths;
            public Color? CustomColor;
            public bool BevelEnabled;
            public double BevelSize = 0.3;
            public bool RandomDefect;
            public double DefectMin;
            public double DefectMax = 2.0;
            public bool BackFace;
        }

        /// <summary>把一片磚（含縫深擠出、倒角、隨機缺陷、材質/顏色、背面）加入文件，回傳新物件 Id；失敗回傳 null。</summary>
        public static Guid? AddTile(Rhino.RhinoDoc doc, Curve curve, int layerIndex, double gd, TileBuildOptions opts)
        {
            opts = opts ?? new TileBuildOptions();
            var attrs = new ObjectAttributes { LayerIndex = layerIndex };

            if (opts.PaintMode == "custom_color" && opts.CustomColor.HasValue)
            {
                attrs.ColorSource = ObjectColorSource.ColorFromObject;
                attrs.ObjectColor = opts.CustomColor.Value;
            }
            else if (opts.PaintMode == "texture" && opts.TexturePaths != null && opts.TexturePaths.Count > 0)
            {
                MaterialsUtil.ApplyRandomTexture(doc, attrs, opts.TexturePaths);
            }

            if (!curve.TryGetPlane(out var curvePlane, GeometryUtil.Tol))
                curvePlane = new Plane(curve.PointAtStart, Vector3d.ZAxis);

            object geom = null; // Extrusion 或 Brep

            if (gd > 0 && opts.BevelEnabled && opts.BevelSize > 0)
            {
                Brep beveled = null;
                try
                {
                    beveled = BuildBeveledSolid(curve, curvePlane, gd, opts.BevelSize, GeometryUtil.Tol);
                }
                catch
                {
                    beveled = null;
                }
                if (beveled != null && beveled.IsValid)
                    geom = beveled;
            }

            if (geom == null && gd > 0)
            {
                try
                {
                    // 底面貼齊選取的曲線／曲面（curve 本身），往法線正方向（+gd）長出去，避免跟選取物件交集
                    geom = Extrusion.Create(curve, gd, true);
                }
                catch
                {
                    geom = null;
                }
            }

            if (geom == null)
            {
                try
                {
                    var breps = Brep.CreatePlanarBreps(curve, GeometryUtil.Tol);
                    if (breps != null && breps.Length > 0)
                        geom = breps[0];
                }
                catch
                {
                    geom = null;
                }
            }

            if (geom == null)
                return doc.Objects.AddCurve(curve, attrs);

            if (opts.RandomDefect)
            {
                Brep brep = geom is Extrusion ext ? ext.ToBrep() : geom as Brep;
                var defected = ApplyRandomDefect(brep, opts.DefectMin, opts.DefectMax);
                if (defected != null)
                    geom = defected;
            }

            Guid gid;
            if (geom is Extrusion extrusion)
                gid = doc.Objects.AddExtrusion(extrusion, attrs);
            else
                gid = doc.Objects.AddBrep((Brep)geom, attrs);

            if (gid == Guid.Empty)
                return null;

            if (opts.BackFace && gd > 0)
            {
                try
                {
                    // 背面：在選取的曲線／曲面另一側（-Z）補一塊對稱的磚體，讓從背面看也是實心的；
                    // 緊貼在 z=0，不會往主磚體那側（+Z）多長，也不會鑽進選取物件裡。
                    var backExt = Extrusion.Create(curve, -gd, true);
                    if (backExt != null)
                        doc.Objects.AddExtrusion(backExt, attrs);
                }
                catch
                {
                    // 背面是附加效果，失敗不影響主磚片已經成功產生
                }
            }

            return gid;
        }

        public static ObjRef PickFace()
        {
            var go = new GetObject();
            go.SetCommandPrompt("請選取要鋪磚的平面（面或封閉平面曲線），按 Enter/Esc 取消");
            go.GeometryFilter = ObjectType.Surface | ObjectType.Brep | ObjectType.Curve;
            go.SubObjectSelect = true;
            var result = go.Get();
            if (result != Rhino.Input.GetResult.Object)
                return null;
            return go.Object(0);
        }
    }
}
