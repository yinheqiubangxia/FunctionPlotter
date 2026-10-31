using System;
using Windows.Security.Credentials;
using Windows.Storage;
using FunctionPlotter.Core.Ai;

namespace FunctionPlotter.UWP
{
    /// <summary>
    /// AI 设置的本地持久化：选中的厂商 / 模型名 / 自定义 BaseUrl 存 LocalSettings，
    /// 各厂商的 API Key 存进 Windows 密码保险柜（PasswordVault），不落明文。
    /// </summary>
    public static class AiSettings
    {
        private const string KeyProviderId = "Ai.ProviderId";
        private const string KeyModelName = "Ai.ModelName";
        private const string KeyCustomBaseUrl = "Ai.CustomBaseUrl";
        private const string VaultResource = "FunctionPlotter.AiKey.";

        private static ApplicationDataContainer Local => ApplicationData.Current.LocalSettings;

        public static string ProviderId
        {
            get => Local.Values[KeyProviderId] as string ?? "deepseek";
            set => Local.Values[KeyProviderId] = value;
        }

        public static string ModelName
        {
            get => Local.Values[KeyModelName] as string;
            set => Local.Values[KeyModelName] = value;
        }

        public static string CustomBaseUrl
        {
            get => Local.Values[KeyCustomBaseUrl] as string;
            set => Local.Values[KeyCustomBaseUrl] = value;
        }

        public static LLMProvider CurrentProvider => LLMProvider.FindById(ProviderId);

        /// <summary>当前生效的 BaseUrl（自定义厂商时读用户填的地址）。</summary>
        public static string EffectiveBaseUrl
        {
            get
            {
                LLMProvider p = CurrentProvider;
                if (p.Id == "custom") return CustomBaseUrl ?? string.Empty;
                return p.BaseUrl;
            }
        }

        /// <summary>当前生效的模型名（用户没改过就用厂商默认）。</summary>
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
            try
            {
                var vault = new PasswordVault();
                PasswordCredential credential = vault.Retrieve(VaultResource + providerId, "default");
                credential.RetrievePassword();
                return credential.Password;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static void SetApiKey(string providerId, string apiKey)
        {
            var vault = new PasswordVault();
            string resource = VaultResource + providerId;

            // 先清掉旧的，避免重复条目
            try
            {
                foreach (PasswordCredential old in vault.RetrieveAll())
                {
                    if (old.Resource == resource) vault.Remove(old);
                }
            }
            catch (Exception) { /* 保险柜为空时会抛，忽略 */ }

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                vault.Add(new PasswordCredential(resource, "default", apiKey));
            }
        }
    }
}
