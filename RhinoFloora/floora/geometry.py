# encoding: utf-8
"""底層幾何工具：取得選取物件的邊界/平面、內縮矩形、將 UV 多邊形裁切到邊界內。
比照原 SketchUp 版 geometry.rb（face_offset / calc_centroid 等）的功能，改用 RhinoCommon 實作。
"""

import Rhino.Geometry as rg

TOL = 0.001


def get_boundary_and_plane(obj_ref):
    """給定使用者選取的 Rhino.DocObjects.ObjRef，回傳 (boundary_curve, plane)。
    若選到的是 Brep 的某個面，取該面外圈曲線與其平面；
    若選到的是封閉平面曲線，直接使用。取不到平面（非平面物件）則回傳 (None, None)。
    """
    face = obj_ref.Face()
    if face is not None:
        ok, plane = face.TryGetPlane(TOL)
        if not ok:
            return None, None
        loop = face.OuterLoop
        if loop is None:
            return None, None
        crv = loop.To3dCurve()
        if crv is None:
            return None, None
        if not crv.IsClosed:
            crv.MakeClosed(TOL)
        return crv, plane

    curve = obj_ref.Curve()
    if curve is not None:
        if not curve.IsClosed:
            return None, None
        ok, plane = curve.TryGetPlane(TOL)
        if not ok:
            return None, None
        return curve, plane

    return None, None


def rect(u0, v0, w, h, inset):
    """回傳一個矩形的 4 個角點（UV 座標），四邊各內縮 inset（用於留縫寬）。"""
    return [
        (u0 + inset, v0 + inset),
        (u0 + w - inset, v0 + inset),
        (u0 + w - inset, v0 + h - inset),
        (u0 + inset, v0 + h - inset),
    ]


def centroid(curve):
    amp = rg.AreaMassProperties.Compute(curve)
    if amp:
        return amp.Centroid
    return curve.GetBoundingBox(True).Center


def _bbox_overlap(a, b):
    return not (a.Max.X < b.Min.X or a.Min.X > b.Max.X or
                a.Max.Y < b.Min.Y or a.Min.Y > b.Max.Y or
                a.Max.Z < b.Min.Z or a.Min.Z > b.Max.Z)


def _boolean_intersection(curve_a, curve_b):
    try:
        return rg.Curve.CreateBooleanIntersection(curve_a, curve_b, TOL)
    except TypeError:
        return rg.Curve.CreateBooleanIntersection(curve_a, curve_b)


def clip_polys_to_boundary(polys_uv, grid_plane, boundary_curve):
    """把一組 UV 多邊形（每個為 [(u,v), ...]）映射到 grid_plane 所在的 3D 平面，
    並裁切到 boundary_curve 範圍內，回傳裁切後的 Curve 清單。"""
    bbox = boundary_curve.GetBoundingBox(True)
    expanded = rg.BoundingBox(bbox.Min, bbox.Max)
    expanded.Inflate(max(bbox.Diagonal.Length * 0.001, 0.01))

    results = []
    for poly_uv in polys_uv:
        if len(poly_uv) < 3:
            continue
        pts3d = [grid_plane.PointAt(u, v) for (u, v) in poly_uv]
        cbb = rg.BoundingBox(pts3d)
        if not _bbox_overlap(cbb, expanded):
            continue
        pts3d.append(pts3d[0])
        try:
            tile_curve = rg.PolylineCurve(pts3d)
        except Exception:
            continue
        if tile_curve is None or not tile_curve.IsValid:
            continue

        try:
            pieces = _boolean_intersection(tile_curve, boundary_curve)
        except Exception:
            pieces = None
        if not pieces:
            continue
        for c in pieces:
            amp = rg.AreaMassProperties.Compute(c)
            if amp and amp.Area > 1e-4:
                results.append(c)
    return results
