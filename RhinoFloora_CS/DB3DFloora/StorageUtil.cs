using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DB3DFloora
{
    /// <summary>設定值持久化（對照 Python 版 storage.py），以使用者家目錄下的 JSON 檔
    /// 保存每個圖案的參數與目前選用的圖案。</summary>
    public static class StorageUtil
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".floora_rhino_settings.json");

        private class SettingsData
        {
            public Dictionary<string, TileOptions> Patterns { get; set; } = new Dictionary<string, TileOptions>();
            public string CurrentPattern { get; set; }
            public string Language { get; set; }
            public List<TilePreset> Presets { get; set; } = new List<TilePreset>();
        }

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            // TileOptions（Gx/Gy/Gw/Gd...）是用公開「欄位」寫的，不是屬性。
            // System.Text.Json 預設只序列化屬性，不含欄位，沒開這個的話所有磚片尺寸都會靜默存成空物件，
            // 重新載入時全部退回類別層級的欄位初始值（Gx=30、Gy=30...），使用者調過的尺寸實際上從來沒真的存起來過。
            IncludeFields = true,
        };

        private static SettingsData LoadAll()
        {
            if (!File.Exists(SettingsPath))
                return new SettingsData();
            try
            {
                var text = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<SettingsData>(text, JsonOpts) ?? new SettingsData();
            }
            catch
            {
                return new SettingsData();
            }
        }

        private static void SaveAll(SettingsData data)
        {
            try
            {
                var text = JsonSerializer.Serialize(data, JsonOpts);
                File.WriteAllText(SettingsPath, text);
            }
            catch
            {
                // 存檔失敗不影響外掛其他功能
            }
        }

        /// <summary>取得某圖案上次儲存的參數；沒有存過就回傳 fallback 的複本。
        /// 讀取後一律套用 Defaults.NormalizeSafeDimensions，把舊版存檔裡過小的 Gx／Gy 拉回下限
        /// （對照原作者 v1.0.1 的 load_opts，即使是先前存的設定值也會在載入時修正）。</summary>
        public static TileOptions LoadPatternOpts(string patternId, TileOptions fallback)
        {
            var data = LoadAll();
            TileOptions opts;
            if (data.Patterns != null && data.Patterns.TryGetValue(patternId, out var saved) && saved != null)
                opts = saved;
            else
                opts = fallback.Clone();
            Defaults.NormalizeSafeDimensions(patternId, opts);
            return opts;
        }

        public static void SavePatternOpts(string patternId, TileOptions opts)
        {
            var data = LoadAll();
            data.Patterns ??= new Dictionary<string, TileOptions>();
            data.Patterns[patternId] = opts;
            data.CurrentPattern = patternId;
            SaveAll(data);
        }

        public static string LoadCurrentPattern(string defaultId)
        {
            var data = LoadAll();
            return string.IsNullOrEmpty(data.CurrentPattern) ? defaultId : data.CurrentPattern;
        }

        public static void ResetAll()
        {
            // 語言是介面偏好設定、常用造型是使用者自己收藏的清單，都不是「目前圖案參數」，
            // 「重設」時只清目前各圖案的參數，語言跟常用造型保留。
            var data = LoadAll();
            SaveAll(new SettingsData { Language = data.Language, Presets = data.Presets });
        }

        /// <summary>使用者自訂的「常用造型」：把目前圖案＋完整尺寸參數存成一個有名字的組合，
        /// 之後可以直接套用，不用每次重新調整尺寸。</summary>
        public static List<TilePreset> LoadPresets()
        {
            return LoadAll().Presets ?? new List<TilePreset>();
        }

        /// <summary>新增或覆蓋（同名時）一個常用造型。</summary>
        public static void SavePreset(string name, string patternId, TileOptions opts)
        {
            var data = LoadAll();
            data.Presets ??= new List<TilePreset>();
            data.Presets.RemoveAll(p => p.Name == name);
            data.Presets.Add(new TilePreset { Name = name, PatternId = patternId, Opts = opts.Clone() });
            SaveAll(data);
        }

        public static void DeletePreset(string name)
        {
            var data = LoadAll();
            data.Presets?.RemoveAll(p => p.Name == name);
            SaveAll(data);
        }

        /// <summary>載入使用者上次選的介面語言；沒存過就回傳 fallback。</summary>
        public static AppLanguage LoadLanguage(AppLanguage fallback)
        {
            var lang = LoadAll().Language;
            if (lang == "en")
                return AppLanguage.En;
            if (lang == "zh-tw")
                return AppLanguage.ZhTw;
            return fallback;
        }

        public static void SaveLanguage(AppLanguage lang)
        {
            var data = LoadAll();
            data.Language = lang == AppLanguage.En ? "en" : "zh-tw";
            SaveAll(data);
        }
    }
}
