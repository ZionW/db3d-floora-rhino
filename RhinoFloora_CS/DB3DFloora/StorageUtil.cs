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
        }

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
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

        /// <summary>取得某圖案上次儲存的參數；沒有存過就回傳 fallback 的複本。</summary>
        public static TileOptions LoadPatternOpts(string patternId, TileOptions fallback)
        {
            var data = LoadAll();
            if (data.Patterns != null && data.Patterns.TryGetValue(patternId, out var saved) && saved != null)
                return saved;
            return fallback.Clone();
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
            SaveAll(new SettingsData());
        }
    }
}
