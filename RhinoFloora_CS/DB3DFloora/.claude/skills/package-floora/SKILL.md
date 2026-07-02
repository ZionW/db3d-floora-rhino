---
name: package-floora
description: 重新編譯 DB3DFloora 並打包成單一跨平台的 Yak 安裝檔（.yak，platform any，同時含 net7.0 與 net48）。當使用者要求「打包」「封裝」「發佈」「更新版本」這個 Rhino 外掛時使用。
---

# 打包 DB3DFloora（單一跨平台 Yak 套件）

這個外掛的原始碼在 `DB3DFloora/`（本 skill 所在專案的根目錄），用 McNeel 官方 `Rhino.Templates` 鷹架的 RhinoCommon + Eto.Forms 專案，多目標框架 `net7.0`（Mac／新版 Windows Rhino 8）與 `net48`（Rhino 7／傳統 Windows Rhino 8）。

發佈方式採用 McNeel 現行建議的 **Yak 套件管理員**（不是舊版已停止維護的 `.macrhi`／`.rhi`），而且用官方的**多目標打包格式**（見 [Creating a Multi-Targeted Rhino Plug-In Package](https://developer.rhino3d.com/guides/yak/creating-a-multi-targeted-rhino-plugin-package/)）：net7.0 跟 net48 兩份 build 放進同一個 `.yak` 檔（`net7.0/`、`net48/` 子資料夾＋根目錄一份 `manifest.yml`），產出 `platform: any` 的單一安裝檔，Windows 跟 Mac 使用者都用同一個檔案安裝，Rhino 會自動選對的子資料夾載入。**不要再分開包 mac/win 兩個檔案。**

## 前置設定（每次 Bash 呼叫 dotnet 前都要設定，shell 狀態不會保留）

```bash
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
export PATH="/opt/homebrew/opt/dotnet/bin:$PATH"
```

`yak` 工具已經內建在 Rhino 8 裡，路徑固定：
```
/Applications/Rhino 8.app/Contents/Resources/bin/yak
```

## 打包資料夾結構（`yak-any/`）

```
DB3DFloora/yak-any/
├── manifest.yml          ← 一定要在根目錄，不能放進 net48/ 或 net7.0/ 裡面
├── net48/
│   ├── DB3DFloora.rhp
│   └── icon.png
└── net7.0/
    ├── DB3DFloora.rhp
    └── icon.png
```
`icon.png` 是從 `EmbeddedResources/plugin-utility.ico` 轉出來的（`sips -s format png ... --out icon.png`），manifest.yml 裡用 `icon: icon.png` 參照。兩個子資料夾各放一份一樣的 icon.png。

## 步驟

1. **確認/更新版本號**：打開 `DB3DFloora.csproj`，`<Version>` 這個欄位是 Yak 套件版號的來源。只要程式碼有任何改動要重新封裝，就把版號往上加一碼（例如 `1.0.1` → `1.0.2`）——Yak 用版號分辨新舊，同版號重複安裝容易搞不清楚裝到底是不是最新的。順便確認 `<Company>`／`<Description>` 是不是還是舊的鷹架佔位文字，不是的話一起修掉，這兩個欄位會自動變成 manifest.yml 的 `authors`／`description`。

2. **重新編譯兩個框架**（在 `DB3DFloora/` 目錄下）：
   ```bash
   dotnet build -f net7.0
   dotnet build -f net48
   ```
   兩個都要跑，缺一個框架的建置輸出就會是舊的。確認輸出都是「建置成功、0 個錯誤」。

3. **更新 `yak-any/` 兩個子資料夾裡的 `.rhp`**：
   ```bash
   cd DB3DFloora
   cp bin/Debug/net7.0/DB3DFloora.rhp yak-any/net7.0/DB3DFloora.rhp
   cp bin/Debug/net48/DB3DFloora.rhp yak-any/net48/DB3DFloora.rhp
   ```
   icon.png 如果已經存在就不用動；只有第一次建立 `yak-any/` 資料夾時才需要用 `sips` 從 `EmbeddedResources/plugin-utility.ico` 轉一份。

4. **重新產生／確認 `manifest.yml`**（在 `yak-any/` 目錄下）：
   ```bash
   cd yak-any
   rm -f manifest.yml
   "/Applications/Rhino 8.app/Contents/Resources/bin/yak" spec --input net7.0/DB3DFloora.rhp --output .
   ```
   `yak spec` 只會抓 name/version/authors/description，`url`／`icon`／`keywords` 要手動補回去：
   ```yaml
   url: https://www.db3drender.com/
   icon: icon.png
   keywords:
   - tile
   - flooring
   - pattern
   ```

5. **建置套件**（在 `yak-any/` 目錄下，**不要加 `--platform` 參數**——資料夾裡同時有 `net48/`、`net7.0/` 子資料夾時，yak 會自動偵測成多目標套件並標記 `platform: any`）：
   ```bash
   "/Applications/Rhino 8.app/Contents/Resources/bin/yak" build
   ```
   會產出 `db3dfloora-<version>-rh8_0-any.yak`，終端機輸出應該會顯示套件內容包含 `manifest.yml`、`net48/`、`net7.0/` 三項。

6. **驗證**（在這台 Mac 上安裝一次）：
   ```bash
   "/Applications/Rhino 8.app/Contents/Resources/bin/yak" install db3dfloora-<version>-rh8_0-any.yak
   cat "$HOME/Library/Application Support/McNeel/Rhinoceros/packages/8.0/DB3DFloora/manifest.txt"
   ```
   確認版本號正確。再比對雜湊確認 Rhino 真的選了 net7.0（Mac 應該選這個）：
   ```bash
   shasum -a 256 \
     "$HOME/Library/Application Support/McNeel/Rhinoceros/packages/8.0/DB3DFloora/<version>/DB3DFloora.rhp" \
     "$HOME/Library/Application Support/McNeel/Rhinoceros/packages/8.0/DB3DFloora/<version>/net7.0/DB3DFloora.rhp"
   ```
   根目錄那份 `DB3DFloora.rhp` 的雜湊應該要跟 `net7.0/DB3DFloora.rhp` 一致（代表 Rhino 挑對了框架）。

   net48 那份**無法在這台 Mac 上真的裝進 Windows Rhino 測試**，只能保證編譯與封裝步驟本身沒有出錯。要跟使用者說清楚這個限制，實際 Windows 安裝驗證需要 Windows 環境。

## 交付內容

打包完成後，給使用者**一個**檔案路徑（Windows／Mac 通用）：
```
DB3DFloora/yak-any/db3dfloora-<version>-rh8_0-any.yak
```
使用者把這個檔案拖進 Rhino 視窗，或用 `PackageManager` 指令手動安裝，重開 Rhino 後指令列輸入 `Floora` 即可使用。

## 已知限制／注意事項

- **這個 Rhino 行程裡如果之前已經載入過舊版組件，重新安裝新版套件不會讓已開著的 Rhino 熱更新**（.NET 組件載入後無法卸載）。一定要提醒使用者重開 Rhino 才會套用新版本。
- `yak-any/net48/`、`yak-any/net7.0/` 裡的 `DB3DFloora.rhp` 只是打包用的來源檔複本，每次重新打包都要用步驟 3 的指令覆蓋更新，不要手動改它。
- `manifest.yml` 每次用 `yak spec` 重新產生都會把 `url`／`icon`／`keywords` 洗掉，記得每次都要手動補回去。
- 舊版曾經分開打包過 `yak-mac/`、`yak-win/` 兩個資料夾與對應的 `-mac.yak`／`-win.yak` 檔，已經整合成這個單一 `yak-any/` 資料夾並刪除，不要再重建那兩個舊資料夾。
