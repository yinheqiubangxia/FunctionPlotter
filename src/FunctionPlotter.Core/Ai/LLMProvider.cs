using System;
using System.Collections.Generic;

namespace FunctionPlotter.Core.Ai
{
    /// <summary>
    /// 一个 OpenAI 兼容大模型接入点的描述（厂商预设 + 可自定义）。
    /// DeepSeek、Kimi（月之暗面）、小米 MiMo、智谱 GLM 均提供 OpenAI 兼容的
    /// /chat/completions 接口，因此一份客户端代码即可驱动全部模型，
    /// 新增厂商只需要在这里加一条预设。
    /// </summary>
    public sealed class LLMProvider
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string BaseUrl { get; }
        public string DefaultModel { get; }
        public IReadOnlyList<string> SuggestedModels { get; }

        public LLMProvider(string id, string displayName, string baseUrl, string defaultModel, params string[] suggestedModels)
        {
            Id = id;
            DisplayName = displayName;
            BaseUrl = baseUrl;
            DefaultModel = defaultModel;
            SuggestedModels = suggestedModels ?? Array.Empty<string>();
        }

        /// <summary>内置的国产主流大模型预设。均可被用户自定义的 BaseUrl / 模型名覆盖。</summary>
        public static IReadOnlyList<LLMProvider> BuiltIn { get; } = new List<LLMProvider>
        {
            new LLMProvider(
                "deepseek", "DeepSeek（深度求索）",
                "https://api.deepseek.com", "deepseek-chat",
                "deepseek-chat", "deepseek-reasoner"),
            new LLMProvider(
                "kimi", "Kimi（月之暗面 Moonshot）",
                "https://api.moonshot.cn", "moonshot-v1-8k",
                "moonshot-v1-8k", "moonshot-v1-32k", "moonshot-v1-128k", "kimi-k2-0905-preview"),
            new LLMProvider(
                "mimo", "小米 MiMo",
                "https://api.xiaomimimo.com", "mimo-v2-flash",
                "mimo-v2-flash", "mimo-v2-pro"),
            new LLMProvider(
                "zhipu", "智谱 GLM（Zhipu AI）",
                "https://open.bigmodel.cn/api/paas/v4", "glm-4-flash",
                "glm-4-flash", "glm-4.5-flash", "glm-4-air", "glm-4.5"),
            new LLMProvider(
                "custom", "自定义（OpenAI 兼容接口）",
                "", ""),
        };

        public static LLMProvider FindById(string id)
        {
            foreach (LLMProvider p in BuiltIn)
            {
                if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) return p;
            }
            return BuiltIn[0];
        }
    }
}
