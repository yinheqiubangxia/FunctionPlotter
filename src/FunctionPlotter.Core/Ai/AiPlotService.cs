using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FunctionPlotter.Core.Ai
{
    /// <summary>AI 解析出的绘图意图。</summary>
    public sealed class AiPlotResult
    {
        public string Expression { get; set; }
        public double? XMin { get; set; }
        public double? XMax { get; set; }
        public string Note { get; set; }
    }

    /// <summary>
    /// 自然语言 → 函数表达式：调用 OpenAI 兼容的 chat/completions 接口，
    /// 让模型输出严格 JSON，再用本项目的 ExpressionCompiler 验证表达式合法性。
    /// 全程零三方依赖（HttpClient + MiniJson），可在 UWP 沙箱中直接运行。
    /// </summary>
    public sealed class AiPlotService
    {
        private const string SystemPrompt =
            "你是一个函数绘图助手的解析后端。用户用自然语言描述想画的函数图像，" +
            "你要把它转换为一个只含变量 x 的数学表达式。\n" +
            "【输出要求】只输出一个 JSON 对象，不要输出任何其他文字、解释或 Markdown 代码围栏。格式：\n" +
            "{\"expression\": \"...\", \"xMin\": 数字或null, \"xMax\": 数字或null, \"note\": \"简短中文说明\"}\n" +
            "【表达式语法】支持 + - * / ^、隐式乘法（如 2x、(x+1)(x-1)）、圆括号、绝对值 |x| 或 abs(x)；\n" +
            "函数：sin cos tan asin acos atan sinh cosh tanh exp log ln log10 sqrt abs sign floor ceil min max；\n" +
            "常量：pi e tau phi。不要使用语法之外的任何符号或函数。\n" +
            "【范围】若用户指定了 x 的范围（如 -2π 到 2π），把它换算成数字填入 xMin/xMax（π≈3.141592653589793）；否则填 null。\n" +
            "【示例】用户说「绘制 y=sin(x) 在 -2π 到 2π 的图像」→ " +
            "{\"expression\": \"sin(x)\", \"xMin\": -6.283185307179586, \"xMax\": 6.283185307179586, \"note\": \"正弦曲线，范围 ±2π\"}";

        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;

        public AiPlotService(string baseUrl, string apiKey, string model, HttpMessageHandler handler = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("缺少接口地址（BaseUrl）", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("请先在设置中填写该模型的 API Key", nameof(apiKey));
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("缺少模型名称", nameof(model));

            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey.Trim();
            _model = model.Trim();
            _http = handler == null ? new HttpClient() : new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(60);
        }

        /// <summary>把一句自然语言描述转换成可绘制的表达式（含可选的 X 范围）。</summary>
        public async Task<AiPlotResult> DescribeToExpressionAsync(string userText, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(userText)) throw new ArgumentException("请先描述想画的函数");

            string requestJson = MiniJson.WriteObject(new Dictionary<string, object>
            {
                ["model"] = _model,
                ["temperature"] = 0.1,
                ["messages"] = new List<object>
                {
                    new Dictionary<string, object> { ["role"] = "system", ["content"] = SystemPrompt },
                    new Dictionary<string, object> { ["role"] = "user", ["content"] = userText },
                },
            });

            string endpoint = _baseUrl + "/chat/completions";
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    throw new InvalidOperationException("无法连接模型服务（" + endpoint + "）：" + ex.Message, ex);
                }

                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, "模型接口返回 {0}：{1}", (int)response.StatusCode, Truncate(body, 300)));
                }

                string content = ExtractMessageContent(body);
                return ParseResult(content);
            }
        }

        private static string ExtractMessageContent(string responseBody)
        {
            object parsed = MiniJson.Parse(responseBody);
            var dict = MiniJson.AsDict(parsed);
            if (dict == null) throw new FormatException("模型响应不是 JSON 对象");

            // OpenAI 标准错误结构
            var error = MiniJson.AsDict(dict.TryGetValue("error", out object e) ? e : null);
            if (error != null)
            {
                throw new InvalidOperationException("模型接口报错：" + (MiniJson.GetString(error, "message") ?? responseBody));
            }

            var choices = MiniJson.AsList(dict.TryGetValue("choices", out object c) ? c : null);
            if (choices == null || choices.Count == 0) throw new FormatException("模型响应中没有 choices 字段");

            var message = MiniJson.AsDict(MiniJson.AsDict(choices[0])?.TryGetValue("message", out object m) == true ? m : null);
            string content = MiniJson.GetString(message, "content");
            if (string.IsNullOrWhiteSpace(content)) throw new FormatException("模型返回了空内容");
            return content;
        }

        /// <summary>
        /// 解析模型输出：容忍模型偶尔带上 ```json 围栏或前后多余文字，
        /// 截取第一个 { 到最后一个 } 之间的部分解析，并用表达式编译器做合法性校验。
        /// </summary>
        public static AiPlotResult ParseResult(string modelContent)
        {
            string json = ExtractJsonObject(modelContent);
            var dict = MiniJson.AsDict(MiniJson.Parse(json));
            if (dict == null) throw new FormatException("模型输出的 JSON 无法解析");

            var result = new AiPlotResult
            {
                Expression = MiniJson.GetString(dict, "expression"),
                XMin = MiniJson.GetNumber(dict, "xMin"),
                XMax = MiniJson.GetNumber(dict, "xMax"),
                Note = MiniJson.GetString(dict, "note"),
            };

            if (string.IsNullOrWhiteSpace(result.Expression))
                throw new FormatException("模型没有给出 expression 字段");

            // 关键一步：用我们自己的编译器验证，模型瞎编的语法在这里被拦下
            ExpressionCompiler.Compile(result.Expression);

            if (result.XMin.HasValue && result.XMax.HasValue && result.XMin.Value >= result.XMax.Value)
            {
                result.XMin = null;
                result.XMax = null;
            }

            return result;
        }

        private static string ExtractJsonObject(string text)
        {
            if (text == null) throw new FormatException("模型返回为空");
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
                throw new FormatException("模型输出中找不到 JSON 对象：" + Truncate(text, 200));
            return text.Substring(start, end - start + 1);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
