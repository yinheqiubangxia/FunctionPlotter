/**
 * 曲线采样器 —— 针对手环低算力深度优化。
 *
 * 关键策略：
 *  1. 采样点数量固定上限（MAX_SAMPLES），且按屏幕像素宽度决定实际点数，绝不超采。
 *  2. 逐点求值，遇到 NaN/无穷/相邻点跳变过大时「断线」，避免 tan(x)、1/x 画出竖线。
 *  3. Y 范围用分位数而非 min/max，单个尖峰不会把曲线压扁。
 */
import { compile } from './parser'

const MAX_SAMPLES = 96 // 手环画布宽度 ~194px，96 点已足够平滑，再高纯属浪费算力
const JUMP_RATIO = 40  // 相邻两点 Y 跳变超过可视范围该倍率 → 判定为渐近线断点

/**
 * 生成「nice」刻度步长：1/2/5 × 10^k
 */
function niceStep(rawStep) {
  if (rawStep <= 0 || !isFinite(rawStep)) return 1
  const pow = Math.pow(10, Math.floor(Math.log10(rawStep)))
  const frac = rawStep / pow
  let nice
  if (frac <= 1) nice = 1
  else if (frac <= 2) nice = 2
  else if (frac <= 5) nice = 5
  else nice = 10
  return nice * pow
}

/**
 * 采样一条曲线。
 * @param {string} expr 表达式文本
 * @param {number} xMin 可视 X 范围左边界
 * @param {number} xMax 可视 X 范围右边界
 * @param {number} widthPx 画布宽度（像素），决定实际采样点数
 * @returns {{ points: Array<{x:number,y:number,break:boolean}>, yMin:number, yMax:number, error:string|null }}
 */
export function sample(expr, xMin, xMax, widthPx) {
  const result = { points: [], yMin: 0, yMax: 0, error: null }
  let f
  try {
    f = compile(expr)
  } catch (e) {
    result.error = e.message
    return result
  }

  // 采样点数：与像素宽度挂钩，但不超过上限
  const n = Math.min(MAX_SAMPLES, Math.max(24, Math.floor(widthPx * 0.5)))
  const ys = []
  const pts = []

  const spanX = xMax - xMin
  let prevY = NaN

  for (let i = 0; i <= n; i++) {
    const x = xMin + (spanX * i) / n
    let y = NaN
    try {
      y = f(x)
    } catch (e) {
      y = NaN
    }

    const finite = isFinite(y)
    let brk = false
    if (!finite) {
      brk = true
    } else if (isFinite(prevY) && ys.length > 0) {
      // 用当前已有点的 Y 范围判断跳变（近似），避免渐近线附近画出竖线
      const curSpan = Math.max(1e-9, yRange(ys))
      if (Math.abs(y - prevY) > curSpan * JUMP_RATIO) {
        brk = true
      }
    }

    pts.push({ x, y: finite ? y : 0, break: brk })
    if (finite) ys.push(y)
    prevY = y
  }

  result.points = pts

  if (ys.length === 0) {
    result.yMin = -1
    result.yMax = 1
    result.error = '该范围内无有效取值'
    return result
  }

  // 分位数 Y 范围（忽略 5% 极值），避免尖峰压扁曲线
  const sorted = ys.slice().sort((a, b) => a - b)
  const lo = sorted[Math.floor(sorted.length * 0.05)]
  const hi = sorted[Math.floor(sorted.length * 0.95)]
  let yMin = lo
  let yMax = hi
  if (yMax - yMin < 1e-6) {
    const pad = Math.max(Math.abs(yMax) * 0.5, 1)
    yMin -= pad
    yMax += pad
  }
  // 留 10% 边距
  const padY = (yMax - yMin) * 0.1
  result.yMin = yMin - padY
  result.yMax = yMax + padY
  return result
}

function yRange(ys) {
  let mn = Infinity
  let mx = -Infinity
  for (let i = 0; i < ys.length; i++) {
    if (ys[i] < mn) mn = ys[i]
    if (ys[i] > mx) mx = ys[i]
  }
  return mx - mn
}

/**
 * 生成坐标轴刻度（供网格/轴标签使用）。
 * @returns {Array<number>}
 */
export function ticks(min, max, count) {
  const step = niceStep((max - min) / count)
  const start = Math.ceil(min / step) * step
  const out = []
  for (let v = start; v <= max + step * 0.001; v += step) {
    out.push(v)
  }
  return out
}

export { niceStep, MAX_SAMPLES }
