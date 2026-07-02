using System;
using System.Collections.Generic;
using System.IO;
using Rhino.Geometry;

namespace DB3DFloora
{
    /// <summary>紋理相關選項（對齊邊／隨機位置／隨機旋轉）。對應 Python 版 texture_opts dict。</summary>
    public class TextureOptions
    {
        public bool AlignEdge;
        public bool RandomPosition;
        public bool RandomRotate;
    }

    /// <summary>材質/貼圖處理（對照 Python 版 materials.py）：讓使用者選幾張圖片當磁磚材質，
    /// 產生圖案時隨機指定給每一片磚；也提供對齊邊／隨機位置／隨機旋轉的貼圖映射。</summary>
    public static class MaterialsUtil
    {
        private static readonly Dictionary<string, int> MaterialCache = new Dictionary<string, int>();
        private static readonly Random Rng = new Random();

        public static void ResetCache()
        {
            MaterialCache.Clear();
        }

        /// <summary>依圖片路徑取得（或建立）一個貼了該圖片的 Rhino 材質，回傳材質 index。</summary>
        public static int GetOrCreateMaterial(Rhino.RhinoDoc doc, string imagePath)
        {
            if (MaterialCache.TryGetValue(imagePath, out int cached))
            {
                if (cached >= 0 && cached < doc.Materials.Count && !doc.Materials[cached].IsDeleted)
                    return cached;
            }

            var name = "Floora_" + Path.GetFileNameWithoutExtension(imagePath);
            var mat = new Rhino.DocObjects.Material { Name = name };
            try
            {
                mat.SetBitmapTexture(imagePath);
            }
            catch
            {
                // 貼圖失敗就先建立空材質，不擋流程
            }
            int idx = doc.Materials.Add(mat);
            MaterialCache[imagePath] = idx;
            return idx;
        }

        /// <summary>從 texturePaths 隨機挑一張圖片，設成這個物件屬性要用的材質。</summary>
        public static void ApplyRandomTexture(Rhino.RhinoDoc doc, Rhino.DocObjects.ObjectAttributes attrs, List<string> texturePaths)
        {
            if (texturePaths == null || texturePaths.Count == 0)
                return;
            var path = texturePaths[Rng.Next(texturePaths.Count)];
            int idx = GetOrCreateMaterial(doc, path);
            attrs.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject;
            attrs.MaterialIndex = idx;
        }

        /// <summary>依鋪磚格線方向建立平面貼圖映射；可選隨機位移/隨機旋轉讓重複貼圖不要太規律。
        /// alignEdge 關閉時改用世界 XY，不管圖案本身怎麼旋轉。</summary>
        public static Rhino.Render.TextureMapping BuildTextureMapping(
            Plane referencePlane, double tileW, double tileH,
            bool alignEdge = false, bool randomPosition = false, bool randomRotate = false)
        {
            var basePlane = alignEdge
                ? new Plane(referencePlane)
                : new Plane(referencePlane.Origin, Vector3d.XAxis, Vector3d.YAxis);

            if (randomRotate)
            {
                double ang = Rng.NextDouble() * 360.0;
                basePlane.Rotate(ang * Math.PI / 180.0, basePlane.ZAxis);
            }

            if (randomPosition)
            {
                double offU = Rng.NextDouble() * Math.Max(tileW, 0.01);
                double offV = Rng.NextDouble() * Math.Max(tileH, 0.01);
                basePlane = new Plane(basePlane.PointAt(offU, offV), basePlane.XAxis, basePlane.YAxis);
            }

            var dx = new Interval(0.0, tileW > 0 ? tileW : 1.0);
            var dy = new Interval(0.0, tileH > 0 ? tileH : 1.0);
            var dz = new Interval(0.0, 1.0);
            try
            {
                return Rhino.Render.TextureMapping.CreatePlaneMapping(basePlane, dx, dy, dz);
            }
            catch
            {
                return null;
            }
        }

        public static void ApplyTextureMapping(Rhino.RhinoDoc doc, Guid objectId, Plane referencePlane,
            double tileW, double tileH, TextureOptions textureOpts)
        {
            textureOpts = textureOpts ?? new TextureOptions();
            var mapping = BuildTextureMapping(
                referencePlane, tileW, tileH,
                textureOpts.AlignEdge, textureOpts.RandomPosition, textureOpts.RandomRotate);
            if (mapping == null)
                return;
            try
            {
                var obj = doc.Objects.Find(objectId);
                obj?.SetTextureMapping(1, mapping);
            }
            catch
            {
                // 忽略貼圖映射失敗，不影響磚片本身已經產生成功
            }
        }
    }
}
