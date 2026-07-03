using System.Collections.Generic;

namespace DB3DFloora
{
    /// <summary>使用者自訂的「常用造型」：一個有名字的「圖案＋完整尺寸」組合，存起來以後可以直接套用。</summary>
    public class TilePreset
    {
        public string Name { get; set; }
        public string PatternId { get; set; }
        public TileOptions Opts { get; set; }
    }

    /// <summary>單一圖案目前使用的參數。對應 Python 版用 dict 存的 opts，這裡改用有型別的類別。</summary>
    public class TileOptions
    {
        // 預設為「點選」：選取物件時預設用互動點擊決定自訂起磚點，而不是自動用轉角。
        // 部分圖案（如六邊形／八邊形等只有單一尺寸的圖案）仍會在 DefaultsFor() 裡明確覆寫成 "center"。
        public string Spt = "pick";
        public double Rot = 0.0;
        public double Gx = 30.0;
        public double Gy = 30.0;
        public double Gw = 0.3;
        public double Gd = 0.3;
        public double R2r = 0.0;
        public double Twa = 30.0;
        public int Bwb = 3;

        public TileOptions Clone()
        {
            return (TileOptions)MemberwiseClone();
        }

        /// <summary>依欄位名讀寫數值型參數，供介面上動態產生的 NumericStepper 欄位共用一套 handler。</summary>
        public double GetField(string key)
        {
            switch (key)
            {
                case "gx": return Gx;
                case "gy": return Gy;
                case "gw": return Gw;
                case "gd": return Gd;
                case "r2r": return R2r;
                case "twa": return Twa;
                default: return 0.0;
            }
        }

        public void SetField(string key, double value)
        {
            switch (key)
            {
                case "gx": Gx = value; break;
                case "gy": Gy = value; break;
                case "gw": Gw = value; break;
                case "gd": Gd = value; break;
                case "r2r": R2r = value; break;
                case "twa": Twa = value; break;
            }
        }
    }

    /// <summary>圖案清單、中文名稱、分類與每個圖案的預設參數（對照 Python 版 defaults.py）。</summary>
    public static class Defaults
    {
        public static readonly string[] Patterns =
        {
            "Brick", "Tile", "Wood", "Wedge",
            "Tweed", "Hbone", "Chevrn", "BsktWv",
            "HpScth1", "HpScth2", "HpScth3", "HpScth4",
            "Hexgon", "Octgon", "I_Block", "Diamonds", "FanTil", "IrPoly",
        };

        public static readonly Dictionary<string, string> NamesTw = new Dictionary<string, string>
        {
            {"Brick", "磚縫鋪"},
            {"Tile", "對縫鋪"},
            {"Wood", "隨作鋪"},
            {"Wedge", "楔形"},
            {"Tweed", "斜紋鋪"},
            {"Hbone", "人字鋪"},
            {"Chevrn", "魚骨拼"},
            {"BsktWv", "編織紋"},
            {"HpScth1", "大小格子1"},
            {"HpScth2", "大小格子2"},
            {"HpScth3", "大小格子3"},
            {"HpScth4", "大小格子4"},
            {"Hexgon", "六邊形"},
            {"Octgon", "八邊形"},
            {"I_Block", "工字塊"},
            {"Diamonds", "鑽石紋"},
            {"FanTil", "扇形磚"},
            {"IrPoly", "不規則多邊形"},
        };

        public static readonly Dictionary<string, string> NamesEn = new Dictionary<string, string>
        {
            {"Brick", "Brick"},
            {"Tile", "Grid Tile"},
            {"Wood", "Random Plank"},
            {"Wedge", "Wedge"},
            {"Tweed", "Tweed"},
            {"Hbone", "Herringbone"},
            {"Chevrn", "Chevron"},
            {"BsktWv", "Basket Weave"},
            {"HpScth1", "Hopscotch 1"},
            {"HpScth2", "Hopscotch 2"},
            {"HpScth3", "Hopscotch 3"},
            {"HpScth4", "Hopscotch 4"},
            {"Hexgon", "Hexagon"},
            {"Octgon", "Octagon"},
            {"I_Block", "I-Block"},
            {"Diamonds", "Diamonds"},
            {"FanTil", "Fan Tile"},
            {"IrPoly", "Irregular Polygon"},
        };

        /// <summary>依目前語言（Strings.Current）取得圖案顯示名稱。</summary>
        public static string DisplayName(string patternId)
        {
            var table = Strings.Current == AppLanguage.En ? NamesEn : NamesTw;
            return table.TryGetValue(patternId, out var name) ? name : patternId;
        }

        public static readonly (string Name, string[] Ids)[] CategoriesTw =
        {
            ("基礎", new[] {"Brick", "Tile", "Wood", "Wedge"}),
            ("編織", new[] {"Tweed", "Hbone", "Chevrn", "BsktWv"}),
            ("格子", new[] {"HpScth1", "HpScth2", "HpScth3", "HpScth4"}),
            ("幾何", new[] {"Hexgon", "Octgon", "I_Block", "Diamonds", "FanTil", "IrPoly"}),
        };

        /// <summary>起始點選項的內部值（跟 UI 顯示語言無關），顯示文字用 Strings.T("spt."+value) 取得。</summary>
        public static readonly string[] StartPointValues = { "corner", "center", "pick" };

        /// <summary>這些圖案的縫寬對外觀影響很小/機制不同，介面上不顯示縫寬欄位。</summary>
        public static readonly HashSet<string> NoGwPatterns = new HashSet<string> { "IrPoly" };

        /// <summary>這些圖案不使用寬度 Gy（只靠 Gx 控制單一尺寸），介面上隱藏寬度欄位。</summary>
        public static readonly HashSet<string> NoGyPatterns = new HashSet<string>
        {
            "Hexgon", "Octgon", "Diamonds", "FanTil", "IrPoly",
            "HpScth1", "HpScth2", "HpScth3", "HpScth4",
        };

        /// <summary>介面能設定的最小長寬（cm）。原作者在 v1.0.1 加上這個下限，避免磚片尺寸設太小
        /// 產生過密、不真實的鋪貼結果；同時把每個圖案的預設尺寸都調大到符合實際磚材常見規格。</summary>
        public const double MinSafeDimension = 30.0;

        /// <summary>對照原作者 v1.0.1 的 normalize_safe_dimensions：載入設定時把過小的 Gx／Gy 拉回下限。
        /// FanTil 不套用（扇形磚的 gy 其實是扇形分段角度相關的獨立參數，不是磚片尺寸）；
        /// Hexgon/Octgon/IrPoly/Diamonds 只用 Gx 一個尺寸，不套用 Gy 下限。</summary>
        public static void NormalizeSafeDimensions(string patternId, TileOptions opts)
        {
            if (opts.Gx > 0 && opts.Gx < MinSafeDimension)
                opts.Gx = MinSafeDimension;
            if (patternId == "FanTil" || NoGyPatterns.Contains(patternId))
                return;
            if (opts.Gy > 0 && opts.Gy < MinSafeDimension)
                opts.Gy = MinSafeDimension;
        }

        public static TileOptions DefaultsFor(string patternId)
        {
            var d = new TileOptions();
            switch (patternId)
            {
                case "Brick": d.Gx = 60.0; d.Gy = 30.0; d.R2r = 50.0; break;
                case "Tile": d.Gx = 60.0; d.Gy = 60.0; d.Gd = 0.2; d.R2r = 0.0; break;
                case "Wood": d.Gx = 120.0; d.Gy = 30.0; d.Gw = 0.2; d.Gd = 0.2; break;
                case "Wedge": d.Gx = 60.0; d.Gy = 30.0; d.R2r = 50.0; break;
                case "Tweed": d.Gx = 60.0; d.Gy = 30.0; d.Gw = 0.5; d.Twa = 30.0; break;
                case "Hbone": d.Gx = 60.0; d.Gy = 30.0; break;
                case "Chevrn": d.Gx = 60.0; d.Gy = 30.0; break;
                case "BsktWv": d.Gx = 60.0; d.Gy = 30.0; d.Gw = 0.5; d.Bwb = 3; break;
                case "HpScth1": d.Gx = 30.0; d.Gy = 30.0; d.Gw = 0.5; d.Spt = "center"; break;
                case "HpScth2": d.Gx = 30.0; d.Gy = 30.0; d.Gw = 0.5; d.Spt = "center"; break;
                case "HpScth3": d.Gx = 60.0; d.Gy = 60.0; d.Gd = 0.2; break;
                case "HpScth4": d.Gx = 30.0; d.Gy = 30.0; d.Gd = 0.2; break;
                case "Hexgon": d.Gx = 30.0; d.Spt = "center"; break;
                case "Octgon": d.Gx = 30.0; d.Spt = "center"; break;
                case "I_Block": d.Gx = 60.0; d.Gy = 30.0; d.Gw = 0.5; break;
                case "Diamonds": d.Gx = 30.0; d.Gw = 0.5; d.Spt = "center"; break;
                case "FanTil": d.Gx = 30.0; d.Gy = 24.0; d.Spt = "center"; break;
                case "IrPoly": d.Gx = 30.0; d.Gw = 0.5; d.Spt = "center"; break;
            }
            return d;
        }
    }
}
