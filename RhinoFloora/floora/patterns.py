# encoding: utf-8
"""12 種圖案的生成演算法（比照原 SketchUp 版 patterns.rb 對應圖案的效果，
以 RhinoCommon 重新實作，非逐行翻譯）。

所有圖案都在選取面所在平面的局部 UV 座標中生成，再裁切到面的邊界內。
"""

import math
import random

import Rhino.Geometry as rg

from . import geometry as geo

MAX_GRID_STEPS = 140

# 這些圖案在方格網格上額外疊加一個整體旋轉角度，做出對角紋理的視覺效果
PATTERN_EXTRA_ROT = {
    'Diamonds': 45.0,
    'Hbone': 45.0,
    'Chevrn': 45.0,
}


def _merge(opts, **kw):
    d = dict(opts)
    d.update(kw)
    return d


# ---- 各圖案的「單一格子」產生函式：輸入格子索引 (i, j)，輸出一或多個 UV 多邊形 ----

def cell_offset_rect(i, j, gx, gy, gw, opts):
    r2r = float(opts.get('r2r', 0.0)) / 100.0
    shift = (j * r2r * gx) % gx if gx else 0.0
    u0 = i * gx - shift
    v0 = j * gy
    return [geo.rect(u0, v0, gx, gy, gw / 2.0)]


def cell_wedge(i, j, gx, gy, gw, opts):
    r2r = float(opts.get('r2r', 0.0)) / 100.0
    shift = (j * r2r * gx) % gx if gx else 0.0
    u0 = i * gx - shift
    v0 = j * gy
    p0, p1, p2, p3 = geo.rect(u0, v0, gx, gy, gw / 2.0)
    return [[p0, p1, p2], [p0, p2, p3]]


def cell_basketweave(i, j, gx, gy, gw, opts):
    bwb = max(2, int(opts.get('bwb', 3)))
    block = bwb * gy
    u0 = i * block
    v0 = j * block
    horizontal = (i + j) % 2 == 0
    hw = gw / 2.0
    polys = []
    for k in range(bwb):
        if horizontal:
            polys.append(geo.rect(u0, v0 + k * gy, block, gy, hw))
        else:
            polys.append(geo.rect(u0 + k * gy, v0, gy, block, hw))
    return polys


def cell_hexagon(i, j, gx, gy, gw, opts):
    s = gx / 2.0
    apothem = s * math.sqrt(3) / 2.0
    inset = (gw / 2.0) / math.cos(math.pi / 6.0)
    r = max(s - inset, 0.01)
    col_w = 1.5 * s
    row_h = 2 * apothem
    cu = i * col_w
    cv = j * row_h + (apothem if i % 2 != 0 else 0.0)
    pts = []
    for k in range(6):
        ang = k * math.pi / 3.0  # flat-top hexagon, matches col_w/row_h spacing below
        pts.append((cu + r * math.cos(ang), cv + r * math.sin(ang)))
    return [pts]


def cell_octagon(i, j, gx, gy, gw, opts):
    cell = gx + gw
    apothem = gx / 2.0
    r = apothem / math.cos(math.pi / 8.0)
    cu = i * cell
    cv = j * cell
    pts = []
    for k in range(8):
        ang = math.pi / 8.0 + k * math.pi / 4.0
        pts.append((cu + r * math.cos(ang), cv + r * math.sin(ang)))
    return [pts]


def cell_iblock(i, j, gx, gy, gw, opts):
    shift = (j % 2) * (gx / 2.0)
    u0 = i * gx - shift
    v0 = j * gy
    hw = gw / 2.0
    nd = gy * 0.22
    nw = gx * 0.28
    x0, x1 = u0 + hw, u0 + gx - hw
    y0, y1 = v0 + hw, v0 + gy - hw
    ym0, ym1 = y0 + nd, y1 - nd
    if ym0 >= ym1 or nw * 2 >= (x1 - x0):
        return [geo.rect(u0, v0, gx, gy, hw)]
    pts = [
        (x0, y0), (x0 + nw, y0), (x0 + nw, ym0), (x1 - nw, ym0), (x1 - nw, y0),
        (x1, y0), (x1, y1),
        (x1 - nw, y1), (x1 - nw, ym1), (x0 + nw, ym1), (x0 + nw, y1),
        (x0, y1),
    ]
    return [pts]


def cell_diamond(i, j, gx, gy, gw, opts):
    return cell_offset_rect(i, j, gx, gx, gw, _merge(opts, r2r=0.0))


def cell_fantile(i, j, gx, gy, gw, opts):
    """扇形磚（魚鱗/扇貝紋）：半圓形磚片，隔行錯位排列使弧邊相互交扣。"""
    r = gx
    row_h = r
    shift = (j % 2) * r
    cu = i * 2 * r - shift
    cv = j * row_h
    hw = gw / 2.0
    segs = 16
    rad = max(r - hw, 0.05)
    center = (cu, cv + row_h)
    pts = []
    for k in range(segs + 1):
        ang = math.pi - (math.pi * k / segs)
        pts.append((center[0] + rad * math.cos(ang), center[1] - rad * math.sin(ang)))
    return [pts]


# 「大小格子」系列：一個週期內放一片大方磚＋數片小方磚，(col, row, span) 皆以「小格」為單位。
HOPSCOTCH_LAYOUTS = {
    'HpScth1': (3, [(0, 0, 2)], [(2, 0, 1), (2, 1, 1), (0, 2, 1), (1, 2, 1), (2, 2, 1)]),
    'HpScth2': (3, [(1, 1, 2)], [(0, 0, 1), (1, 0, 1), (2, 0, 1), (0, 1, 1), (0, 2, 1)]),
    'HpScth3': (3, [(0, 1, 2)], [(0, 0, 1), (1, 0, 1), (2, 0, 1), (2, 1, 1), (2, 2, 1)]),
    'HpScth4': (4, [(0, 0, 3)], [(3, 0, 1), (3, 1, 1), (3, 2, 1), (0, 3, 1), (1, 3, 1), (2, 3, 1), (3, 3, 1)]),
}


def cell_hopscotch(i, j, gx, gy, gw, opts, pattern_id):
    period, big_list, small_list = HOPSCOTCH_LAYOUTS[pattern_id]
    unit = gx
    hw = gw / 2.0
    u0 = i * period * unit
    v0 = j * period * unit
    polys = []
    for (col, row, span) in (big_list + small_list):
        polys.append(geo.rect(u0 + col * unit, v0 + row * unit, span * unit, span * unit, hw))
    return polys


def _make_hopscotch_fn(pattern_id):
    def fn(i, j, gx, gy, gw, opts):
        return cell_hopscotch(i, j, gx, gy, gw, opts, pattern_id)
    return fn


PATTERN_CELL_FUNCS = {
    'Brick':   cell_offset_rect,
    'Tile':    cell_offset_rect,
    'Wedge':   cell_wedge,
    'Hbone':   lambda i, j, gx, gy, gw, opts: cell_offset_rect(i, j, gx, gy, gw, _merge(opts, r2r=50.0)),
    'Chevrn':  lambda i, j, gx, gy, gw, opts: cell_offset_rect(i, j, gx, gy, gw, _merge(opts, r2r=50.0)),
    'BsktWv':  cell_basketweave,
    'Hexgon':  cell_hexagon,
    'Octgon':  cell_octagon,
    'I_Block': cell_iblock,
    'Diamonds': cell_diamond,
    'FanTil':  cell_fantile,
}
for _pid in HOPSCOTCH_LAYOUTS:
    PATTERN_CELL_FUNCS[_pid] = _make_hopscotch_fn(_pid)


def _grid_count(diag, step):
    step = max(step, 0.1)
    return min(int(diag / step) + 3, MAX_GRID_STEPS)


def _wood_polys(diag, gx, gy, gw):
    hw = gw / 2.0
    rows = min(int(diag / gy) + 3, MAX_GRID_STEPS)
    polys = []
    for r in range(-rows, rows + 1):
        v0 = r * gy
        u = -diag - random.uniform(0, gx)
        limit = diag * 1.2
        guard = 0
        while u < limit and guard < 4000:
            length = gx * random.uniform(0.55, 1.5)
            polys.append(geo.rect(u, v0, length, gy, hw))
            u += length
            guard += 1
    return polys


def _tweed_polys(diag, gx, gy, gw, twa):
    hw = gw / 2.0
    shear = math.tan(math.radians(twa))
    n = _grid_count(diag, min(gx, gy))
    polys = []
    for i in range(-n, n + 1):
        for j in range(-n, n + 1):
            r = geo.rect(i * gx, j * gy, gx, gy, hw)
            polys.append([(u + v * shear, v) for (u, v) in r])
    return polys


# ---- 不規則多邊形（IrPoly）：以 Voronoi 圖為基礎，半平面裁切法直接算出各細胞多邊形 ----

IRPOLY_MAX_N = 45
IRPOLY_NEIGHBOR_WINDOW = 4


def _line_intersect(p1, p2, mx, my, nx, ny):
    dx, dy = p2[0] - p1[0], p2[1] - p1[1]
    denom = dx * nx + dy * ny
    if abs(denom) < 1e-12:
        return p1
    t = ((mx - p1[0]) * nx + (my - p1[1]) * ny) / denom
    t = max(0.0, min(1.0, t))
    return (p1[0] + t * dx, p1[1] + t * dy)


def _clip_halfplane(poly, site, other):
    """保留多邊形中「離 site 比離 other 近」的那一半（用垂直平分線裁切）。"""
    mx, my = (site[0] + other[0]) / 2.0, (site[1] + other[1]) / 2.0
    nx, ny = other[0] - site[0], other[1] - site[1]

    def inside(pt):
        return (pt[0] - mx) * nx + (pt[1] - my) * ny <= 0

    result = []
    n = len(poly)
    for idx in range(n):
        cur = poly[idx]
        prev = poly[idx - 1]
        cur_in = inside(cur)
        prev_in = inside(prev)
        if cur_in:
            if not prev_in:
                result.append(_line_intersect(prev, cur, mx, my, nx, ny))
            result.append(cur)
        elif prev_in:
            result.append(_line_intersect(prev, cur, mx, my, nx, ny))
    return result


def _line_line_intersect(x1, y1, dx1, dy1, x2, y2, dx2, dy2):
    denom = dx1 * dy2 - dy1 * dx2
    if abs(denom) < 1e-12:
        return None
    t = ((x2 - x1) * dy2 - (y2 - y1) * dx2) / denom
    return (x1 + t * dx1, y1 + t * dy1)


def _inset_polygon(poly, dist):
    """將凸多邊形（Voronoi 細胞必為凸）整體內縮 dist，用於留出縫寬。"""
    if dist <= 0 or len(poly) < 3:
        return poly
    n = len(poly)
    area2 = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    if area2 < 0:
        poly = poly[::-1]
        n = len(poly)
    edges = []
    for i in range(n):
        p1 = poly[i]
        p2 = poly[(i + 1) % n]
        dx, dy = p2[0] - p1[0], p2[1] - p1[1]
        length = math.hypot(dx, dy)
        if length < 1e-9:
            continue
        nx, ny = dy / length, -dx / length
        ox, oy = p1[0] - nx * dist, p1[1] - ny * dist
        edges.append((ox, oy, dx, dy))
    if len(edges) < 3:
        return None
    new_pts = []
    m = len(edges)
    for i in range(m):
        ox1, oy1, dx1, dy1 = edges[i - 1]
        ox2, oy2, dx2, dy2 = edges[i]
        pt = _line_line_intersect(ox1, oy1, dx1, dy1, ox2, oy2, dx2, dy2)
        if pt is None:
            return None
        new_pts.append(pt)
    return new_pts


def _irregular_polygon_polys(diag, gx, gw):
    step = max(gx, 1.0)
    n = min(int(diag / step) + 2, IRPOLY_MAX_N)
    win = IRPOLY_NEIGHBOR_WINDOW

    grid_pts = {}
    for i in range(-n, n + 1):
        for j in range(-n, n + 1):
            jx = random.uniform(-0.35, 0.35) * step
            jy = random.uniform(-0.35, 0.35) * step
            grid_pts[(i, j)] = (i * step + jx, j * step + jy)

    half = diag * 1.3
    bound = [(-half, -half), (half, -half), (half, half), (-half, half)]
    hw = gw / 2.0

    polys = []
    for (i, j), p in grid_pts.items():
        cell = list(bound)
        stop = False
        for di in range(-win, win + 1):
            for dj in range(-win, win + 1):
                if di == 0 and dj == 0:
                    continue
                q = grid_pts.get((i + di, j + dj))
                if q is None:
                    continue
                cell = _clip_halfplane(cell, p, q)
                if not cell:
                    stop = True
                    break
            if stop:
                break
        if cell and len(cell) >= 3:
            inset = _inset_polygon(cell, hw)
            if inset:
                polys.append(inset)
    return polys


def _resolve_origin(boundary_curve, plane, bbox, spt, anchor_pt):
    if spt == 'pick' and anchor_pt is not None:
        return anchor_pt
    if spt == 'center':
        return geo.centroid(boundary_curve)
    ok, u0, v0 = plane.ClosestParameter(bbox.Min)
    if not ok:
        return bbox.Min
    return plane.PointAt(u0, v0)


def generate(pattern_id, boundary_curve, plane, opts, anchor_pt=None):
    """主要進入點：依圖案 ID、選取的邊界/平面與選項，回傳裁切好的磁磚 Curve 清單。"""
    gx = float(opts.get('gx', 30.0))
    gy = float(opts.get('gy', 30.0))
    gw = float(opts.get('gw', 0.3))
    rot = float(opts.get('rot', 0.0)) + PATTERN_EXTRA_ROT.get(pattern_id, 0.0)
    spt = opts.get('spt', 'corner')

    if gx <= 0 or gy <= 0:
        raise ValueError(u"長度與寬度必須大於 0")

    bbox = boundary_curve.GetBoundingBox(True)
    diag = bbox.Diagonal.Length
    if diag <= 0:
        raise ValueError(u"無法取得選取物件的邊界")

    origin = _resolve_origin(boundary_curve, plane, bbox, spt, anchor_pt)
    grid_plane = rg.Plane(origin, plane.XAxis, plane.YAxis)
    if rot:
        grid_plane.Rotate(math.radians(rot), grid_plane.ZAxis)

    if pattern_id == 'Wood':
        raw = _wood_polys(diag, gx, gy, gw)
    elif pattern_id == 'Tweed':
        raw = _tweed_polys(diag, gx, gy, gw, float(opts.get('twa', 30.0)))
    elif pattern_id == 'IrPoly':
        raw = _irregular_polygon_polys(diag, gx, gw)
    else:
        cell_fn = PATTERN_CELL_FUNCS.get(pattern_id)
        if cell_fn is None:
            raise ValueError(u"未支援的圖案：%s" % pattern_id)
        cell_gx, cell_gy = gx, gy
        if pattern_id == 'BsktWv':
            bwb = max(2, int(opts.get('bwb', 3)))
            cell_gx = cell_gy = bwb * gy
        elif pattern_id == 'Diamonds':
            cell_gx = cell_gy = gx
        elif pattern_id == 'Hexgon':
            cell_gx = cell_gy = 1.5 * (gx / 2.0)
        elif pattern_id == 'Octgon':
            cell_gx = cell_gy = gx + gw
        elif pattern_id == 'FanTil':
            cell_gx, cell_gy = 2.0 * gx, gx
        elif pattern_id in HOPSCOTCH_LAYOUTS:
            period = HOPSCOTCH_LAYOUTS[pattern_id][0]
            cell_gx = cell_gy = period * gx

        n = _grid_count(diag, min(cell_gx, cell_gy))
        raw = []
        for i in range(-n, n + 1):
            for j in range(-n, n + 1):
                raw.extend(cell_fn(i, j, gx, gy, gw, opts))

    if len(raw) > 30000:
        raise ValueError(u"磚片數量過多，請放大磚片尺寸或縮小選取範圍")

    return geo.clip_polys_to_boundary(raw, grid_plane, boundary_curve)
