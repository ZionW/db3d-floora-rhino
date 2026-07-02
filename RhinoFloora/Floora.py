# encoding: utf-8
"""DB3D-Floora - Rhino 版進入點。

使用方式：
  1. 把整個 RhinoFloora 資料夾放到固定位置（例如文件夾）。
  2. 在 Rhino 指令列輸入：
         -RunPythonScript "完整路徑/Floora.py"
     或直接把本檔案拖曳到 Rhino 視窗中執行。
  3. 也可以用 Rhino 的「工具 > 巨集」把上面那行指令存成工具列按鈕/別名，方便重複開啟。

執行後會開啟一個永遠置頂的中文操作視窗，可選擇圖案、設定尺寸/縫寬/縫深/旋轉，
按「產生圖案」後在畫面中點選一個平面（Brep 面或封閉平面曲線）即可鋪磚。
"""

import os
import sys
import traceback

_here = os.path.dirname(os.path.abspath(__file__))
if _here not in sys.path:
    sys.path.insert(0, _here)

try:
    from floora import ui
    ui.show()
except Exception:
    print(u"DB3D-Floora 啟動失敗：")
    traceback.print_exc()
