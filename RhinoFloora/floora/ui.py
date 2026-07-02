# encoding: utf-8
"""中文浮動操作介面（比照原 SketchUp 版 dialog.rb + dialog.html / calculator.rb + calculator.html）。
使用 Eto.Forms 建立非模態、永遠置頂的視窗，讓使用者在點選「產生磚塊」後於 Rhino 視圖中
選取一個平面（Brep 面或封閉平面曲線），依目前設定生成磁磚圖案。

介面採白底＋藍色點綴的簡潔風格：圖案選單以實際拼貼演算法畫出的小縮圖取代文字下拉選單，
視窗不強制固定尺寸、不包 Scrollable，讓內容自然決定高度，避免出現滾動軸。

版面用「外層垂直 StackLayout ＋ 每列各自一個水平 StackLayout」組成，
不用 DynamicLayout（不同列欄位數不一致時，DynamicLayout 的共用欄寬機制會讓視窗寬度暴增）。
"""

import math
import random

import System
import System.Drawing as sysdraw

import Rhino
import Rhino.Geometry as rg
import Rhino.Input.Custom as ric

import Eto.Forms as forms
import Eto.Drawing as drawing

from . import defaults as df
from . import patterns as pat
from . import geometry as geo
from . import calculator as calc
from . import storage as store
from . import icons
from . import materials

MAX_UNDO = 3

PAINT_MODES = [(u"目前材質", 'current'), (u"自訂顏色", 'custom_color'), (u"貼圖材質", 'texture')]
IMAGE_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.bmp', '.tif', '.tiff']
DEFAULT_TILE_COLOR = drawing.Color.FromArgb(0xD3, 0xD3, 0xD3)  # 預設磚色：淺灰色
THUMB_SIZE = 44
MAX_TEXTURES = 5  # 縮圖固定顯示這麼多格（含空格），選幾張圖片都不會讓視窗跟著變長變短
THUMB_COLS = MAX_TEXTURES

INCREMENTS = {'gx': 1.0, 'gy': 1.0, 'gw': 0.1, 'gd': 0.1, 'r2r': 5.0, 'twa': 1.0}

# ---------------------------------------------------------------- 設計代幣：日本傳統色命名色票
# 只定義這 7 個具名色票，其餘 UI 狀態色（hover/選中的圖示描邊等）都是這幾個色票的深淺衍生，
# 不再另外亂加顏色，維持整體配色統一。
TOKEN_SHIRONERI = drawing.Color.FromArgb(0xFC, 0xFC, 0xFA)  # 白練 - 視窗底色
TOKEN_USUZUMI   = drawing.Color.FromArgb(0xF4, 0xF5, 0xF6)  # 薄墨 - 卡片/面板底色
TOKEN_HAIZAKURA = drawing.Color.FromArgb(0xE4, 0xE7, 0xEB)  # 灰櫻 - 邊框、分隔線
TOKEN_HANADA    = drawing.Color.FromArgb(0x2A, 0x5F, 0x8A)  # 縹色 - 主強調藍（標題／選中／主按鈕）
TOKEN_ASAGI     = drawing.Color.FromArgb(0xDC, 0xEA, 0xF3)  # 浅葱 - 選中時的淺藍底
TOKEN_SUMI      = drawing.Color.FromArgb(0x33, 0x38, 0x3F)  # 墨色 - 主要文字
TOKEN_NEZUMI    = drawing.Color.FromArgb(0x8B, 0x94, 0xA0)  # 鼠色 - 次要/說明文字

# 語意化別名：其餘程式碼一律用這些名稱，方便之後整組換色
C_BG           = TOKEN_SHIRONERI
C_PANEL        = TOKEN_USUZUMI
C_BORDER       = TOKEN_HAIZAKURA
C_ACCENT       = TOKEN_HANADA
C_ACCENT_LIGHT = TOKEN_ASAGI
C_ACCENT_HOVER = drawing.Color.FromArgb(0x6F, 0x9C, 0xC2)  # 縹色與邊框之間的中間色，滑鼠移入時用
C_TEXT         = TOKEN_SUMI
C_TEXT_SUB     = TOKEN_NEZUMI
C_ICON_FILL       = drawing.Color.FromArgb(0xA6, 0xC2, 0xD8)
C_ICON_FILL_SEL   = TOKEN_HANADA
C_ICON_STROKE     = drawing.Color.FromArgb(0x78, 0x96, 0xB4)
C_ICON_STROKE_SEL = drawing.Color.FromArgb(0x15, 0x38, 0x60)

# ---------------------------------------------------------------- 字級系統
# 全部視窗（主介面／材料計算機／外掛說明）統一用這 5 級字級，不再各自寫死大小，
# 標題 > 區塊標籤 > 小標題 > 內文 > 說明文字，數字越小字級越小。
FONT_SPECS = {
    'title':   (11, True),   # 視窗大標題
    'section': (9, True),    # 區塊標籤（圖案／材質／使用方式…）
    'subhead': (8, True),    # 區塊內的小分組標題
    'body':    (8, False),   # 一般內文、欄位標籤
    'caption': (7, False),   # 縮圖文字、狀態列等輔助說明
}


def _font(key):
    size, bold = FONT_SPECS[key]
    style = drawing.FontStyle.Bold if bold else drawing.FontStyle.NONE
    return drawing.Font(drawing.FontFamilies.Sans, size, style)


# 欄位標籤統一寬度，讓每一列的輸入元件對齊成一欄，畫面比較整齊
FIELD_LABEL_WIDTH = 70

PALETTE = {
    'Brick':    sysdraw.Color.FromArgb(196, 120, 80),
    'Tile':     sysdraw.Color.FromArgb(200, 200, 200),
    'Wood':     sysdraw.Color.FromArgb(150, 110, 60),
    'Wedge':    sysdraw.Color.FromArgb(170, 150, 120),
    'Tweed':    sysdraw.Color.FromArgb(180, 160, 140),
    'Hbone':    sysdraw.Color.FromArgb(160, 120, 90),
    'Chevrn':   sysdraw.Color.FromArgb(140, 100, 80),
    'BsktWv':   sysdraw.Color.FromArgb(120, 140, 110),
    'HpScth1':  sysdraw.Color.FromArgb(170, 130, 150),
    'HpScth2':  sysdraw.Color.FromArgb(170, 130, 150),
    'HpScth3':  sysdraw.Color.FromArgb(170, 130, 150),
    'HpScth4':  sysdraw.Color.FromArgb(170, 130, 150),
    'Hexgon':   sysdraw.Color.FromArgb(100, 140, 160),
    'Octgon':   sysdraw.Color.FromArgb(150, 150, 180),
    'I_Block':  sysdraw.Color.FromArgb(130, 130, 130),
    'Diamonds': sysdraw.Color.FromArgb(180, 140, 180),
    'FanTil':   sysdraw.Color.FromArgb(120, 160, 190),
    'IrPoly':   sysdraw.Color.FromArgb(140, 160, 140),
}

# 縮圖上顯示的簡短名稱（比完整名稱短，避免小縮圖擠不下）
TILE_LABELS = {
    'HpScth1': u'格子1', 'HpScth2': u'格子2', 'HpScth3': u'格子3', 'HpScth4': u'格子4',
    'IrPoly': u'不規則',
}

TILE_W, TILE_H = 48, 54
ICON_AREA = 26
GRID_COLS = 6

_instance = None


def show():
    global _instance
    if _instance is not None and not _instance.IsDisposed:
        _instance.BringToFront()
        return
    _instance = FlooraForm()
    _instance.Show()


def _color_for(pattern_id):
    return PALETTE.get(pattern_id, sysdraw.Color.FromArgb(180, 180, 180))


def _label(text, font=None, color=None, align=None):
    lbl = forms.Label()
    lbl.Text = text
    if font is not None:
        lbl.Font = font
    if color is not None:
        lbl.TextColor = color
    if align is not None:
        lbl.TextAlignment = align
    return lbl


def _section_label(text, font, color):
    return _label(text, font, color, align=forms.TextAlignment.Left)


def _button(text, primary=False):
    btn = forms.Button()
    btn.Text = text
    if primary:
        try:
            btn.BackgroundColor = C_ACCENT
            btn.TextColor = drawing.Colors.White
        except Exception:
            pass
    return btn


def _hr():
    p = forms.Panel()
    p.Height = 1
    p.BackgroundColor = C_BORDER
    return p


def _vstack():
    s = forms.StackLayout()
    s.Orientation = forms.Orientation.Vertical
    s.Spacing = 3
    s.HorizontalContentAlignment = forms.HorizontalAlignment.Stretch
    return s


def _hrow(*controls):
    """水平排一列控制項；傳 None 會變成可伸縮的空白區塊（讓其餘控制項靠左對齊，不被拉伸）。"""
    s = forms.StackLayout()
    s.Orientation = forms.Orientation.Horizontal
    s.Spacing = 4
    for c in controls:
        if c is None:
            s.Items.Add(forms.StackLayoutItem(forms.Panel(), True))
        else:
            s.Items.Add(forms.StackLayoutItem(c))
    return s


def _add_row(outer, control):
    # 注意：這裡故意不傳 expand=True。StackLayoutItem 的 expand 是沿著主軸（垂直）方向，
    # 若視窗有多餘空間，expand=True 的每一列都會被拉伸（連 1px 的分隔線、按鈕都會被拉高），
    # 之前材質計算機的分隔線/計算按鈕過高就是這個原因。維持預設（不 expand）才會保持原本高度。
    outer.Items.Add(forms.StackLayoutItem(control))


def _find_layer(doc, name):
    for lyr in doc.Layers:
        if not lyr.IsDeleted and lyr.Name == name:
            return lyr.Index
    return -1


def _pattern_layer(doc, pattern_id):
    name = u"Floora_%s" % df.NAMES_TW.get(pattern_id, pattern_id)
    idx = _find_layer(doc, name)
    if idx >= 0:
        return idx
    new_layer = Rhino.DocObjects.Layer()
    new_layer.Name = name
    new_layer.Color = _color_for(pattern_id)
    return doc.Layers.Add(new_layer)


def _preview_layer(doc):
    name = u"Floora_Preview"
    idx = _find_layer(doc, name)
    if idx >= 0:
        return idx
    new_layer = Rhino.DocObjects.Layer()
    new_layer.Name = name
    new_layer.Color = sysdraw.Color.FromArgb(0x2A, 0x5F, 0x8A)
    return doc.Layers.Add(new_layer)


def _to_sys_color(eto_color):
    return sysdraw.Color.FromArgb(eto_color.Rb, eto_color.Gb, eto_color.Bb)


def _offset_inward(curve, plane, dist, tol):
    """把封閉平面曲線往內縮 dist（挑面積變小的那個方向），失敗回傳 None。"""
    orig_amp = rg.AreaMassProperties.Compute(curve)
    orig_area = orig_amp.Area if orig_amp else None
    best, best_area = None, None
    for sign in (1.0, -1.0):
        try:
            cand = curve.Offset(plane, dist * sign, tol, rg.CurveOffsetCornerStyle.Sharp)
        except Exception:
            cand = None
        if not cand:
            continue
        c = cand[0]
        if c is None:
            continue
        if not c.IsClosed:
            c.MakeClosed(tol)
        amp = rg.AreaMassProperties.Compute(c)
        if not amp or amp.Area <= 0:
            continue
        if orig_area is not None and amp.Area >= orig_area:
            continue
        if best_area is None or amp.Area > best_area:
            best, best_area = c, amp.Area
    return best


def _build_beveled_solid(curve, plane, gd, bevel_size, tol):
    """磚片頂緣倒斜角：頂面內縮成一圈斜角，斜角以下維持原本外緣尺寸直到底面。
    任何一步失敗都回傳 None，呼叫端會自動退回原本的直角擠出。"""
    bevel_size = min(bevel_size, gd - tol) if gd > tol else bevel_size
    if bevel_size <= tol:
        return None
    inner = _offset_inward(curve, plane, bevel_size, tol)
    if inner is None:
        return None

    outer_lower = curve.DuplicateCurve()
    outer_lower.Transform(rg.Transform.Translation(-plane.ZAxis * bevel_size))
    outer_bottom = curve.DuplicateCurve()
    outer_bottom.Transform(rg.Transform.Translation(-plane.ZAxis * gd))

    pieces = []
    top_cap = rg.Brep.CreatePlanarBreps(inner, tol)
    if top_cap:
        pieces.extend(top_cap)
    bevel_ring = rg.Brep.CreateFromLoft([inner, outer_lower], rg.Point3d.Unset, rg.Point3d.Unset,
                                         rg.LoftType.Straight, False)
    if bevel_ring:
        pieces.extend(bevel_ring)

    remaining = gd - bevel_size
    if remaining > tol:
        wall_ring = rg.Brep.CreateFromLoft([outer_lower, outer_bottom], rg.Point3d.Unset, rg.Point3d.Unset,
                                            rg.LoftType.Straight, False)
        if wall_ring:
            pieces.extend(wall_ring)
        bottom_cap = rg.Brep.CreatePlanarBreps(outer_bottom, tol)
    else:
        bottom_cap = rg.Brep.CreatePlanarBreps(outer_lower, tol)
    if bottom_cap:
        pieces.extend(bottom_cap)

    if not pieces:
        return None
    try:
        joined = rg.Brep.JoinBreps(pieces, tol)
    except Exception:
        joined = None
    if not joined:
        return None
    return joined[0]


def _apply_random_defect(brep, defect_min, defect_max):
    """隨機小角度傾斜整片磚，模擬鋪貼時常見的高低差／缺陷感。失敗就原樣傳回。"""
    if brep is None or defect_max <= 0:
        return brep
    try:
        bbox = brep.GetBoundingBox(True)
        center = bbox.Center
        axis = rg.Vector3d(random.uniform(-1.0, 1.0), random.uniform(-1.0, 1.0), 0.0)
        if not axis.Unitize():
            return brep
        angle_deg = random.uniform(defect_min, defect_max)
        if angle_deg <= 0:
            return brep
        xf = rg.Transform.Rotation(math.radians(angle_deg), axis, center)
        brep.Transform(xf)
    except Exception:
        pass
    return brep


def _add_tile(doc, curve, layer_index, gd, paint_mode='current', texture_paths=None, custom_color=None,
              bevel_enabled=False, bevel_size=0.3,
              random_defect=False, defect_min=0.0, defect_max=2.0, back_face=False):
    attrs = Rhino.DocObjects.ObjectAttributes()
    attrs.LayerIndex = layer_index

    if paint_mode == 'custom_color' and custom_color is not None:
        try:
            attrs.ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject
            attrs.ObjectColor = custom_color
        except Exception:
            pass
    elif paint_mode == 'texture' and texture_paths:
        try:
            materials.apply_random_texture(doc, attrs, texture_paths)
        except Exception:
            pass

    ok, curve_plane = curve.TryGetPlane(geo.TOL)
    if not ok:
        curve_plane = rg.Plane(curve.PointAtStart, rg.Vector3d.ZAxis)

    geom = None  # 最終要加入文件的幾何：rg.Extrusion 或 rg.Brep

    if gd and gd > 0 and bevel_enabled and bevel_size and bevel_size > 0:
        try:
            geom = _build_beveled_solid(curve, curve_plane, gd, bevel_size, geo.TOL)
        except Exception:
            geom = None
        if geom is not None and not geom.IsValid:
            geom = None

    if geom is None and gd and gd > 0:
        try:
            geom = rg.Extrusion.Create(curve, -gd, True)
        except Exception:
            geom = None

    if geom is None:
        try:
            breps = rg.Brep.CreatePlanarBreps(curve, geo.TOL)
        except Exception:
            breps = None
        if breps:
            geom = breps[0]

    if geom is None:
        return doc.Objects.AddCurve(curve, attrs)

    if random_defect:
        try:
            brep = geom.ToBrep() if hasattr(geom, 'ToBrep') else geom
        except Exception:
            brep = geom
        geom = _apply_random_defect(brep, defect_min, defect_max) or geom

    if isinstance(geom, rg.Extrusion):
        gid = doc.Objects.AddExtrusion(geom, attrs)
    else:
        gid = doc.Objects.AddBrep(geom, attrs)

    if gid == System.Guid.Empty:
        return None

    if back_face and gd and gd > 0:
        try:
            depth_below = gd + (bevel_size if bevel_enabled and bevel_size else 0.0)
            back_curve = curve.DuplicateCurve()
            back_curve.Transform(rg.Transform.Translation(-curve_plane.ZAxis * depth_below))
            back_ext = rg.Extrusion.Create(back_curve, -gd, True)
            if back_ext is not None:
                doc.Objects.AddExtrusion(back_ext, attrs)
        except Exception:
            pass

    return gid


def _pick_face():
    go = ric.GetObject()
    go.SetCommandPrompt(u"請選取要鋪磚的平面（面或封閉平面曲線），按 Enter/Esc 取消")
    go.GeometryFilter = (Rhino.DocObjects.ObjectType.Surface |
                          Rhino.DocObjects.ObjectType.Brep |
                          Rhino.DocObjects.ObjectType.Curve)
    go.SubObjectSelect = True
    result = go.Get()
    if result != Rhino.Input.GetResult.Object:
        return None
    return go.Object(0)


def _rhino_main_window():
    """取得 Rhino 主視窗（Eto Form），用來讓本視窗跟著 Rhino 一起縮到最小/還原。
    優先取得目前文件對應的主視窗（較可靠），拿不到時退回全域主視窗；
    任何環境不支援時安靜地回傳 None，不影響外掛其他功能。"""
    try:
        doc = Rhino.RhinoDoc.ActiveDoc
        if doc is not None:
            win = Rhino.UI.RhinoEtoApp.MainWindowForDocument(doc)
            if win is not None:
                return win
    except Exception:
        pass
    try:
        return Rhino.UI.RhinoEtoApp.MainWindow
    except Exception:
        return None


class PatternTile(forms.Drawable):
    """單一圖案的縮圖選取按鈕：用實際拼貼演算法畫出縮圖，點擊即選取該圖案。"""

    def __init__(self, pattern_id, label_text, on_select):
        super(PatternTile, self).__init__()
        self.pattern_id = pattern_id
        self.label_text = label_text
        self._on_select = on_select
        self.selected = False
        self.hovered = False
        self.Size = drawing.Size(TILE_W, TILE_H)
        self._polys = icons.sample_polygons(pattern_id)
        self.Paint += self._paint
        self.MouseDown += self._mouse_down
        self.MouseEnter += self._mouse_enter
        self.MouseLeave += self._mouse_leave
        try:
            self.Cursor = forms.Cursors.Pointer
        except Exception:
            pass

    def set_selected(self, val):
        if self.selected != val:
            self.selected = val
            self.Invalidate()

    def _mouse_down(self, sender, e):
        self._on_select(self.pattern_id)

    def _mouse_enter(self, sender, e):
        self.hovered = True
        self.Invalidate()

    def _mouse_leave(self, sender, e):
        self.hovered = False
        self.Invalidate()

    def _paint(self, sender, e):
        g = e.Graphics
        g.AntiAlias = True
        bg = C_ACCENT_LIGHT if self.selected else C_PANEL
        if self.selected:
            border, border_w = C_ACCENT, 1.4
        elif self.hovered:
            border, border_w = C_ACCENT_HOVER, 1.2
        else:
            border, border_w = C_BORDER, 1.0

        g.Clear(C_BG)
        g.FillRectangle(drawing.SolidBrush(bg), 1, 1, TILE_W - 2, TILE_H - 2)
        g.DrawRectangle(drawing.Pen(border, border_w), 1, 1, TILE_W - 2, TILE_H - 2)

        icon_fill = C_ICON_FILL_SEL if self.selected else C_ICON_FILL
        icon_stroke = C_ICON_STROKE_SEL if self.selected else C_ICON_STROKE
        half = ICON_AREA / 2.0
        cx = TILE_W / 2.0
        cy = 4 + half
        view = icons.VIEW

        try:
            g.SetClip(drawing.RectangleF(2, 2, TILE_W - 4, ICON_AREA + 6))
        except Exception:
            pass

        brush = drawing.SolidBrush(icon_fill)
        pen = drawing.Pen(icon_stroke, 0.8)
        for poly in self._polys:
            if len(poly) < 3:
                continue
            pts = [drawing.PointF(cx + (x / view) * half, cy - (y / view) * half) for (x, y) in poly]
            g.FillPolygon(brush, pts)
            g.DrawPolygon(pen, pts)

        try:
            g.ResetClip()
        except Exception:
            pass

        font = _font('caption')
        text_color = C_ACCENT if self.selected else C_TEXT
        tb = drawing.SolidBrush(text_color)
        sz = g.MeasureString(font, self.label_text)
        tx = (TILE_W - sz.Width) / 2.0
        ty = TILE_H - sz.Height - 3
        g.DrawText(font, tb, tx, ty, self.label_text)


class TextureThumb(forms.Drawable):
    """已選材質圖片的縮圖，右上角有個別的 x 取消鈕。"""

    BADGE = 14

    def __init__(self, path, on_remove):
        super(TextureThumb, self).__init__()
        self.path = path
        self._on_remove = on_remove
        self.Size = drawing.Size(THUMB_SIZE, THUMB_SIZE)
        try:
            self._bitmap = drawing.Bitmap(path)
        except Exception:
            self._bitmap = None
        self.Paint += self._paint
        self.MouseDown += self._mouse_down
        try:
            self.Cursor = forms.Cursors.Pointer
        except Exception:
            pass

    def _badge_rect(self):
        return drawing.RectangleF(THUMB_SIZE - self.BADGE, 0, self.BADGE, self.BADGE)

    def _paint(self, sender, e):
        g = e.Graphics
        g.AntiAlias = True
        g.Clear(C_BG)
        inner = drawing.RectangleF(1, 1, THUMB_SIZE - 2, THUMB_SIZE - 2)
        if self._bitmap is not None:
            try:
                g.DrawImage(self._bitmap, inner)
            except Exception:
                g.FillRectangle(drawing.SolidBrush(C_PANEL), inner)
        else:
            g.FillRectangle(drawing.SolidBrush(C_PANEL), inner)
        g.DrawRectangle(drawing.Pen(C_BORDER, 1.0), 1, 1, THUMB_SIZE - 2, THUMB_SIZE - 2)

        badge = self._badge_rect()
        g.FillEllipse(drawing.SolidBrush(drawing.Color.FromArgb(0xB0, 0x30, 0x30)), badge)
        font = _font('subhead')
        sz = g.MeasureString(font, u"x")
        tx = badge.X + (badge.Width - sz.Width) / 2.0
        ty = badge.Y + (badge.Height - sz.Height) / 2.0
        g.DrawText(font, drawing.SolidBrush(drawing.Colors.White), tx, ty, u"x")

    def _mouse_down(self, sender, e):
        if self._badge_rect().Contains(e.Location):
            self._on_remove(self.path)


def _empty_thumb_slot():
    """空的縮圖佔位格：跟 TextureThumb 一樣大小，維持縮圖區固定高度，不管選幾張圖都不會變長變短。"""
    d = forms.Drawable()
    d.Size = drawing.Size(THUMB_SIZE, THUMB_SIZE)

    def paint(sender, e):
        g = e.Graphics
        g.AntiAlias = True
        g.Clear(C_BG)
        inner = drawing.RectangleF(1, 1, THUMB_SIZE - 2, THUMB_SIZE - 2)
        g.FillRectangle(drawing.SolidBrush(C_PANEL), inner)
        g.DrawRectangle(drawing.Pen(C_BORDER, 1.0), 1, 1, THUMB_SIZE - 2, THUMB_SIZE - 2)

    d.Paint += paint
    return d


class FlooraForm(forms.Form):
    def __init__(self):
        super(FlooraForm, self).__init__()
        self.Title = u"DB3D Floora for Rhino by Onon.Nihow"
        self.Topmost = True
        self.Resizable = True
        self.BackgroundColor = C_BG
        self.Padding = drawing.Padding(10)

        try:
            main_window = _rhino_main_window()
            if main_window is not None:
                self.Owner = main_window
        except Exception:
            pass

        self._pattern_id = store.load_current_pattern('Tile')
        if self._pattern_id not in df.PATTERNS:
            self._pattern_id = 'Tile'
        self._opts = store.load_pattern_opts(self._pattern_id, df.defaults_for(self._pattern_id))
        self._undo_stack = []
        self._fields = {}
        self._field_rows = {}
        self._pattern_tiles = {}

        self._paint_mode = 'current'
        self._texture_paths = []
        self._custom_color = _to_sys_color(DEFAULT_TILE_COLOR)

        self._bevel_enabled = False
        self._bevel_size = 0.3

        self._texture_opts = {'align_edge': False, 'random_position': False, 'random_rotate': False}
        self._random_defect = False
        self._defect_min = 0.0
        self._defect_max = 2.0
        self._back_face = False
        self._keep_group = False

        self._preview_boundary = None
        self._preview_plane = None
        self._preview_anchor = None
        self._preview_ids = []

        self._build_layout()
        self.Closed += self._on_closed

    # ---------------------------------------------------------------- layout

    def _build_layout(self):
        outer = _vstack()

        # 字級一律用模組層級的 FONT_SPECS（見檔案開頭），跟材料計算機／外掛說明共用同一套字級
        title_font = _font('title')
        section_font = _font('section')
        sub_font = _font('body')

        title = _label(u"DB3D Floora for Rhino by Onon.Nihow", title_font, C_ACCENT)
        title.TextAlignment = forms.TextAlignment.Center
        _add_row(outer, title)
        _add_row(outer, _hr())

        _add_row(outer, _section_label(u"圖案", section_font, C_ACCENT))
        flat_order = []
        for _cat_name, ids in df.CATEGORIES_TW:
            flat_order.extend(ids)
        row = []
        for pid in flat_order:
            label_text = TILE_LABELS.get(pid, df.NAMES_TW[pid])
            tile = PatternTile(pid, label_text, self._select_pattern)
            tile.set_selected(pid == self._pattern_id)
            self._pattern_tiles[pid] = tile
            row.append(tile)
            if len(row) == GRID_COLS:
                outer.Items.Add(forms.StackLayoutItem(_hrow(*row)))
                row = []
        if row:
            outer.Items.Add(forms.StackLayoutItem(_hrow(*row)))

        _add_row(outer, _hr())
        _add_row(outer, _section_label(u"尺寸與縫隙", section_font, C_ACCENT))

        def make_field(key):
            box = forms.NumericStepper()
            box.DecimalPlaces = 2
            box.MinValue = 0
            box.MaxValue = 100000
            box.Increment = INCREMENTS.get(key, 1.0)
            box.Width = 70
            box.Value = float(self._opts.get(key, 0.0))
            box.ValueChanged += self._make_opt_handler(key)
            self._fields[key] = box
            return box

        self._gx_label = _label(u"長度 (cm)")
        self._gx_label.Width = FIELD_LABEL_WIDTH
        gx_box = make_field('gx')
        gy_label = _label(u"寬度 (cm)")
        gy_label.Width = FIELD_LABEL_WIDTH
        gy_box = make_field('gy')
        outer.Items.Add(forms.StackLayoutItem(_hrow(self._gx_label, gx_box, gy_label, gy_box, None)))
        self._field_rows['gx'] = (self._gx_label, gx_box, None)
        self._field_rows['gy'] = (gy_label, gy_box, None)

        gw_label = _label(u"縫寬 (cm)")
        gw_label.Width = FIELD_LABEL_WIDTH
        gw_box = make_field('gw')
        gd_label = _label(u"縫深 (cm)")
        gd_label.Width = FIELD_LABEL_WIDTH
        gd_box = make_field('gd')
        outer.Items.Add(forms.StackLayoutItem(_hrow(gw_label, gw_box, gd_label, gd_box, None)))
        self._field_rows['gw'] = (gw_label, gw_box, None)
        self._field_rows['gd'] = (gd_label, gd_box, None)

        r2r_label = _label(u"錯縫 %")
        r2r_label.Width = FIELD_LABEL_WIDTH
        r2r_box = make_field('r2r')
        twa_label = _label(u"斜紋角度")
        twa_label.Width = FIELD_LABEL_WIDTH
        twa_box = make_field('twa')
        self._bwb_label = _label(u"編織數")
        self._bwb_label.Width = FIELD_LABEL_WIDTH
        self._bwb_options = ['2', '3', '4']
        self._bwb_dd = forms.DropDown()
        self._bwb_dd.DataStore = self._bwb_options
        self._bwb_dd.Width = 55
        self._bwb_dd.SelectedIndexChanged += self._on_bwb_changed
        outer.Items.Add(forms.StackLayoutItem(
            _hrow(r2r_label, r2r_box, twa_label, twa_box, self._bwb_label, self._bwb_dd, None)))
        self._field_rows['r2r'] = (r2r_label, r2r_box, set(['Brick', 'Tile', 'Wedge']))
        self._field_rows['twa'] = (twa_label, twa_box, set(['Tweed']))

        rot_label = _label(u"旋轉角度")
        rot_label.Width = FIELD_LABEL_WIDTH
        self._rot_options = ['0', '45', '90']
        self._rot_dd = forms.DropDown()
        self._rot_dd.DataStore = self._rot_options
        self._rot_dd.Width = 65
        self._rot_dd.SelectedIndexChanged += self._on_rot_changed

        spt_label = _label(u"起始點")
        spt_label.Width = FIELD_LABEL_WIDTH
        self._spt_items = df.START_POINT_OPTIONS
        self._spt_dd = forms.DropDown()
        self._spt_dd.DataStore = [t[0] for t in self._spt_items]
        self._spt_dd.Width = 65
        self._spt_dd.SelectedIndexChanged += self._on_spt_changed
        outer.Items.Add(forms.StackLayoutItem(_hrow(rot_label, self._rot_dd, spt_label, self._spt_dd, None)))

        _add_row(outer, _hr())
        _add_row(outer, _section_label(u"材質", section_font, C_ACCENT))

        paint_label = _label(u"上色模式")
        paint_label.Width = FIELD_LABEL_WIDTH
        self._paint_options = PAINT_MODES
        self._paint_dd = forms.DropDown()
        self._paint_dd.DataStore = [t[0] for t in self._paint_options]
        self._paint_dd.Width = 90
        self._paint_dd.SelectedIndex = 0
        self._paint_dd.SelectedIndexChanged += self._on_paint_mode_changed
        outer.Items.Add(forms.StackLayoutItem(_hrow(paint_label, self._paint_dd, None)))

        # 自訂顏色列（預設淺灰色）
        color_label = _label(u"磚片顏色")
        color_label.Width = FIELD_LABEL_WIDTH
        self._color_picker = forms.ColorPicker()
        self._color_picker.Value = DEFAULT_TILE_COLOR
        self._color_picker.ValueChanged += self._on_custom_color_changed
        self._color_row = _hrow(color_label, self._color_picker, None)
        self._color_row.Visible = False
        outer.Items.Add(forms.StackLayoutItem(self._color_row))

        # 貼圖材質列：選擇按鈕 + 已選圖片縮圖（各自可按 x 移除）
        self._btn_pick_textures = _button(u"選擇材質圖片…")
        self._btn_pick_textures.Click += self._on_pick_textures
        self._texture_status = _label(u"尚未選擇材質圖片", sub_font, C_TEXT_SUB)
        self._texture_pick_row = _hrow(self._btn_pick_textures, self._texture_status, None)
        self._texture_pick_row.Visible = False
        outer.Items.Add(forms.StackLayoutItem(self._texture_pick_row))

        self._texture_thumb_container = _vstack()
        self._texture_thumb_container.Visible = False
        outer.Items.Add(forms.StackLayoutItem(self._texture_thumb_container))
        self._rebuild_texture_thumbs()  # 先畫好固定的 5 格佔位，避免之後選圖時高度跳動

        # ---- 紋理（僅貼圖材質模式時有意義）----
        _add_row(outer, _hr())
        self._texture_section_label = _section_label(u"紋理", section_font, C_ACCENT)
        self._texture_section_label.Visible = False
        _add_row(outer, self._texture_section_label)

        align_label = _label(u"對齊邊")
        align_label.Width = FIELD_LABEL_WIDTH
        self._align_edge_check = forms.CheckBox()
        self._align_edge_check.Checked = False
        self._align_edge_check.CheckedChanged += self._on_texture_opt_changed
        rpos_label = _label(u"隨機位置")
        rpos_label.Width = FIELD_LABEL_WIDTH
        self._random_pos_check = forms.CheckBox()
        self._random_pos_check.Checked = False
        self._random_pos_check.CheckedChanged += self._on_texture_opt_changed
        rrot_label = _label(u"隨機旋轉")
        rrot_label.Width = FIELD_LABEL_WIDTH
        self._random_rot_check = forms.CheckBox()
        self._random_rot_check.Checked = False
        self._random_rot_check.CheckedChanged += self._on_texture_opt_changed
        self._texture_effect_row = _hrow(
            align_label, self._align_edge_check, rpos_label, self._random_pos_check,
            rrot_label, self._random_rot_check, None)
        self._texture_effect_row.Visible = False
        outer.Items.Add(forms.StackLayoutItem(self._texture_effect_row))

        # ---- 效果 ----
        _add_row(outer, _hr())
        _add_row(outer, _section_label(u"效果", section_font, C_ACCENT))

        bevel_label = _label(u"倒斜角")
        bevel_label.Width = FIELD_LABEL_WIDTH
        self._bevel_check = forms.CheckBox()
        self._bevel_check.Checked = False
        self._bevel_check.CheckedChanged += self._on_bevel_toggle
        self._bevel_size_label = _label(u"角尺寸 (cm)")
        self._bevel_size_label.Width = FIELD_LABEL_WIDTH
        self._bevel_size_box = forms.NumericStepper()
        self._bevel_size_box.DecimalPlaces = 2
        self._bevel_size_box.MinValue = 0.01
        self._bevel_size_box.Increment = 0.1
        self._bevel_size_box.Width = 70
        self._bevel_size_box.Value = 0.3
        self._bevel_size_box.ValueChanged += self._on_bevel_size_changed
        self._bevel_size_label.Visible = False
        self._bevel_size_box.Visible = False
        outer.Items.Add(forms.StackLayoutItem(
            _hrow(bevel_label, self._bevel_check, self._bevel_size_label, self._bevel_size_box, None)))

        defect_label = _label(u"隨機缺陷")
        defect_label.Width = FIELD_LABEL_WIDTH
        self._defect_check = forms.CheckBox()
        self._defect_check.Checked = False
        self._defect_check.CheckedChanged += self._on_defect_toggle
        self._defect_min_label = _label(u"最小角度")
        self._defect_min_label.Width = FIELD_LABEL_WIDTH
        self._defect_min_box = forms.NumericStepper()
        self._defect_min_box.DecimalPlaces = 1
        self._defect_min_box.MinValue = 0.0
        self._defect_min_box.MaxValue = 45.0
        self._defect_min_box.Width = 55
        self._defect_min_box.Value = 0.0
        self._defect_min_box.ValueChanged += self._on_defect_range_changed
        self._defect_max_label = _label(u"最大角度")
        self._defect_max_label.Width = FIELD_LABEL_WIDTH
        self._defect_max_box = forms.NumericStepper()
        self._defect_max_box.DecimalPlaces = 1
        self._defect_max_box.MinValue = 0.0
        self._defect_max_box.MaxValue = 45.0
        self._defect_max_box.Width = 55
        self._defect_max_box.Value = 2.0
        self._defect_max_box.ValueChanged += self._on_defect_range_changed
        for c in (self._defect_min_label, self._defect_min_box, self._defect_max_label, self._defect_max_box):
            c.Visible = False
        outer.Items.Add(forms.StackLayoutItem(_hrow(
            defect_label, self._defect_check,
            self._defect_min_label, self._defect_min_box,
            self._defect_max_label, self._defect_max_box, None)))

        back_label = _label(u"背面")
        back_label.Width = FIELD_LABEL_WIDTH
        self._back_face_check = forms.CheckBox()
        self._back_face_check.Checked = False
        self._back_face_check.CheckedChanged += self._on_back_face_toggle
        group_label = _label(u"獨立群組")
        group_label.Width = FIELD_LABEL_WIDTH
        self._keep_group_check = forms.CheckBox()
        self._keep_group_check.Checked = False
        self._keep_group_check.CheckedChanged += self._on_keep_group_toggle
        outer.Items.Add(forms.StackLayoutItem(
            _hrow(back_label, self._back_face_check, group_label, self._keep_group_check, None)))

        _add_row(outer, _hr())
        self._btn_preview = _button(u"選擇物件")
        self._btn_preview.Click += self._on_preview
        self._btn_generate = _button(u"產生磚塊", primary=True)
        self._btn_generate.Click += self._on_generate
        self._btn_undo = _button(u"復原")
        self._btn_undo.Click += self._on_undo
        self._btn_calc = _button(u"材料計算機")
        self._btn_calc.Click += self._on_calc
        self._btn_reset = _button(u"重設")
        self._btn_reset.Click += self._on_reset
        self._btn_info = _button(u"外掛說明")
        self._btn_info.Click += self._on_info

        outer.Items.Add(forms.StackLayoutItem(_hrow(self._btn_preview, self._btn_generate)))
        outer.Items.Add(forms.StackLayoutItem(_hrow(self._btn_undo, self._btn_calc, self._btn_reset)))
        outer.Items.Add(forms.StackLayoutItem(_hrow(self._btn_info)))

        self._status = _label(u"請點選「產生磚塊」開始", sub_font, C_TEXT_SUB)
        _add_row(outer, self._status)

        self.Content = outer
        self._refresh_fields()

    # ---------------------------------------------------------------- state sync

    def _refresh_fields(self):
        for key, box in self._fields.items():
            box.Value = float(self._opts.get(key, 0.0))

        cur_rot = str(int(self._opts.get('rot', 0)))
        if cur_rot in self._rot_options:
            self._rot_dd.SelectedIndex = self._rot_options.index(cur_rot)

        cur_spt = self._opts.get('spt', 'corner')
        for k, (_lbl, val) in enumerate(self._spt_items):
            if val == cur_spt:
                self._spt_dd.SelectedIndex = k

        cur_bwb = str(int(self._opts.get('bwb', 3)))
        if cur_bwb in self._bwb_options:
            self._bwb_dd.SelectedIndex = self._bwb_options.index(cur_bwb)

        self._update_field_visibility()

    def _update_field_visibility(self):
        pid = self._pattern_id
        for key, (lbl, box, pats) in self._field_rows.items():
            visible = True if pats is None else (pid in pats)
            if key == 'gy' and pid in df.NO_GY_PATTERNS:
                visible = False
            if key == 'gw' and pid in df.NO_GW_PATTERNS:
                visible = False
            lbl.Visible = visible
            box.Visible = visible

        self._gx_label.Text = u"密度 (cm)" if pid == 'IrPoly' else (u"邊長 (cm)" if pid in df.NO_GY_PATTERNS else u"長度 (cm)")

        show_bwb = (pid == 'BsktWv')
        self._bwb_label.Visible = show_bwb
        self._bwb_dd.Visible = show_bwb

    # ---------------------------------------------------------------- handlers

    def _select_pattern(self, pid):
        if pid == self._pattern_id:
            return
        self._set_pattern(pid)

    def _set_pattern(self, pid):
        self._pattern_id = pid
        self._opts = store.load_pattern_opts(pid, df.defaults_for(pid))
        for p, tile in self._pattern_tiles.items():
            tile.set_selected(p == pid)
        self._refresh_fields()
        store.save_pattern_opts(pid, self._opts)
        self._update_preview()

    def _make_opt_handler(self, key):
        def handler(sender, e):
            self._opts[key] = sender.Value
            store.save_pattern_opts(self._pattern_id, self._opts)
            self._update_preview()
        return handler

    def _on_rot_changed(self, sender, e):
        val = self._rot_options[self._rot_dd.SelectedIndex]
        self._opts['rot'] = float(val)
        store.save_pattern_opts(self._pattern_id, self._opts)
        self._update_preview()

    def _on_spt_changed(self, sender, e):
        val = self._spt_items[self._spt_dd.SelectedIndex][1]
        self._opts['spt'] = val
        store.save_pattern_opts(self._pattern_id, self._opts)
        self._update_preview()

    def _on_bwb_changed(self, sender, e):
        if self._bwb_dd.SelectedIndex < 0:
            return
        val = self._bwb_options[self._bwb_dd.SelectedIndex]
        self._opts['bwb'] = int(val)
        store.save_pattern_opts(self._pattern_id, self._opts)
        self._update_preview()

    def _on_paint_mode_changed(self, sender, e):
        idx = self._paint_dd.SelectedIndex
        if idx < 0 or idx >= len(self._paint_options):
            return
        self._paint_mode = self._paint_options[idx][1]
        self._color_row.Visible = (self._paint_mode == 'custom_color')
        show_texture = (self._paint_mode == 'texture')
        self._texture_pick_row.Visible = show_texture
        self._texture_thumb_container.Visible = show_texture
        self._texture_section_label.Visible = show_texture
        self._texture_effect_row.Visible = show_texture
        self._update_preview()

    def _on_custom_color_changed(self, sender, e):
        self._custom_color = _to_sys_color(self._color_picker.Value)
        self._update_preview()

    def _on_texture_opt_changed(self, sender, e):
        self._texture_opts = {
            'align_edge': bool(self._align_edge_check.Checked),
            'random_position': bool(self._random_pos_check.Checked),
            'random_rotate': bool(self._random_rot_check.Checked),
        }

    def _on_bevel_toggle(self, sender, e):
        self._bevel_enabled = bool(self._bevel_check.Checked)
        self._bevel_size_label.Visible = self._bevel_enabled
        self._bevel_size_box.Visible = self._bevel_enabled
        self._update_preview()

    def _on_bevel_size_changed(self, sender, e):
        self._bevel_size = self._bevel_size_box.Value
        self._update_preview()

    def _on_defect_toggle(self, sender, e):
        self._random_defect = bool(self._defect_check.Checked)
        for c in (self._defect_min_label, self._defect_min_box, self._defect_max_label, self._defect_max_box):
            c.Visible = self._random_defect

    def _on_defect_range_changed(self, sender, e):
        self._defect_min = self._defect_min_box.Value
        self._defect_max = max(self._defect_max_box.Value, self._defect_min)

    def _on_back_face_toggle(self, sender, e):
        self._back_face = bool(self._back_face_check.Checked)

    def _on_keep_group_toggle(self, sender, e):
        self._keep_group = bool(self._keep_group_check.Checked)

    def _on_pick_textures(self, sender, e):
        dlg = forms.OpenFileDialog()
        dlg.Title = u"選擇材質圖片"
        dlg.MultiSelect = True
        f = forms.FileFilter(u"圖片檔案", IMAGE_EXTENSIONS)
        dlg.Filters.Add(f)
        dlg.CurrentFilter = f
        result = dlg.ShowDialog(self)
        if result != forms.DialogResult.Ok:
            return
        paths = list(dlg.Filenames) if hasattr(dlg, 'Filenames') else [dlg.FileName]
        paths = [p for p in paths if p]
        if not paths:
            return
        existing = set(self._texture_paths)
        skipped = 0
        for p in paths:
            if p in existing:
                continue
            if len(self._texture_paths) >= MAX_TEXTURES:
                skipped += 1
                continue
            self._texture_paths.append(p)
            existing.add(p)
        materials.reset_cache()
        self._rebuild_texture_thumbs()
        self._update_preview()
        if skipped:
            forms.MessageBox.Show(self, u"最多只能選 %d 張材質圖片，已略過 %d 張。" % (MAX_TEXTURES, skipped),
                                   u"DB3D-Floora")

    def _remove_texture(self, path):
        if path in self._texture_paths:
            self._texture_paths.remove(path)
        materials.reset_cache()
        self._rebuild_texture_thumbs()
        self._update_preview()

    def _rebuild_texture_thumbs(self):
        # 固定畫出 MAX_TEXTURES 格（選了幾張就填幾張縮圖，其餘用空格佔位），
        # 這樣不管使用者選 1 張還是 5 張，這個區塊的高度都不會變，介面也就不會跟著變長變短。
        self._texture_thumb_container.Items.Clear()
        row = []
        for path in self._texture_paths:
            row.append(TextureThumb(path, self._remove_texture))
        while len(row) < MAX_TEXTURES:
            row.append(_empty_thumb_slot())
        self._texture_thumb_container.Items.Add(forms.StackLayoutItem(_hrow(*row)))

        if self._texture_paths:
            self._texture_status.Text = u"已選 %d／%d 張材質圖片" % (len(self._texture_paths), MAX_TEXTURES)
        else:
            self._texture_status.Text = u"尚未選擇材質圖片（最多 %d 張）" % MAX_TEXTURES

    # ------------------------------------------------------------ 預覽

    def _preview_layer_id(self, doc):
        return _preview_layer(doc)

    def _clear_preview(self, doc=None):
        if not self._preview_ids:
            return
        doc = doc or Rhino.RhinoDoc.ActiveDoc
        for gid in self._preview_ids:
            try:
                doc.Objects.Delete(gid, True)
            except Exception:
                pass
        self._preview_ids = []

    def _update_preview(self):
        if self._preview_boundary is None:
            return
        doc = Rhino.RhinoDoc.ActiveDoc
        self._clear_preview(doc)
        try:
            curves = pat.generate(self._pattern_id, self._preview_boundary, self._preview_plane,
                                   self._opts, self._preview_anchor)
        except Exception as ex:
            doc.Views.Redraw()
            self._status.Text = u"預覽失敗：%s" % ex
            return

        layer_idx = self._preview_layer_id(doc)
        attrs = Rhino.DocObjects.ObjectAttributes()
        attrs.LayerIndex = layer_idx
        ids = []
        for c in curves:
            try:
                gid = doc.Objects.AddCurve(c, attrs)
            except Exception:
                continue
            if gid and gid != System.Guid.Empty:
                ids.append(gid)
        self._preview_ids = ids
        doc.Views.Redraw()
        self._status.Text = u"預覽中：%d 片磚（調整參數會即時更新，按「產生磚塊」確認）" % len(ids)

    def _on_preview(self, sender, e):
        """「選擇物件」：先選面，接著滑鼠移動即時預覽鋪磚位置（用 conduit 畫，不會產生真的
        線段物件，取消也不會留下任何東西），左鍵點擊決定起磚點後才轉成可調整參數的靜態預覽。"""
        obj_ref = _pick_face()
        if obj_ref is None:
            self._status.Text = u"已取消選取"
            return

        boundary, plane = geo.get_boundary_and_plane(obj_ref)
        if boundary is None:
            forms.MessageBox.Show(self, u"選取的物件不是平面（面或封閉平面曲線），請重新選取。", u"DB3D-Floora")
            return

        anchor = self._interactive_pick_anchor(boundary, plane)
        if anchor is None:
            self._status.Text = u"已取消選取起磚點"
            return

        self._preview_boundary = boundary
        self._preview_plane = plane
        self._preview_anchor = anchor
        self._update_preview()

    def _interactive_pick_anchor(self, boundary, plane):
        """滑鼠在面上移動時即時畫出鋪磚預覽（DynamicDraw，純畫面顯示，不建立文件物件），
        左鍵點擊回傳該點作為起磚點；按 Esc/右鍵取消則回傳 None，畫面上不會留下任何東西。"""
        gp = ric.GetPoint()
        gp.SetCommandPrompt(u"移動滑鼠預覽鋪磚位置，左鍵點擊決定起磚點（Esc 取消）")
        try:
            gp.Constrain(plane, False)
        except Exception:
            pass

        cache = {'key': None, 'curves': []}
        color = sysdraw.Color.FromArgb(0x2A, 0x5F, 0x8A)

        def dynamic_draw(sender, de):
            try:
                pt = plane.ClosestPoint(de.CurrentPoint)
            except Exception:
                return
            key = (round(pt.X, 1), round(pt.Y, 1), round(pt.Z, 1))
            if key != cache['key']:
                cache['key'] = key
                try:
                    cache['curves'] = pat.generate(self._pattern_id, boundary, plane, self._opts, pt)
                except Exception:
                    cache['curves'] = []
            for c in cache['curves']:
                try:
                    de.Display.DrawCurve(c, color, 2)
                except Exception:
                    pass

        gp.DynamicDraw += dynamic_draw
        try:
            result = gp.Get()
        finally:
            gp.DynamicDraw -= dynamic_draw

        if result != Rhino.Input.GetResult.Point:
            return None
        return plane.ClosestPoint(gp.Point())

    # ------------------------------------------------------------ 產生 / 復原 / 計算機 / 重設

    def _on_generate(self, sender, e):
        if self._preview_boundary is not None:
            boundary = self._preview_boundary
            plane = self._preview_plane
            anchor_pt = self._preview_anchor
        else:
            obj_ref = _pick_face()
            if obj_ref is None:
                self._status.Text = u"已取消選取"
                return

            boundary, plane = geo.get_boundary_and_plane(obj_ref)
            if boundary is None:
                forms.MessageBox.Show(self, u"選取的物件不是平面（面或封閉平面曲線），請重新選取。", u"DB3D-Floora")
                return

            anchor_pt = None
            if self._opts.get('spt') == 'pick':
                gp = ric.GetPoint()
                gp.SetCommandPrompt(u"請點選圖案起始點")
                result = gp.Get()
                if result != Rhino.Input.GetResult.Point:
                    self._status.Text = u"已取消選取起始點"
                    return
                anchor_pt = plane.ClosestPoint(gp.Point())

        try:
            curves = pat.generate(self._pattern_id, boundary, plane, self._opts, anchor_pt)
        except Exception as ex:
            forms.MessageBox.Show(self, u"產生磚塊失敗：%s" % ex, u"DB3D-Floora")
            return

        if not curves:
            self._status.Text = u"沒有產生任何磚片，請檢查尺寸設定"
            return

        doc = Rhino.RhinoDoc.ActiveDoc
        self._clear_preview(doc)
        layer_idx = _pattern_layer(doc, self._pattern_id)
        gd = float(self._opts.get('gd', 0.0))

        # 紋理對齊用的參考平面：跟著圖案本身的旋轉走（含圖案自帶的額外旋轉，例如魚骨拼/鑽石紋）
        ref_plane = rg.Plane(plane.Origin, plane.XAxis, plane.YAxis)
        total_rot = float(self._opts.get('rot', 0.0)) + pat.PATTERN_EXTRA_ROT.get(self._pattern_id, 0.0)
        if total_rot:
            ref_plane.Rotate(math.radians(total_rot), ref_plane.ZAxis)
        tex_w = float(self._opts.get('gx', 30.0))
        tex_h = float(self._opts.get('gy', 30.0)) or tex_w

        batch = []
        for c in curves:
            gid = _add_tile(doc, c, layer_idx, gd, self._paint_mode, self._texture_paths, self._custom_color,
                             self._bevel_enabled, self._bevel_size,
                             self._random_defect, self._defect_min, self._defect_max, self._back_face)
            if gid and gid != System.Guid.Empty:
                if self._paint_mode == 'texture' and self._texture_paths:
                    materials.apply_texture_mapping(doc, gid, ref_plane, tex_w, tex_h, self._texture_opts)
                batch.append(gid)

        if self._keep_group and batch:
            try:
                doc.Groups.Add(batch)
            except Exception:
                pass

        doc.Views.Redraw()

        self._undo_stack.append(batch)
        if len(self._undo_stack) > MAX_UNDO:
            self._undo_stack.pop(0)

        store.save_pattern_opts(self._pattern_id, self._opts)
        self._preview_boundary = None
        self._preview_plane = None
        self._preview_anchor = None
        self._status.Text = u"已產生 %d 片磚（%s）" % (len(batch), df.NAMES_TW.get(self._pattern_id, self._pattern_id))

    def _on_undo(self, sender, e):
        if not self._undo_stack:
            self._status.Text = u"沒有可復原的操作"
            return
        batch = self._undo_stack.pop()
        doc = Rhino.RhinoDoc.ActiveDoc
        for gid in batch:
            doc.Objects.Delete(gid, True)
        doc.Views.Redraw()
        self._status.Text = u"已復原上一步（剩餘 %d 步可復原）" % len(self._undo_stack)

    def _on_calc(self, sender, e):
        try:
            stats = calc.layer_area_stats()
        except Exception as ex:
            forms.MessageBox.Show(self, u"計算失敗：%s" % ex, u"DB3D-Floora")
            return
        CalculatorForm(stats).Show()

    def _on_info(self, sender, e):
        InfoForm().Show()

    def _on_reset(self, sender, e):
        store.reset_all()
        self._clear_preview()
        self._preview_boundary = None
        self._preview_plane = None
        self._preview_anchor = None
        self._set_pattern('Tile')
        self._undo_stack = []
        self._status.Text = u"已重設為預設值"

    def _on_closed(self, sender, e):
        global _instance
        self._clear_preview()
        _instance = None


def _open_url(url):
    try:
        import System.Diagnostics as sysdiag
        psi = sysdiag.ProcessStartInfo(url)
        psi.UseShellExecute = True
        sysdiag.Process.Start(psi)
    except Exception:
        pass


class InfoForm(forms.Form):
    """外掛說明：使用方式＋作者介紹，全部內容靠左對齊。
    注意：介面功能有變動時，下面 USAGE_GROUPS 的內容也要跟著同步更新。"""

    # 每組 = (小標題, [條列說明...])，順序對應介面由上到下的操作流程
    USAGE_GROUPS = [
        (u"1. 選圖案與尺寸", [
            u"從「圖案」縮圖選擇鋪貼樣式",
            u"在「尺寸與縫隙」設定長寬、縫寬、縫深",
            u"部分圖案還會有錯縫%／斜紋角度／編織數、旋轉角度、起始點",
        ]),
        (u"2. 設定材質", [
            u"上色模式：目前材質／自訂顏色／貼圖材質",
            u"貼圖材質最多可選 %d 張圖片，縮圖右上角 x 可個別移除" % MAX_TEXTURES,
        ]),
        (u"3. 紋理（貼圖材質模式）", [
            u"對齊邊／隨機位置／隨機旋轉",
        ]),
        (u"4. 效果", [
            u"倒斜角、隨機缺陷、背面、獨立群組",
        ]),
        (u"5. 選面與預覽", [
            u"按「選擇物件」選面，移動滑鼠即時預覽鋪磚位置（純畫面顯示，不會產生真的線段）",
            u"左鍵點擊決定起磚點，按 Esc／右鍵取消不留下任何東西",
            u"起磚點確定後仍可持續調整參數，預覽會即時更新",
        ]),
        (u"6. 產生與管理", [
            u"按「產生磚塊」確認並生成正式磚片",
            u"「復原」可撤銷最近 3 次操作；「重設」回到預設值",
        ]),
        (u"7. 材料計算機", [
            u"統計已產生磚片的面積、估算用量，並可匯出 CSV／圖片",
        ]),
    ]

    def __init__(self):
        super(InfoForm, self).__init__()
        self.Title = u"外掛說明"
        self.Topmost = True
        self.Resizable = True
        self.BackgroundColor = C_BG
        self.Padding = drawing.Padding(8)
        try:
            main_window = _rhino_main_window()
            if main_window is not None:
                self.Owner = main_window
        except Exception:
            pass
        self._build()

    def _link_row(self, label_text, url):
        lbl = _label(label_text, None, C_TEXT_SUB, align=forms.TextAlignment.Left)
        try:
            link = forms.LinkButton()
            link.Text = url
            link.Click += lambda s, e: _open_url(url)
            return _hrow(lbl, link, None)
        except Exception:
            return _hrow(lbl, _label(url, align=forms.TextAlignment.Left), None)

    def _body_label(self, text):
        lbl = _label(text, align=forms.TextAlignment.Left)
        try:
            lbl.Wrap = forms.WrapMode.Word
            lbl.Width = 320
        except Exception:
            pass
        return lbl

    def _build(self):
        outer = _vstack()
        section_font = _font('section')
        title_font = _font('title')
        subhead_font = _font('subhead')

        title = _label(u"DB3D Floora for Rhino", title_font, C_ACCENT, align=forms.TextAlignment.Left)
        _add_row(outer, title)
        _add_row(outer, _hr())

        _add_row(outer, _section_label(u"使用方式", section_font, C_ACCENT))
        for subtitle, lines in self.USAGE_GROUPS:
            _add_row(outer, _label(subtitle, subhead_font, C_TEXT, align=forms.TextAlignment.Left))
            for line in lines:
                _add_row(outer, self._body_label(u"‧ " + line))

        _add_row(outer, _hr())
        _add_row(outer, _section_label(u"作者介紹", section_font, C_ACCENT))
        _add_row(outer, _label(u"原始外掛：DB3D-Floora（SketchUp 版）", align=forms.TextAlignment.Left))
        _add_row(outer, _label(u"製作：DB3D.RENDER", None, C_TEXT_SUB, align=forms.TextAlignment.Left))
        outer.Items.Add(forms.StackLayoutItem(self._link_row(u"官網：", u"https://www.db3drender.com/")))
        outer.Items.Add(forms.StackLayoutItem(
            self._link_row(u"Instagram：", u"https://www.instagram.com/db3d.render/")))

        _add_row(outer, _hr())
        _add_row(outer, _label(u"Rhino 版製作：Onon.Nihow", None, C_ACCENT, align=forms.TextAlignment.Left))

        self.Content = outer


class CalculatorForm(forms.Form):
    def __init__(self, stats):
        super(CalculatorForm, self).__init__()
        self.Title = u"材料計算機"
        self.Topmost = True
        self.Resizable = True
        self.BackgroundColor = C_BG
        self.Padding = drawing.Padding(8)
        try:
            main_window = _rhino_main_window()
            if main_window is not None:
                self.Owner = main_window
        except Exception:
            pass
        self._build(stats)

    def _build(self, stats):
        outer = _vstack()
        title_font = _font('title')
        section_font = _font('section')

        title = _label(u"材料計算機", title_font, C_ACCENT, align=forms.TextAlignment.Left)
        _add_row(outer, title)
        _add_row(outer, _hr())

        total_area = sum(s['area_m2'] for s in stats)
        total_ping = total_area / calc.M2_PER_PING

        _add_row(outer, _section_label(u"圖層統計", section_font, C_ACCENT))
        if not stats:
            _add_row(outer, _label(u"目前模型中沒有 Floora 產生的磚片圖層", None, C_TEXT_SUB,
                                    align=forms.TextAlignment.Left))
        for s in stats:
            _add_row(outer, _label(u"%s：%d 片，%.2f m²（%.2f 坪）" % (
                s['name'], s['count'], s['area_m2'], s['area_ping']), align=forms.TextAlignment.Left))
        _add_row(outer, _label(u"總計：%.2f m²（%.2f 坪）" % (total_area, total_ping), None, C_ACCENT,
                                align=forms.TextAlignment.Left))

        _add_row(outer, _hr())
        _add_row(outer, _section_label(u"磚片估算", section_font, C_ACCENT))

        def numfield(default):
            box = forms.NumericStepper()
            box.DecimalPlaces = 2
            box.MinValue = 0
            box.MaxValue = 1000000
            box.Value = default
            box.Width = 100
            return box

        self._len_box = numfield(60.0)
        self._wid_box = numfield(60.0)
        self._waste_box = numfield(8.0)
        self._pcs_box = numfield(4.0)
        self._price_box_box = numfield(0.0)
        self._price_ping_box = numfield(0.0)

        def field_label(text):
            lbl = _label(text)
            lbl.Width = FIELD_LABEL_WIDTH
            return lbl

        outer.Items.Add(forms.StackLayoutItem(_hrow(field_label(u"磚片長 (cm)"), self._len_box, None)))
        outer.Items.Add(forms.StackLayoutItem(_hrow(field_label(u"磚片寬 (cm)"), self._wid_box, None)))
        outer.Items.Add(forms.StackLayoutItem(_hrow(field_label(u"耗損率 (%)"), self._waste_box, None)))
        outer.Items.Add(forms.StackLayoutItem(_hrow(field_label(u"每箱片數"), self._pcs_box, None)))
        outer.Items.Add(forms.StackLayoutItem(_hrow(field_label(u"每箱價格"), self._price_box_box, None)))
        outer.Items.Add(forms.StackLayoutItem(_hrow(field_label(u"每坪價格"), self._price_ping_box, None)))

        self._result_label = _label(u"")
        btn = _button(u"計算", primary=True)
        btn.Click += lambda s, e: self._recalc(total_area)
        _add_row(outer, btn)
        _add_row(outer, self._result_label)

        _add_row(outer, _hr())
        btn_csv = _button(u"匯出 CSV")
        btn_csv.Click += self._on_export_csv
        btn_img = _button(u"匯出圖片")
        btn_img.Click += self._on_export_image
        outer.Items.Add(forms.StackLayoutItem(_hrow(btn_csv, btn_img)))

        self.Content = outer
        self._stats = stats
        self._last_estimate = None
        self._recalc(total_area)

    def _recalc(self, total_area):
        r = calc.estimate(
            total_area,
            self._len_box.Value, self._wid_box.Value, self._waste_box.Value,
            self._pcs_box.Value, self._price_box_box.Value, self._price_ping_box.Value,
        )
        self._last_estimate = r
        if not r:
            self._result_label.Text = u"請輸入有效的磚片尺寸"
            return
        self._result_label.Text = (
            u"含耗損面積：%.2f m²\n需求片數：%d 片\n需求箱數：%d 箱\n估價（依箱）：%.0f\n估價（依坪）：%.0f"
            % (r['needed_area_m2'], r['tile_count'], r['boxes'], r['price_by_box'], r['price_by_ping'])
        )

    def _on_export_csv(self, sender, e):
        dlg = forms.SaveFileDialog()
        dlg.Title = u"匯出 CSV"
        f = forms.FileFilter(u"CSV 檔案", ['.csv'])
        dlg.Filters.Add(f)
        dlg.CurrentFilter = f
        dlg.FileName = u"floora_計算結果.csv"
        result = dlg.ShowDialog(self)
        if result != forms.DialogResult.Ok:
            return
        path = dlg.FileName
        if not path.lower().endswith('.csv'):
            path += '.csv'
        try:
            calc.write_csv(path, self._stats, self._last_estimate)
            forms.MessageBox.Show(self, u"已匯出：\n%s" % path, u"DB3D-Floora")
        except Exception as ex:
            forms.MessageBox.Show(self, u"匯出失敗：%s" % ex, u"DB3D-Floora")

    def _on_export_image(self, sender, e):
        dlg = forms.SaveFileDialog()
        dlg.Title = u"匯出圖片"
        f = forms.FileFilter(u"PNG 圖片", ['.png'])
        dlg.Filters.Add(f)
        dlg.CurrentFilter = f
        dlg.FileName = u"floora_計算結果.png"
        result = dlg.ShowDialog(self)
        if result != forms.DialogResult.Ok:
            return
        path = dlg.FileName
        if not path.lower().endswith('.png'):
            path += '.png'
        try:
            self._render_result_image(path)
            forms.MessageBox.Show(self, u"已匯出：\n%s" % path, u"DB3D-Floora")
        except Exception as ex:
            forms.MessageBox.Show(self, u"匯出失敗：%s" % ex, u"DB3D-Floora")

    def _render_result_image(self, path):
        title_font = drawing.Font(drawing.FontFamilies.Sans, 13, drawing.FontStyle.Bold)
        body_font = drawing.Font(drawing.FontFamilies.Sans, 11)

        lines = [u"DB3D Floora 材質計算結果"]
        for s in self._stats:
            lines.append(u"%s：%d 片，%.2f m²（%.2f 坪）" % (s['name'], s['count'], s['area_m2'], s['area_ping']))
        total_area = sum(s['area_m2'] for s in self._stats)
        total_ping = total_area / calc.M2_PER_PING
        lines.append(u"總計：%.2f m²（%.2f 坪）" % (total_area, total_ping))
        if self._last_estimate:
            r = self._last_estimate
            lines.append(u"")
            lines.append(u"含耗損面積：%.2f m²" % r['needed_area_m2'])
            lines.append(u"需求片數：%d 片" % r['tile_count'])
            lines.append(u"需求箱數：%d 箱" % r['boxes'])
            lines.append(u"估價（依箱）：%.0f" % r['price_by_box'])
            lines.append(u"估價（依坪）：%.0f" % r['price_by_ping'])

        width, line_h, top_h, pad = 480, 24, 50, 20
        height = top_h + pad * 2 + line_h * len(lines)
        bmp = drawing.Bitmap(width, height, drawing.PixelFormat.Format32bppRgba)
        g = drawing.Graphics(bmp)
        g.AntiAlias = True
        g.Clear(C_BG)
        g.DrawText(title_font, drawing.SolidBrush(C_ACCENT), pad, pad, lines[0])
        y = pad + top_h
        body_brush = drawing.SolidBrush(C_TEXT)
        for line in lines[1:]:
            g.DrawText(body_font, body_brush, pad, y, line)
            y += line_h
        g.Dispose()
        bmp.Save(path, drawing.ImageFormat.Png)
