# encoding: utf-8
"""材質/用量計算機（比照原 SketchUp 版 calculator.rb）。
掃描目前文件中所有 Floora_ 開頭圖層的物件，統計每個圖層的片數與面積，
並依使用者輸入的磚片尺寸/耗損率/每箱片數/單價估算所需片數、箱數與價格。
"""

import codecs
import math

import Rhino
import Rhino.Geometry as rg

M2_PER_PING = 3.30579


def layer_area_stats(prefix=u"Floora_", exclude=(u"Floora_Preview",)):
    doc = Rhino.RhinoDoc.ActiveDoc
    stats = {}
    for obj in doc.Objects:
        try:
            layer = doc.Layers[obj.Attributes.LayerIndex]
        except Exception:
            continue
        name = layer.Name if layer else u""
        if not name.startswith(prefix) or name in exclude:
            continue
        try:
            amp = rg.AreaMassProperties.Compute(obj.Geometry)
        except Exception:
            amp = None
        area = amp.Area if amp else 0.0
        if area <= 0:
            continue
        entry = stats.setdefault(name, {'count': 0, 'area': 0.0})
        entry['count'] += 1
        entry['area'] += area

    scale = _sqr_unit_to_sqm(doc)
    results = []
    for name, v in stats.items():
        area_m2 = v['area'] * scale
        results.append({
            'name': name,
            'count': v['count'],
            'area_m2': round(area_m2, 4),
            'area_ping': round(area_m2 / M2_PER_PING, 4),
        })
    results.sort(key=lambda r: -r['area_m2'])
    return results


def _sqr_unit_to_sqm(doc):
    to_m = Rhino.RhinoMath.UnitScale(doc.ModelUnitSystem, Rhino.UnitSystem.Meters)
    return to_m * to_m


def estimate(area_m2, tile_len_cm, tile_wid_cm, waste_pct, pcs_per_box, price_per_box, price_per_ping):
    tile_area_m2 = (tile_len_cm / 100.0) * (tile_wid_cm / 100.0)
    if tile_area_m2 <= 0:
        return None
    needed_area = area_m2 * (1.0 + waste_pct / 100.0)
    tile_count = int(math.ceil(needed_area / tile_area_m2))
    boxes = int(math.ceil(tile_count / pcs_per_box)) if pcs_per_box else 0
    price_by_box = boxes * price_per_box
    price_by_ping = (area_m2 / M2_PER_PING) * price_per_ping
    return {
        'needed_area_m2': round(needed_area, 4),
        'tile_count': tile_count,
        'boxes': boxes,
        'price_by_box': round(price_by_box, 2),
        'price_by_ping': round(price_by_ping, 2),
    }


def write_csv(path, stats, estimate_result):
    """把圖層統計＋估算結果寫成 CSV（帶 BOM，Excel 開啟中文不會亂碼）。"""
    lines = []
    lines.append(u"圖層,片數,面積(m2),面積(坪)")
    total_area = 0.0
    total_ping = 0.0
    for s in stats:
        lines.append(u"%s,%d,%.4f,%.4f" % (s['name'], s['count'], s['area_m2'], s['area_ping']))
        total_area += s['area_m2']
        total_ping += s['area_ping']
    lines.append(u"總計,,%.4f,%.4f" % (total_area, total_ping))

    if estimate_result:
        lines.append(u"")
        lines.append(u"項目,數值")
        lines.append(u"含耗損面積(m2),%.4f" % estimate_result['needed_area_m2'])
        lines.append(u"需求片數,%d" % estimate_result['tile_count'])
        lines.append(u"需求箱數,%d" % estimate_result['boxes'])
        lines.append(u"估價(依箱),%.0f" % estimate_result['price_by_box'])
        lines.append(u"估價(依坪),%.0f" % estimate_result['price_by_ping'])

    with codecs.open(path, 'w', encoding='utf-8-sig') as f:
        f.write(u"\r\n".join(lines))
