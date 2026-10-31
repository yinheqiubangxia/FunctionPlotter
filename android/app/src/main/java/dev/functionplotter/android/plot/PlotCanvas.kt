package dev.functionplotter.android.plot

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.gestures.detectTransformGestures
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.nativeCanvas
import androidx.compose.ui.input.pointer.pointerInput
import dev.functionplotter.android.math.Sampler
import kotlin.math.ceil
import kotlin.math.floor

/**
 * 绘图画布：网格 + 坐标轴 + 分段曲线 + AI 虚影预览。
 * 手势：拖动平移、双指捏合缩放、双击复位。
 */
@Composable
fun PlotCanvas(
    state: PlotUiState,
    onPan: (Float, Float) -> Unit,
    onZoom: (factor: Float, anchorX: Float, anchorY: Float, w: Float, h: Float) -> Unit,
    onGestureEnd: () -> Unit,
    onDoubleTap: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val gridColor = MaterialTheme.colorScheme.outlineVariant
    val axisColor = MaterialTheme.colorScheme.outline
    val labelColor = MaterialTheme.colorScheme.onSurfaceVariant
    val previewColor = MaterialTheme.colorScheme.tertiary

    val currentState by rememberUpdatedState(state)

    Canvas(
        modifier = modifier
            .fillMaxSize()
            .pointerInput(Unit) {
                detectTransformGestures { centroid, pan, zoom, _ ->
                    onPan(pan.x, pan.y)
                    if (zoom != 1f) {
                        onZoom(zoom, centroid.x, centroid.y, size.width.toFloat(), size.height.toFloat())
                    }
                    onGestureEnd()
                }
            }
            .pointerInput(Unit) {
                detectTapGestures(onDoubleTap = { onDoubleTap() })
            }
    ) {
        val v = currentState.viewport
        val w = size.width
        val h = size.height

        fun worldToScreenX(x: Double) = ((x - v.centerX) / v.unitsPerPixel + w / 2).toFloat()
        fun worldToScreenY(y: Double) = (h / 2 - (y - v.centerY) / v.unitsPerPixel).toFloat()

        if (currentState.showGrid) {
            drawGrid(v, w, h, gridColor, labelColor, ::worldToScreenX, ::worldToScreenY)
        }
        if (currentState.showAxes) {
            drawAxes(v, w, h, axisColor, ::worldToScreenX, ::worldToScreenY)
        }

        // 正式曲线
        for (fn in currentState.functions) {
            val segs = currentState.segments[fn.id] ?: continue
            for (seg in segs) {
                drawSegment(seg, fn.color, ::worldToScreenX, ::worldToScreenY)
            }
        }

        // AI 预览（虚线 + 半透明）
        for (seg in currentState.previewSegments) {
            drawSegment(
                seg, previewColor.copy(alpha = 0.8f),
                ::worldToScreenX, ::worldToScreenY,
                pathEffect = PathEffect.dashPathEffect(floatArrayOf(16f, 12f)),
            )
        }
    }
}

private fun DrawScope.drawGrid(
    v: Viewport, w: Float, h: Float,
    gridColor: Color, labelColor: Color,
    wx: (Double) -> Float, wy: (Double) -> Float,
) {
    // 目标屏幕间距 ~80px
    val step = Sampler.niceStep(v.unitsPerPixel * 80)
    val xMin = v.xMin(w); val xMax = v.xMax(w)
    val yMin = v.yMin(h); val yMax = v.yMax(h)

    val textPaint = android.graphics.Paint().apply {
        color = android.graphics.Color.argb(
            (labelColor.alpha * 255).toInt(),
            (labelColor.red * 255).toInt(),
            (labelColor.green * 255).toInt(),
            (labelColor.blue * 255).toInt(),
        )
        textSize = 26f
        isAntiAlias = true
    }

    var x = ceil(xMin / step) * step
    while (x <= xMax) {
        val sx = wx(x)
        drawLine(gridColor, Offset(sx, 0f), Offset(sx, h), strokeWidth = 1f)
        val label = Sampler.formatLabel(x, step)
        val yLabelPos = wy(0.0).coerceIn(20f, h - 10f)
        drawContext.canvas.nativeCanvas.drawText(label, sx + 6f, yLabelPos + 26f, textPaint)
        x += step
    }
    var y = ceil(yMin / step) * step
    while (y <= yMax) {
        val sy = wy(y)
        drawLine(gridColor, Offset(0f, sy), Offset(w, sy), strokeWidth = 1f)
        val label = Sampler.formatLabel(y, step)
        val xLabelPos = wx(0.0).coerceIn(4f, w - 80f)
        drawContext.canvas.nativeCanvas.drawText(label, xLabelPos + 6f, sy - 6f, textPaint)
        y += step
    }
}

private fun DrawScope.drawAxes(
    v: Viewport, w: Float, h: Float,
    axisColor: Color,
    wx: (Double) -> Float, wy: (Double) -> Float,
) {
    val x0 = wx(0.0)
    if (x0 in 0f..w) drawLine(axisColor, Offset(x0, 0f), Offset(x0, h), strokeWidth = 2.5f)
    val y0 = wy(0.0)
    if (y0 in 0f..h) drawLine(axisColor, Offset(0f, y0), Offset(w, y0), strokeWidth = 2.5f)
}

private fun DrawScope.drawSegment(
    seg: Sampler.Segment,
    color: Color,
    wx: (Double) -> Float, wy: (Double) -> Float,
    pathEffect: PathEffect? = null,
) {
    if (seg.size < 2) return
    val path = Path()
    // 世界坐标在 Double，转 Float 屏幕坐标
    val firstX = wx(seg.xs[0].toDouble()); val firstY = wy(seg.ys[0].toDouble())
    path.moveTo(firstX, firstY.coerceScreenY())
    var prevScreenY = firstY
    for (i in 1 until seg.size) {
        val sx = wx(seg.xs[i].toDouble())
        val sy = wy(seg.ys[i].toDouble())
        // 屏幕上跳动超过 4 倍高度视为视觉渐近线，移动而非连线
        if (kotlin.math.abs(sy - prevScreenY) > size.height * 4) {
            path.moveTo(sx, sy.coerceScreenY())
        } else {
            path.lineTo(sx, sy.coerceScreenY())
        }
        prevScreenY = sy
    }
    drawPath(
        path, color,
        style = Stroke(width = 4f, pathEffect = pathEffect),
    )
}

/** 超屏的 y 钳制到屏幕外一点，避免 Float 溢出导致渲染异常。 */
private fun Float.coerceScreenY(): Float = coerceIn(-1e6f, 1e6f)
