using System.Collections.Generic;

namespace DB3DFloora
{
    /// <summary>單一圖案目前使用的參數。對應 Python 版用 dict 存的 opts，這裡改用有型別的類別。</summary>
    public class TileOptions
    {
        public string Spt = "corner";
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

        public static readonly (string Name, string[] Ids)[] CategoriesTw =
        {
            ("基礎", new[] {"Brick", "Tile", "Wood", "Wedge"}),
            ("編織", new[] {"Tweed", "Hbone", "Chevrn", "BsktWv"}),
            ("格子", new[] {"HpScth1", "HpScth2", "HpScth3", "HpScth4"}),
            ("幾何", new[] {"Hexgon", "Octgon", "I_Block", "Diamonds", "FanTil", "IrPoly"}),
        };

        public static readonly (string Label, string Value)[] StartPointOptions =
        {
            ("轉角", "corner"),
            ("中心", "center"),
            ("點選", "pick"),
        };

        /// <summary>這些圖案的縫寬對外觀影響很小/機制不同，介面上不顯示縫寬欄位。</summary>
        public static readonly HashSet<string> NoGwPatterns = new HashSet<string> { "IrPoly" };

        /// <summary>這些圖案不使用寬度 Gy（只靠 Gx 控制單一尺寸），介面上隱藏寬度欄位。</summary>
        public static readonly HashSet<string> NoGyPatterns = new HashSet<string>
        {
            "Hexgon", "Octgon", "Diamonds", "FanTil", "IrPoly",
            "HpScth1", "HpScth2", "HpScth3", "HpScth4",
        };

        public static TileOptions DefaultsFor(string patternId)
        {
            var d = new TileOptions();
            switch (patternId)
            {
                case "Brick": d.Gx = 20.0; d.Gy = 10.0; d.R2r = 50.0; break;
                case "Tile": d.Gx = 30.0; d.Gy = 30.0; d.R2r = 0.0; break;
                case "Wood": d.Gx = 90.0; d.Gy = 9.0; break;
                case "Wedge": d.Gx = 20.0; d.Gy = 10.0; d.R2r = 50.0; break;
                case "Tweed": d.Gx = 20.0; d.Gy = 10.0; d.Twa = 30.0; break;
                case "Hbone": d.Gx = 20.0; d.Gy = 5.0; break;
                case "Chevrn": d.Gx = 40.0; d.Gy = 8.0; break;
                case "BsktWv": d.Gx = 20.0; d.Gy = 10.0; d.Bwb = 3; break;
                case "HpScth1": d.Gx = 10.0; d.Spt = "center"; break;
                case "HpScth2": d.Gx = 10.0; d.Spt = "center"; break;
                case "HpScth3": d.Gx = 10.0; d.Spt = "center"; break;
                case "HpScth4": d.Gx = 10.0; d.Spt = "center"; break;
                case "Hexgon": d.Gx = 15.0; d.Spt = "center"; break;
                case "Octgon": d.Gx = 15.0; d.Spt = "center"; break;
                case "I_Block": d.Gx = 20.0; d.Gy = 10.0; break;
                case "Diamonds": d.Gx = 15.0; d.Spt = "center"; break;
                case "FanTil": d.Gx = 15.0; d.Spt = "center"; break;
                case "IrPoly": d.Gx = 20.0; d.Gw = 0.5; d.Spt = "center"; break;
            }
            return d;
        }
    }
}
