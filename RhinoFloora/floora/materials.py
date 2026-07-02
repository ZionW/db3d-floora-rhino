# encoding: utf-8
"""材質/貼圖處理（比照原 SketchUp 版 materials.rb 的隨機貼圖功能，簡化版）。
讓使用者選幾張圖片當磁磚材質，產生圖案時隨機指定給每一片磚；
也提供對齊邊／隨機位置／隨機旋轉的貼圖映射（texture mapping），對應原本紋理選項。
"""

import math
import os
import random

import Rhino
import Rhino.Geometry as rg

_material_cache = {}  # path -> material index，避免同一張圖重複建立材質


def reset_cache():
    _material_cache.clear()


def get_or_create_material(doc, image_path):
    """依圖片路徑取得（或建立）一個貼了該圖片的 Rhino 材質，回傳材質 index。"""
    cached = _material_cache.get(image_path)
    if cached is not None:
        # 確認材質還存在（使用者可能重設過文件）
        if 0 <= cached < doc.Materials.Count and not doc.Materials[cached].IsDeleted:
            return cached

    name = u"Floora_%s" % os.path.splitext(os.path.basename(image_path))[0]
    mat = Rhino.DocObjects.Material()
    mat.Name = name
    try:
        mat.SetBitmapTexture(image_path)
    except Exception:
        pass
    idx = doc.Materials.Add(mat)
    _material_cache[image_path] = idx
    return idx


def apply_random_texture(doc, attrs, texture_paths):
    """從 texture_paths 隨機挑一張圖片，設成這個物件屬性要用的材質。"""
    if not texture_paths:
        return
    path = random.choice(texture_paths)
    idx = get_or_create_material(doc, path)
    attrs.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject
    attrs.MaterialIndex = idx


def build_texture_mapping(reference_plane, tile_w, tile_h, align_edge=False,
                           random_position=False, random_rotate=False):
    """依鋪磚格線方向建立平面貼圖映射；可選隨機位移/隨機旋轉讓重複貼圖不要太規律。
    align_edge 關閉時改用世界 XY，不管圖案本身怎麼旋轉。"""
    if align_edge:
        base = rg.Plane(reference_plane)
    else:
        base = rg.Plane(reference_plane.Origin, rg.Vector3d.XAxis, rg.Vector3d.YAxis)

    if random_rotate:
        ang = random.uniform(0.0, 360.0)
        base.Rotate(math.radians(ang), base.ZAxis)

    if random_position:
        off_u = random.uniform(0.0, max(tile_w, 0.01))
        off_v = random.uniform(0.0, max(tile_h, 0.01))
        base = rg.Plane(base.PointAt(off_u, off_v), base.XAxis, base.YAxis)

    dx = rg.Interval(0.0, tile_w if tile_w > 0 else 1.0)
    dy = rg.Interval(0.0, tile_h if tile_h > 0 else 1.0)
    dz = rg.Interval(0.0, 1.0)
    try:
        return Rhino.Render.TextureMapping.CreatePlaneMapping(base, dx, dy, dz)
    except Exception:
        return None


def apply_texture_mapping(doc, object_id, reference_plane, tile_w, tile_h, texture_opts):
    texture_opts = texture_opts or {}
    mapping = build_texture_mapping(
        reference_plane, tile_w, tile_h,
        align_edge=texture_opts.get('align_edge', False),
        random_position=texture_opts.get('random_position', False),
        random_rotate=texture_opts.get('random_rotate', False),
    )
    if mapping is None:
        return
    try:
        obj = doc.Objects.Find(object_id)
        if obj is not None:
            obj.SetTextureMapping(1, mapping)
    except Exception:
        pass
