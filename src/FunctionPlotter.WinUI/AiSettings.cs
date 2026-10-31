using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FunctionPlotter.Core.Ai;

namespace FunctionPlotter.WinUI
{
    /// <summary>
    /// AI 设置的本地持久化：非打包 WinUI 3 应用没有包标识，
    /// 无法使用 ApplicationData / PasswordVault，改为把设置（含各厂商 API Key）
    /// 存到 %APPDATA%\FunctionPlotter\settings.json，文件仅当前用户可读。
    /// </summary>
    public static class AiSettings
    {
        private sealed class Store
        {
            public string ProviderId { get; set; } = "deepseek";
            public string ModelName { get; set; }
            public string CustomBaseUrl { get; set; }
            public Dictionary<string, string> ApiKeys { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly string StoreDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FunctionPlotter");
        private static readonly string StorePath = Path.Combine(StoreDir, "settings.json");

        private static Store _store;

        private static Store Current
        {
            get
            {
                if (_store == null)
                {
                    _store = new Store();
                    try
                    {
                        if (File.Exists(StorePath))
                        {
                            var loaded = JsonSerializer.Deserialize<Store>(File.ReadAllText(StorePath));
                            if (loaded != null) _store = loaded;
                        }
                    }
                    catch { /* 配置损坏时退回默认值 */ }
                }
                return _store;
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(StoreDir);
                File.WriteAllText(StorePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* 保存失败不影响主功能 */ }
        }

        public static string ProviderId
        {
            get => Current.ProviderId;
            set => Current.ProviderId = value;
        }

        public static string ModelName
        {
            get => Current.ModelName;
            set => Current.ModelName = value;
        }

        public static string CustomBaseUrl
        {
            get => Current.CustomBaseUrl;
            set => Current.CustomBaseUrl = value;
        }

        public static LLMProvider CurrentProvider => LLMProvider.FindById(ProviderId);

        public static string EffectiveBaseUrl
        {
            get
            {
                LLMProvider p = CurrentProvider;
                if (p.Id == "custom") return CustomBaseUrl ?? string.Empty;
                return p.BaseUrl;
            }
        }

        public static string EffectiveModel
        {
            get
            {
                string saved = ModelName;
                if (!string.IsNullOrWhiteSpace(saved)) return saved;
                return CurrentProvider.DefaultModel;
            }
        }

        public static string GetApiKey(string providerId)
        {
            return Current.ApiKeys.TryGetValue(providerId, out string key) ? key : string.Empty;
        }

        public static void SetApiKey(string providerId, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Current.ApiKeys.Remove(providerId);
            }
            else
            {
                Current.ApiKeys[providerId] = apiKey.Trim();
            }
            Save();
        }
    }
}
