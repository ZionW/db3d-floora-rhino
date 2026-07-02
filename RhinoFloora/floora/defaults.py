# encoding: utf-8
"""圖案清單、中文名稱、分類與每個圖案的預設參數（比照原 SketchUp 版 defaults.rb）。"""

PATTERNS = [
    'Brick', 'Tile', 'Wood', 'Wedge',
    'Tweed', 'Hbone', 'Chevrn', 'BsktWv',
    'HpScth1', 'HpScth2', 'HpScth3', 'HpScth4',
    'Hexgon', 'Octgon', 'I_Block', 'Diamonds', 'FanTil', 'IrPoly',
]

NAMES_TW = {
    'Brick':    u'磚縫鋪',
    'Tile':     u'對縫鋪',
    'Wood':     u'隨作鋪',
    'Wedge':    u'楔形',
    'Tweed':    u'斜紋鋪',
    'Hbone':    u'人字鋪',
    'Chevrn':   u'魚骨拼',
    'BsktWv':   u'編織紋',
    'HpScth1':  u'大小格子1',
    'HpScth2':  u'大小格子2',
    'HpScth3':  u'大小格子3',
    'HpScth4':  u'大小格子4',
    'Hexgon':   u'六邊形',
    'Octgon':   u'八邊形',
    'I_Block':  u'工字塊',
    'Diamonds': u'鑽石紋',
    'FanTil':   u'扇形磚',
    'IrPoly':   u'不規則多邊形',
}

CATEGORIES_TW = [
    (u'基礎', ['Brick', 'Tile', 'Wood', 'Wedge']),
    (u'編織', ['Tweed', 'Hbone', 'Chevrn', 'BsktWv']),
    (u'格子', ['HpScth1', 'HpScth2', 'HpScth3', 'HpScth4']),
    (u'幾何', ['Hexgon', 'Octgon', 'I_Block', 'Diamonds', 'FanTil', 'IrPoly']),
]

START_POINT_OPTIONS = [
    (u'轉角', 'corner'),
    (u'中心', 'center'),
    (u'點選', 'pick'),
]

# 這些圖案的縫寬對外觀影響很小/機制不同，介面上不顯示縫寬欄位
NO_GW_PATTERNS = set(['IrPoly'])
# 這些圖案不使用寬度 gy（只靠 gx 控制單一尺寸），介面上隱藏寬度欄位
NO_GY_PATTERNS = set(['Hexgon', 'Octgon', 'Diamonds', 'FanTil', 'IrPoly',
                       'HpScth1', 'HpScth2', 'HpScth3', 'HpScth4'])

# 單位：長度/寬度/縫寬/縫深皆為模型單位（建議在公分單位的檔案中使用）
_BASE = {
    'spt': 'corner', 'rot': 0.0, 'gx': 30.0, 'gy': 30.0,
    'gw': 0.3, 'gd': 0.3, 'r2r': 0.0, 'twa': 30.0, 'bwb': 3,
}

_OVERRIDES = {
    'Brick':    {'gx': 20.0, 'gy': 10.0, 'r2r': 50.0},
    'Tile':     {'gx': 30.0, 'gy': 30.0, 'r2r': 0.0},
    'Wood':     {'gx': 90.0, 'gy': 9.0},
    'Wedge':    {'gx': 20.0, 'gy': 10.0, 'r2r': 50.0},
    'Tweed':    {'gx': 20.0, 'gy': 10.0, 'twa': 30.0},
    'Hbone':    {'gx': 20.0, 'gy': 5.0},
    'Chevrn':   {'gx': 40.0, 'gy': 8.0},
    'BsktWv':   {'gx': 20.0, 'gy': 10.0, 'bwb': 3},
    'HpScth1':  {'gx': 10.0, 'spt': 'center'},
    'HpScth2':  {'gx': 10.0, 'spt': 'center'},
    'HpScth3':  {'gx': 10.0, 'spt': 'center'},
    'HpScth4':  {'gx': 10.0, 'spt': 'center'},
    'Hexgon':   {'gx': 15.0, 'spt': 'center'},
    'Octgon':   {'gx': 15.0, 'spt': 'center'},
    'I_Block':  {'gx': 20.0, 'gy': 10.0},
    'Diamonds': {'gx': 15.0, 'spt': 'center'},
    'FanTil':   {'gx': 15.0, 'spt': 'center'},
    'IrPoly':   {'gx': 20.0, 'gw': 0.5, 'spt': 'center'},
}


def defaults_for(pattern_id):
    d = dict(_BASE)
    d.update(_OVERRIDES.get(pattern_id, {}))
    return d
