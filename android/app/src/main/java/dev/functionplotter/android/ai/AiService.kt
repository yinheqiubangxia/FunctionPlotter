package dev.functionplotter.android.ai

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.TimeUnit

/**
 * AI 函数辅助：把自然语言（"画一个心形线"、"x 的三次方减 3x"）
 * 转换成可绘制的表达式列表。协议为 OpenAI 兼容 chat/completions，
 * 预设 DeepSeek / Kimi / MiMo，可自定义任意兼容端点。
 */
object AiService {

    class AiException(message: String) : Exception(message)

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(45, TimeUnit.SECONDS)
        .build()

    private const val SYSTEM_PROMPT = """你是一个数学函数绘图助手。用户用自然语言描述想画的函数，你把它转换成关于 x 的数学表达式。
规则：
1. 只输出 JSON：{"expressions": ["expr1", "expr2"], "note": "可选的一句话说明"}
2. 表达式语法：四则运算、幂用 ^、隐式乘法(如 2x)、函数 sin cos tan asin acos atan sinh cosh tanh exp ln log lg sqrt abs floor ceil、常量 pi e tau phi，绝对值用 abs(x)。
3. 隐式方程（如心形线）无法用 y=f(x) 表示时，给出最接近的显式形式（如上下两半分别一条曲线），并在 note 中说明。
4. 不要输出 JSON 以外的任何内容。"""

    data class AiResult(val expressions: List<String>, val note: String?)

    suspend fun suggest(settings: AiSettings, userPrompt: String): AiResult =
        withContext(Dispatchers.IO) {
            if (settings.apiKey.isBlank()) throw AiException("请先在设置中填写 API Key")
            if (settings.baseUrl.isBlank()) throw AiException("请填写 Base URL")

            val body = JSONObject().apply {
                put("model", settings.model.ifBlank { "deepseek-chat" })
                put("messages", JSONArray().apply {
                    put(JSONObject().put("role", "system").put("content", SYSTEM_PROMPT))
                    put(JSONObject().put("role", "user").put("content", userPrompt))
                })
                put("temperature", 0.3)
            }.toString()

            val request = Request.Builder()
                .url(settings.baseUrl.trimEnd('/') + "/chat/completions")
                .addHeader("Authorization", "Bearer ${settings.apiKey}")
                .addHeader("Content-Type", "application/json")
                .post(body.toRequestBody("application/json".toMediaType()))
                .build()

            client.newCall(request).execute().use { resp ->
                val text = resp.body?.string().orEmpty()
                if (!resp.isSuccessful) {
                    throw AiException("HTTP ${resp.code}：${text.take(200)}")
                }
                val content = JSONObject(text)
                    .getJSONArray("choices")
                    .getJSONObject(0)
                    .getJSONObject("message")
                    .getString("content")
                parseContent(content)
            }
        }

    /** 模型偶尔会套 ```json 代码块，宽容处理。 */
    private fun parseContent(content: String): AiResult {
        val jsonText = content.trim()
            .removePrefix("```json").removePrefix("```")
            .removeSuffix("```").trim()
        val obj = JSONObject(jsonText)
        val arr = obj.optJSONArray("expressions") ?: JSONArray()
        val list = buildList {
            for (i in 0 until arr.length()) add(arr.getString(i))
        }
        if (list.isEmpty()) throw AiException("AI 没有返回可用表达式")
        return AiResult(list, obj.optString("note").ifBlank { null })
    }
}
