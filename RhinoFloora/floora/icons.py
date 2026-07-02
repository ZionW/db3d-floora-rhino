# encoding: utf-8
"""為每種圖案取樣出一組縮小版的 UV 多邊形，供選圖案的縮圖使用。
直接重用 patterns.py 的純數學生成函式（不依賴 Rhino），確保縮圖跟實際產生的圖案長相一致。
"""

import math

from . import patterns as pat
from . import defaults as df

VIEW = 20.0  # 縮圖取樣的座標範圍為 -VIEW..VIEW
TARGET_CELL = 17.0  # 讓縮圖裡大約只看得到 2x2 顆磚，不要重複太多次

def sample_polygons(pattern_id):
    opts = df.defaults_for(pattern_id)
    base_dim = opts.get('gy') or opts['gx']
    scale = TARGET_CELL / max(opts['gx'], base_dim, 1.0)
    gx = opts['gx'] * scale
    gy = base_dim * scale
    # 長寬比過於懸殊時（例如隨作鋪的長條木地板）縮圖會顯得過於稀疏，
    # 這裡只在縮圖取樣時放寬比例，不影響實際產生的圖案。
    gy = max(gy, gx / 6.0)
    gw = max(opts['gw'] * scale, 0.5)

    diag = VIEW * 1.15

    if pattern_id == 'Wood':
        raw = pat._wood_polys(diag, gx, gy, gw)
    elif pattern_id == 'Tweed':
        raw = pat._tweed_polys(diag, gx, gy, gw, opts.get('twa', 30.0))
    elif pattern_id == 'IrPoly':
        raw = pat._irregular_polygon_polys(diag, gx, gw)
    else:
        fn = pat.PATTERN_CELL_FUNCS.get(pattern_id)
        if fn is None:
            return []
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
        elif pattern_id in pat.HOPSCOTCH_LAYOUTS:
            period = pat.HOPSCOTCH_LAYOUTS[pattern_id][0]
            cell_gx = cell_gy = period * gx
        n = int(diag / max(min(cell_gx, cell_gy), 1.0)) + 2
        raw = []
        for i in range(-n, n + 1):
            for j in range(-n, n + 1):
                raw.extend(fn(i, j, gx, gy, gw, opts))

    extra_rot = pat.PATTERN_EXTRA_ROT.get(pattern_id, 0.0)
    if extra_rot:
        r = math.radians(extra_rot)
        c, s = math.cos(r), math.sin(r)
        raw = [[(x * c - y * s, x * s + y * c) for (x, y) in poly] for poly in raw]

    # 只保留落在取樣範圍附近的多邊形，避免縮圖畫出太多用不到的磚片
    kept = []
    pad = VIEW + 2.0
    for poly in raw:
        cx = sum(p[0] for p in poly) / len(poly)
        cy = sum(p[1] for p in poly) / len(poly)
        if -pad <= cx <= pad and -pad <= cy <= pad:
            kept.append(poly)
    return kept
