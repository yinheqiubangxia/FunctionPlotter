package dev.functionplotter.android.plot

import androidx.compose.ui.graphics.Color
import androidx.lifecycle.ViewModel
import dev.functionplotter.android.math.ExpressionParser
import dev.functionplotter.android.math.Sampler
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update

/** 一条已添加的函数曲线。 */
data class PlotFunction(
    val id: Long,
    val expression: String,
    val color: Color,
)

/** 视口：世界坐标中心 + 每像素世界单位。 */
data class Viewport(
    val centerX: Double = 0.0,
    val centerY: Double = 0.0,
    val unitsPerPixel: Double = 0.02,
) {
    fun xMin(widthPx: Float) = centerX - unitsPerPixel * widthPx / 2
    fun xMax(widthPx: Float) = centerX + unitsPerPixel * widthPx / 2
    fun yMin(heightPx: Float) = centerY - unitsPerPixel * heightPx / 2
    fun yMax(heightPx: Float) = centerY + unitsPerPixel * heightPx / 2
}

data class PlotUiState(
    val functions: List<PlotFunction> = emptyList(),
    /** 采样结果，与 functions 一一对应。 */
    val segments: Map<Long, List<Sampler.Segment>> = emptyMap(),
    val viewport: Viewport = Viewport(),
    val showGrid: Boolean = true,
    val showAxes: Boolean = true,
    val inputError: String? = null,
    /** AI 建议的待确认表达式，作为虚影实时预览。 */
    val previewExpression: String? = null,
    val previewSegments: List<Sampler.Segment> = emptyList(),
    val previewError: String? = null,
)

class PlotViewModel : ViewModel() {

    private val _uiState = MutableStateFlow(PlotUiState())
    val uiState: StateFlow<PlotUiState> = _uiState.asStateFlow()

    private var nextId = 1L
    private val palette = listOf(
        Color(0xFF7C4DFF), Color(0xFF00BFA5), Color(0xFFFF6D00),
        Color(0xFF2979FF), Color(0xFFD500F9), Color(0xFF64DD17),
    )

    init {
        addFunction("sin(x)")
    }

    /** 添加函数；返回是否成功。 */
    fun addFunction(expression: String): Boolean {
        val error = ExpressionParser.validate(expression)
        if (error != null) {
            _uiState.update { it.copy(inputError = error) }
            return false
        }
        val fn = PlotFunction(
            id = nextId++,
            expression = expression.trim(),
            color = palette[(_uiState.value.functions.size) % palette.size],
        )
        _uiState.update { it.copy(functions = it.functions + fn, inputError = null) }
        resample()
        return true
    }

    fun removeFunction(id: Long) {
        _uiState.update {
            it.copy(
                functions = it.functions.filterNot { f -> f.id == id },
                segments = it.segments - id,
            )
        }
    }

    fun clearInputError() = _uiState.update { it.copy(inputError = null) }

    fun toggleGrid() = _uiState.update { it.copy(showGrid = !it.showGrid) }
    fun toggleAxes() = _uiState.update { it.copy(showAxes = !it.showAxes) }

    fun resetViewport() {
        _uiState.update { it.copy(viewport = Viewport()) }
        resample()
    }

    /** 平移（屏幕像素增量）。 */
    fun pan(dxPx: Float, dyPx: Float) {
        _uiState.update {
            val v = it.viewport
            it.copy(
                viewport = v.copy(
                    centerX = v.centerX - dxPx * v.unitsPerPixel,
                    centerY = v.centerY + dyPx * v.unitsPerPixel,
                )
            )
        }
    }

    /** 以屏幕上某点为锚点缩放。 */
    fun zoom(factor: Float, anchorXPx: Float, anchorYPx: Float, widthPx: Float, heightPx: Float) {
        _uiState.update {
            val v = it.viewport
            val newUpp = (v.unitsPerPixel / factor).coerceIn(1e-12, 1e12)
            // 锚点下的世界坐标在缩放前后保持不变
            val worldX = v.centerX + (anchorXPx - widthPx / 2) * v.unitsPerPixel
            val worldY = v.centerY - (anchorYPx - heightPx / 2) * v.unitsPerPixel
            val newCenterX = worldX - (anchorXPx - widthPx / 2) * newUpp
            val newCenterY = worldY + (anchorYPx - heightPx / 2) * newUpp
            it.copy(viewport = Viewport(newCenterX, newCenterY, newUpp))
        }
    }

    /** 平移/缩放后按当前视口重采样（手势结束时调用）。 */
    fun resample(widthPx: Float = lastWidth, heightPx: Float = lastHeight) {
        if (widthPx <= 0 || heightPx <= 0) return
        lastWidth = widthPx; lastHeight = heightPx
        val v = _uiState.value.viewport
        val xMin = v.xMin(widthPx); val xMax = v.xMax(widthPx)
        val samples = (widthPx * 1.5f).toInt().coerceIn(256, 4096)

        val map = HashMap<Long, List<Sampler.Segment>>()
        for (fn in _uiState.value.functions) {
            val f = runCatching { ExpressionParser.compile(fn.expression) }.getOrNull() ?: continue
            map[fn.id] = Sampler.sample(f, xMin, xMax, samples)
        }
        val preview = _uiState.value.previewExpression
        val previewSegs = preview?.let { expr ->
            runCatching { ExpressionParser.compile(expr) }.getOrNull()
                ?.let { Sampler.sample(it, xMin, xMax, samples) }
        } ?: emptyList()
        _uiState.update { it.copy(segments = map, previewSegments = previewSegs) }
    }

    private var lastWidth = 0f
    private var lastHeight = 0f

    // ---------- AI 预览 ----------

    fun setPreviewExpression(expr: String?) {
        if (expr == null) {
            _uiState.update { it.copy(previewExpression = null, previewSegments = emptyList(), previewError = null) }
            return
        }
        val error = ExpressionParser.validate(expr)
        _uiState.update { it.copy(previewExpression = expr, previewError = error) }
        if (error == null) resample()
    }

    /** 把 AI 预览采纳为正式曲线。 */
    fun acceptPreview() {
        val expr = _uiState.value.previewExpression ?: return
        setPreviewExpression(null)
        addFunction(expr)
    }
}
