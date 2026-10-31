package dev.functionplotter.android.ai

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class AiUiState(
    val settings: AiSettings = AiSettings(),
    val loading: Boolean = false,
    val error: String? = null,
    val suggestions: List<String> = emptyList(),
    val note: String? = null,
    /** 当前选中用于实时预览的建议下标，-1 表示未选中。 */
    val selectedIndex: Int = -1,
)

class AiViewModel(app: Application) : AndroidViewModel(app) {

    private val repo = AiSettingsRepository(app)

    private val _uiState = MutableStateFlow(AiUiState())
    val uiState: StateFlow<AiUiState> = _uiState.asStateFlow()

    private var requestJob: Job? = null

    init {
        viewModelScope.launch {
            repo.settings.collect { s -> _uiState.update { it.copy(settings = s) } }
        }
    }

    fun saveSettings(s: AiSettings) {
        _uiState.update { it.copy(settings = s) }
        viewModelScope.launch { repo.save(s) }
    }

    /** 发起 AI 请求；拿到建议后自动选中第一条供画布实时预览。 */
    fun ask(prompt: String, onPreview: (String?) -> Unit) {
        if (prompt.isBlank()) return
        requestJob?.cancel()
        requestJob = viewModelScope.launch {
            _uiState.update { it.copy(loading = true, error = null, suggestions = emptyList(), note = null) }
            try {
                val result = AiService.suggest(_uiState.value.settings, prompt)
                _uiState.update {
                    it.copy(loading = false, suggestions = result.expressions, note = result.note, selectedIndex = 0)
                }
                onPreview(result.expressions.firstOrNull())
            } catch (e: AiService.AiException) {
                _uiState.update { it.copy(loading = false, error = e.message) }
            } catch (e: Exception) {
                _uiState.update { it.copy(loading = false, error = "网络错误：${e.message}") }
            }
        }
    }

    /** 点击某条建议 → 画布实时切换虚影预览。 */
    fun selectSuggestion(index: Int, onPreview: (String?) -> Unit) {
        val s = _uiState.value
        val expr = s.suggestions.getOrNull(index)
        _uiState.update { it.copy(selectedIndex = index) }
        onPreview(expr)
    }

    fun clearResult() {
        _uiState.update { it.copy(suggestions = emptyList(), note = null, error = null, selectedIndex = -1) }
    }
}
