package dev.functionplotter.android.ai

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map

/** AI 服务商预设：均为 OpenAI 兼容的 /chat/completions 协议。 */
data class AiProvider(
    val id: String,
    val displayName: String,
    val baseUrl: String,
    val defaultModel: String,
    val keyHint: String,
)

object AiProviders {
    val DEEPSEEK = AiProvider(
        id = "deepseek",
        displayName = "DeepSeek",
        baseUrl = "https://api.deepseek.com/v1",
        defaultModel = "deepseek-chat",
        keyHint = "sk-...（platform.deepseek.com 获取）",
    )
    val KIMI = AiProvider(
        id = "kimi",
        displayName = "Kimi (Moonshot)",
        baseUrl = "https://api.moonshot.cn/v1",
        defaultModel = "moonshot-v1-8k",
        keyHint = "sk-...（platform.moonshot.cn 获取）",
    )
    val MIMO = AiProvider(
        id = "mimo",
        displayName = "MiMo (小米)",
        baseUrl = "https://api.xiaomimimo.com/v1",
        defaultModel = "mimo-v1",
        keyHint = "API Key（MiMo 开放平台获取）",
    )
    val CUSTOM = AiProvider(
        id = "custom",
        displayName = "自定义（OpenAI 兼容）",
        baseUrl = "",
        defaultModel = "",
        keyHint = "API Key",
    )

    val ALL = listOf(DEEPSEEK, KIMI, MIMO, CUSTOM)
    fun byId(id: String) = ALL.firstOrNull { it.id == id } ?: DEEPSEEK
}

data class AiSettings(
    val providerId: String = AiProviders.DEEPSEEK.id,
    val apiKey: String = "",
    val baseUrl: String = AiProviders.DEEPSEEK.baseUrl,
    val model: String = AiProviders.DEEPSEEK.defaultModel,
)

private val Context.dataStore by preferencesDataStore(name = "ai_settings")

class AiSettingsRepository(private val context: Context) {
    private val KEY_PROVIDER = stringPreferencesKey("provider_id")
    private val KEY_API_KEY = stringPreferencesKey("api_key")
    private val KEY_BASE_URL = stringPreferencesKey("base_url")
    private val KEY_MODEL = stringPreferencesKey("model")

    val settings = context.dataStore.data.map { p ->
        AiSettings(
            providerId = p[KEY_PROVIDER] ?: AiProviders.DEEPSEEK.id,
            apiKey = p[KEY_API_KEY] ?: "",
            baseUrl = p[KEY_BASE_URL] ?: AiProviders.DEEPSEEK.baseUrl,
            model = p[KEY_MODEL] ?: AiProviders.DEEPSEEK.defaultModel,
        )
    }

    suspend fun current(): AiSettings = settings.first()

    suspend fun save(s: AiSettings) {
        context.dataStore.edit { p ->
            p[KEY_PROVIDER] = s.providerId
            p[KEY_API_KEY] = s.apiKey
            p[KEY_BASE_URL] = s.baseUrl
            p[KEY_MODEL] = s.model
        }
    }
}
