using System;
using System.Collections.Generic;

namespace DB3DFloora
{
    public enum AppLanguage
    {
        ZhTw,
        En,
    }

    /// <summary>介面繁體中文／英文字串對照表。所有面向使用者的文字都應該透過 T(key) 取得，
    /// 不要在 UI 程式碼裡直接寫死中文或英文字面量。切換語言後既有視窗不會即時重繪文字，
    /// 呼叫端要自己關掉舊視窗、重新建立一個新的（跟這個專案一貫「設定變更就重開視窗」的作法一致）。</summary>
    public static class Strings
    {
        public static AppLanguage Current = AppLanguage.ZhTw;

        private static readonly Dictionary<string, (string Zh, string En)> Table = new Dictionary<string, (string, string)>
        {
            // 主視窗標題／區塊標籤
            {"app.title", ("DB3D Floora for Rhino by Onon.Nihow", "DB3D Floora for Rhino by Onon.Nihow")},
            {"section.pattern", ("圖案", "Pattern")},
            {"section.presets", ("常用造型", "Favorite Styles")},
            {"preset.placeholder", ("（尚未儲存常用造型）", "(No favorites saved)")},
            {"btn.addPreset", ("加入常用…", "Add to Favorites…")},
            {"btn.deletePreset", ("刪除", "Delete")},
            {"dlg.presetName", ("常用造型名稱", "Favorite Style Name")},
            {"dlg.presetNamePrompt", ("請輸入這組造型的名稱：", "Enter a name for this style:")},
            {"btn.ok", ("確定", "OK")},
            {"btn.cancel", ("取消", "Cancel")},
            {"msg.presetNameEmpty", ("請輸入名稱", "Please enter a name")},
            {"msg.confirmDeletePreset", ("確定要刪除常用造型「{0}」嗎？", "Delete favorite style “{0}”?")},
            {"status.presetSaved", ("已加入常用造型「{0}」", "Added favorite style “{0}”")},
            {"status.presetApplied", ("已套用常用造型「{0}」", "Applied favorite style “{0}”")},
            {"status.presetDeleted", ("已刪除常用造型「{0}」", "Deleted favorite style “{0}”")},
            {"status.noPresetSelected", ("請先從清單選一個常用造型", "Please select a favorite style from the list first")},
            {"section.dimensions", ("尺寸與縫隙", "Size & Grout")},
            {"section.material", ("材質", "Material")},
            {"section.texture", ("紋理", "Texture")},
            {"section.effects", ("效果", "Effects")},

            // 尺寸欄位
            {"field.length", ("長度 (cm)", "Length (cm)")},
            {"field.length.irpoly", ("密度 (cm)", "Density (cm)")},
            {"field.length.edge", ("邊長 (cm)", "Edge (cm)")},
            {"field.width", ("寬度 (cm)", "Width (cm)")},
            {"field.groutWidth", ("縫寬 (cm)", "Grout W (cm)")},
            {"field.groutDepth", ("縫深 (cm)", "Grout D (cm)")},
            {"field.staggerPct", ("錯縫 %", "Stagger %")},
            {"field.tweedAngle", ("斜紋角度", "Tweed Angle")},
            {"field.weaveCount", ("編織數", "Weave Count")},
            {"field.rotation", ("旋轉角度", "Rotation")},
            {"field.startPoint", ("起始點", "Start Point")},

            // 起始點選項
            {"spt.corner", ("轉角", "Corner")},
            {"spt.center", ("中心", "Center")},
            {"spt.pick", ("點選", "Pick")},

            // 材質
            {"field.paintMode", ("上色模式", "Paint Mode")},
            {"paint.current", ("目前材質", "Current Material")},
            {"paint.customColor", ("自訂顏色", "Custom Color")},
            {"paint.texture", ("貼圖材質", "Texture")},
            {"field.tileColor", ("磚片顏色", "Tile Color")},
            {"btn.pickTextures", ("選擇材質圖片…", "Choose Texture Images…")},
            {"status.noTexturesChosen", ("尚未選擇材質圖片（最多 {0} 張）", "No texture images chosen (up to {0})")},
            {"status.texturesChosen", ("已選 {0}／{1} 張材質圖片", "{0}/{1} texture images chosen")},
            {"msg.tooManyTextures", ("最多只能選 {0} 張材質圖片，已略過 {1} 張。", "Up to {0} texture images allowed, {1} skipped.")},

            // 紋理
            {"field.alignEdge", ("對齊邊", "Align Edge")},
            {"field.randomPosition", ("隨機位置", "Random Position")},
            {"field.randomRotate", ("隨機旋轉", "Random Rotate")},

            // 效果
            {"field.bevel", ("倒斜角", "Bevel")},
            {"field.bevelSize", ("角尺寸 (cm)", "Bevel Size (cm)")},
            {"field.randomDefect", ("隨機缺陷", "Random Defect")},
            {"field.defectMin", ("最小角度", "Min Angle")},
            {"field.defectMax", ("最大角度", "Max Angle")},
            {"field.backFace", ("背面", "Back Face")},
            {"field.keepGroup", ("獨立群組", "Keep Group")},

            // 按鈕
            {"btn.pickObject", ("選擇物件", "Pick Object")},
            {"btn.generate", ("產生磚塊", "Generate Tiles")},
            {"btn.undo", ("復原", "Undo")},
            {"btn.calculator", ("材料計算機", "Material Calculator")},
            {"btn.reset", ("重設", "Reset")},
            {"btn.info", ("外掛說明", "Plugin Info")},

            // 狀態列 / 訊息
            {"status.ready", ("請點選「產生磚塊」開始", "Click “Generate Tiles” to start")},
            {"status.cancelled", ("已取消選取", "Selection cancelled")},
            {"status.anchorCancelled", ("已取消選取起磚點", "Start point selection cancelled")},
            {"status.startPointCancelled", ("已取消選取起始點", "Start point selection cancelled")},
            {"status.previewFailed", ("預覽失敗：{0}", "Preview failed: {0}")},
            {"status.previewing", ("預覽中：{0} 片磚（調整參數會即時更新，按「產生磚塊」確認）", "Previewing: {0} tiles (adjust settings to update live, click “Generate Tiles” to confirm)")},
            {"status.generateFailed", ("產生磚塊失敗：{0}", "Failed to generate tiles: {0}")},
            {"status.noTilesGenerated", ("沒有產生任何磚片，請檢查尺寸設定", "No tiles were generated, please check the size settings")},
            {"status.generated", ("已產生 {0} 片磚（{1}）", "Generated {0} tiles ({1})")},
            {"status.noUndo", ("沒有可復原的操作", "Nothing to undo")},
            {"status.undone", ("已復原上一步（剩餘 {0} 步可復原）", "Last step undone ({0} step(s) left to undo)")},
            {"status.calcFailed", ("計算失敗：{0}", "Calculation failed: {0}")},
            {"status.resetDone", ("已重設為預設值", "Reset to defaults")},
            {"msg.notPlanar", ("選取的物件不是平面（面或封閉平面曲線），請重新選取。", "The selected object is not planar (a face or closed planar curve). Please select again.")},

            // 互動選點
            {"prompt.pickFace", ("請選取要鋪磚的平面（面或封閉平面曲線），按 Enter/Esc 取消", "Select a planar face or closed planar curve to tile, Enter/Esc to cancel")},
            {"prompt.dynamicPreview", ("移動滑鼠預覽鋪磚位置，左鍵點擊決定起磚點（Esc 取消）", "Move the mouse to preview the tile layout, left-click to set the start point (Esc to cancel)")},
            {"prompt.pickStartPoint", ("請點選圖案起始點", "Click to set the pattern start point")},

            // 對話框標題
            {"dlg.appName", ("DB3D-Floora", "DB3D-Floora")},
            {"dlg.chooseTextures", ("選擇材質圖片", "Choose Texture Images")},
            {"dlg.imageFiles", ("圖片檔案", "Image Files")},

            // 材料計算機
            {"calc.title", ("材料計算機", "Material Calculator")},
            {"calc.layerStats", ("圖層統計", "Layer Stats")},
            {"calc.noLayers", ("目前模型中沒有 Floora 產生的磚片圖層", "No Floora tile layers found in the current model")},
            {"calc.layerLine", ("{0}：{1} 片，{2:F2} m²（{3:F2} 坪）", "{0}: {1} tiles, {2:F2} m² ({3:F2} ping)")},
            {"calc.total", ("總計：{0:F2} m²（{1:F2} 坪）", "Total: {0:F2} m² ({1:F2} ping)")},
            {"calc.estimate", ("磚片估算", "Tile Estimate")},
            {"field.tileLen", ("磚片長 (cm)", "Tile Length (cm)")},
            {"field.tileWid", ("磚片寬 (cm)", "Tile Width (cm)")},
            {"field.wastePct", ("耗損率 (%)", "Waste (%)")},
            {"field.pcsPerBox", ("每箱片數", "Pieces / Box")},
            {"field.pricePerBox", ("每箱價格", "Price / Box")},
            {"field.pricePerPing", ("每坪價格", "Price / Ping")},
            {"btn.calc", ("計算", "Calculate")},
            {"calc.invalidSize", ("請輸入有效的磚片尺寸", "Please enter a valid tile size")},
            {"calc.result", ("含耗損面積：{0:F2} m²\n需求片數：{1} 片\n需求箱數：{2} 箱\n估價（依箱）：{3:F0}\n估價（依坪）：{4:F0}",
                "Area incl. waste: {0:F2} m²\nTiles needed: {1}\nBoxes needed: {2}\nEstimate (by box): {3:F0}\nEstimate (by ping): {4:F0}")},
            {"btn.exportCsv", ("匯出 CSV", "Export CSV")},
            {"btn.exportImage", ("匯出圖片", "Export Image")},
            {"dlg.exportCsv", ("匯出 CSV", "Export CSV")},
            {"dlg.exportImage", ("匯出圖片", "Export Image")},
            {"dlg.csvFiles", ("CSV 檔案", "CSV Files")},
            {"dlg.pngFiles", ("PNG 圖片", "PNG Image")},
            {"file.calcResultCsv", ("floora_計算結果.csv", "floora_result.csv")},
            {"file.calcResultPng", ("floora_計算結果.png", "floora_result.png")},
            {"msg.exported", ("已匯出：\n{0}", "Exported:\n{0}")},
            {"msg.exportFailed", ("匯出失敗：{0}", "Export failed: {0}")},
            {"calc.resultImageTitle", ("DB3D Floora 材質計算結果", "DB3D Floora Calculation Result")},

            // 外掛說明
            {"info.title", ("外掛說明", "Plugin Info")},
            {"info.appName", ("DB3D Floora for Rhino", "DB3D Floora for Rhino")},
            {"info.usage", ("使用方式", "How to Use")},
            {"info.usage.1.title", ("1. 選圖案與尺寸", "1. Choose a Pattern & Size")},
            {"info.usage.1.a", ("從「圖案」縮圖選擇鋪貼樣式；第一次選圖案時會直接接著跳出選面／即時預覽起磚點流程，不用另外再按「選擇物件」", "Pick a layout style from the “Pattern” thumbnails; the first time you pick a pattern it immediately starts the pick-face / live start-point preview flow, no need to click “Pick Object” separately")},
            {"info.usage.1.b", ("在「尺寸與縫隙」設定長寬、縫寬、縫深", "Set length/width/grout width/grout depth under “Size & Grout”")},
            {"info.usage.1.c", ("部分圖案還會有錯縫%／斜紋角度／編織數、旋轉角度、起始點", "Some patterns also have stagger %, tweed angle, weave count, rotation, and start point")},
            {"info.usage.2.title", ("2. 設定材質", "2. Set Material")},
            {"info.usage.2.a", ("上色模式：目前材質／自訂顏色／貼圖材質", "Paint mode: current material / custom color / texture")},
            {"info.usage.2.b", ("貼圖材質最多可選 {0} 張圖片，縮圖右上角 x 可個別移除", "Up to {0} texture images can be chosen; use the x on each thumbnail to remove it")},
            {"info.usage.3.title", ("3. 紋理（貼圖材質模式）", "3. Texture (texture paint mode)")},
            {"info.usage.3.a", ("對齊邊／隨機位置／隨機旋轉", "Align edge / random position / random rotate")},
            {"info.usage.4.title", ("4. 效果", "4. Effects")},
            {"info.usage.4.a", ("倒斜角、隨機缺陷、背面、獨立群組", "Bevel, random defect, back face, keep group")},
            {"info.usage.5.title", ("5. 選面與預覽", "5. Pick a Face & Preview")},
            {"info.usage.5.a", ("按「選擇物件」選面（或直接選圖案自動觸發），移動滑鼠即時預覽鋪磚位置（純畫面顯示，不會產生真的線段）", "Click “Pick Object” to select a face (or just pick a pattern to trigger it automatically), move the mouse to preview the layout live (display only, no real geometry is created)")},
            {"info.usage.5.b", ("左鍵點擊決定起磚點，按 Esc／右鍵取消不留下任何東西", "Left-click to set the start point; Esc/right-click cancels without leaving anything behind")},
            {"info.usage.5.c", ("起磚點確定後仍可持續調整參數，預覽會即時更新", "After the start point is set you can keep adjusting settings and the preview updates live")},
            {"info.usage.6.title", ("6. 產生與管理", "6. Generate & Manage")},
            {"info.usage.6.a", ("按「產生磚塊」確認並生成正式磚片", "Click “Generate Tiles” to confirm and create the real tiles")},
            {"info.usage.6.b", ("「復原」可撤銷最近 3 次操作；「重設」回到預設值", "“Undo” reverts the last 3 operations; “Reset” restores defaults")},
            {"info.usage.7.title", ("7. 材料計算機", "7. Material Calculator")},
            {"info.usage.7.a", ("統計已產生磚片的面積、估算用量，並可匯出 CSV／圖片", "Tallies the area of generated tiles, estimates quantities, and can export CSV/image")},
            {"info.authors", ("作者介紹", "Credits")},
            {"info.originalPlugin", ("原始外掛：DB3D-Floora（SketchUp 版）", "Original plugin: DB3D-Floora (SketchUp version)")},
            {"info.madeBy", ("製作：DB3D.RENDER", "By: DB3D.RENDER")},
            {"info.website", ("官網：", "Website: ")},
            {"info.instagram", ("Instagram：", "Instagram: ")},
            {"info.rhinoPort", ("Rhino 版製作：Onon.Nihow", "Rhino port by: Onon.Nihow")},
            {"info.github", ("GitHub：", "GitHub: ")},

            // 語言切換按鈕
            {"btn.language", ("🌐 中文/EN", "🌐 中文/EN")},
        };

        public static string T(string key)
        {
            if (Table.TryGetValue(key, out var pair))
                return Current == AppLanguage.ZhTw ? pair.Zh : pair.En;
            return key;
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static void Toggle()
        {
            Current = Current == AppLanguage.ZhTw ? AppLanguage.En : AppLanguage.ZhTw;
        }
    }
}
