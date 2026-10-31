package dev.functionplotter.android.math

import kotlin.math.abs
import kotlin.math.max
import kotlin.math.min

/**
 * 采样器：把编译好的函数在可见 X 区间内采样成若干「段」，
 * 每段是屏幕上可连续绘制的折线。负责两件正确性大事：
 *
 * 1. **无定义区间**：求值得到 NaN / Infinity（如 ln(x) 在 x<=0、1/0）时直接断段，
 *    不会画出穿越无定义区域的假线。
 * 2. **渐近线**：tan(x)、1/x 这类函数在极点两侧函数值符号相反、量级巨大。
 *    通过「相邻采样点连线的屏幕斜率 + 中点回溯验证」识别：
 *    若中点值不在两端之间（或也为无穷），判定为跨极点，断开；
 *    否则是真连续（如 x^2 的陡坡），照常连线。
 */
object Sampler {

    data class Segment(val xs: FloatArray, val ys: FloatArray) {
        val size: Int get() = xs.size
    }

    /**
     * @param f        编译后的函数
     * @param xMin..xMax 可见世界坐标范围
     * @param samples  采样点数（建议 = 画布像素宽度 * 1.5）
     */
    fun sample(
        f: (Double) -> Double,
        xMin: Double,
        xMax: Double,
        samples: Int
    ): List<Segment> {
        val segments = ArrayList<Segment>()
        if (xMax <= xMin || samples < 8) return segments

        val step = (xMax - xMin) / (samples - 1)
        // 屏幕坐标量级估计：用于渐近线判定的 y 阈值
        var curX = ArrayList<Double>(256)
        var curY = ArrayList<Double>(256)

        var prevX = xMin
        var prevY = f(xMin)
        if (prevY.isFinite()) { curX.add(prevX); curY.add(prevY) }

        fun flush() {
            if (curX.size >= 2) {
                segments += Segment(
                    curX.toDoubleArray().toFloatArray(),
                    curY.toDoubleArray().toFloatArray()
                )
            }
            curX = ArrayList(256); curY = ArrayList(256)
        }

        for (i in 1 until samples) {
            val x = xMin + i * step
            val y = f(x)
            if (!y.isFinite()) { flush(); prevY = Double.NaN; prevX = x; continue }

            if (curX.isNotEmpty() && prevY.isFinite()) {
                // 跨极点检测：两端异号且数值都很大，且中点不在两端之间
                if (isAsymptote(f, prevX, prevY, x, y)) flush()
            }
            curX.add(x); curY.add(y)
            prevX = x; prevY = y
        }
        flush()
        return segments
    }

    private fun isAsymptote(
        f: (Double) -> Double,
        x1: Double, y1: Double,
        x2: Double, y2: Double
    ): Boolean {
        val dy = abs(y2 - y1)
        val mag = max(abs(y1), abs(y2))
        // 快速通道：变化平缓，直接认为连续
        if (dy < 1e-6 * (1 + mag)) return false
        // 中点回溯：真极点处中点值会跑到两端区间之外（通常也是异号巨值）
        val midX = (x1 + x2) / 2
        val midY = f(midX)
        if (!midY.isFinite()) return true
        val lo = min(y1, y2); val hi = max(y1, y2)
        val span = max(dy, 1e-12)
        // 中点显著超出两端范围 → 断；异号巨值 → 断
        if (y1 * y2 < 0 && mag > 1e3 * span.coerceAtMost(1.0)) return true
        return midY < lo - 0.5 * span || midY > hi + 0.5 * span
    }

    /** 计算「好看」的网格步长（1/2/5 × 10^n），保证屏幕上网格间距适中。 */
    fun niceStep(rawStep: Double): Double {
        if (rawStep <= 0 || !rawStep.isFinite()) return 1.0
        val exp = kotlin.math.floor(kotlin.math.log10(rawStep))
        val base = Math.pow(10.0, exp)
        val f = rawStep / base
        return when {
            f < 1.5 -> 1.0 * base
            f < 3.5 -> 2.0 * base
            f < 7.5 -> 5.0 * base
            else -> 10.0 * base
        }
    }

    /** 数值格式化：网格标签用，避免 0.30000000000004。 */
    fun formatLabel(v: Double, step: Double): String {
        val decimals = max(0, -kotlin.math.floor(kotlin.math.log10(step)).toInt())
        return if (decimals <= 0) {
            v.toLong().toString()
        } else {
            "%.${min(decimals, 6)}f".format(v).trimEnd('0').trimEnd('.')
        }
    }

    private fun DoubleArray.toFloatArray(): FloatArray {
        val out = FloatArray(size)
        for (i in indices) out[i] = this[i].toFloat()
        return out
    }

    private fun ArrayList<Double>.toDoubleArray(): DoubleArray {
        val out = DoubleArray(size)
        for (i in indices) out[i] = this[i]
        return out
    }
}
