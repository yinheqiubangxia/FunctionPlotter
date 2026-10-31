/**
 * Canvas 渲染器 —— 极简、增量、低重绘频率。
 *
 * 性能要点：
 *   - 只在「视图变化」或「表达式变化」时才重绘，拖动/缩放期间节流。
 *   - 分层：网格+轴 一层，曲线一层。平移时只移动曲线层，缩放时才整体重绘。
 *   - 采样点少（≤96），drawLine 调用次数与点数同阶，无冗余。
 */
import { sample, ticks } from './sampler'
import { getTheme } from './theme'

export class PlotRenderer {
  /**
   * @param {object} canvas Zepp OS canvas widget
   * @param {number} x 画布区域左上角 x
   * @param {number} y 画布区域左上角 y
   * @param {number} w 宽
   * @param {number} h 高
   */
  constructor(canvas, x, y, w, h) {
    this.canvas = canvas
    this.rect = { x, y, w, h }
    this.view = { xMin: -6, xMax: 6, yMin: -4, yMax: 4 } // 数据坐标视口
    this.expr = ''
    this.curveData = null
  }

  setView(v) { this.view = Object.assign(this.view, v) }
  setExpr(expr) { this.expr = expr }

  /** 屏幕坐标 → 数据坐标 */
  toData(px, py) {
    const { x, y, w, h } = this.rect
    const { xMin, xMax, yMin, yMax } = this.view
    const dx = xMin + ((px - x) / w) * (xMax - xMin)
    const dy = yMax - ((py - y) / h) * (yMax - yMin)
    return { x: dx, y: dy }
  }

  /** 数据坐标 → 屏幕坐标 */
  toScreen(dx, dy) {
    const { x, y, w, h } = this.rect
    const { xMin, xMax, yMin, yMax } = this.view
    const px = x + ((dx - xMin) / (xMax - xMin)) * w
    const py = y + ((yMax - dy) / (yMax - yMin)) * h
    return { x: px, y: py }
  }

  /** 全量重绘（表达式或缩放变化时调用） */
  redraw() {
    const c = this.canvas
    const t = getTheme()
    const { x, y, w, h } = this.rect
    const { xMin, xMax, yMin, yMax } = this.view

    // 清空（直接铺背景色覆盖即可，canvas.clear 也保留以兼容）
    c.setPaint({ color: t.bg, line_width: 1 })
    c.drawRect({ x1: x, y1: y, x2: x + w, y2: y + h, color: t.bg })

    // 采样
    const res = sample(this.expr, xMin, xMax, w)
    this.curveData = res

    if (res.error && !this.expr) {
      this.drawEmpty()
      return
    }

    // 若采样得到 Y 范围与当前视图差异过大（用户缩放 X 后），可自适应，但手环上保守：仅在初次或显式适配时改 Y
    this.drawGrid()
    this.drawAxes()
    this.drawCurve(res)
  }

  drawEmpty() {
    const c = this.canvas
    const t = getTheme()
    const { x, y, w, h } = this.rect
    c.drawText({ x: x, y: y + h / 2, w, text: '输入表达式开始绘图', text_size: 16, color: t.textSub })
  }

  drawGrid() {
    const c = this.canvas
    const t = getTheme()
    const { x, y, w, h } = this.rect
    const { xMin, xMax, yMin, yMax } = this.view

    c.setPaint({ color: t.grid, line_width: 1 })
    const xTicks = ticks(xMin, xMax, 6)
    for (let i = 0; i < xTicks.length; i++) {
      const sx = this.toScreen(xTicks[i], 0).x
      c.drawLine({ x1: sx, y1: y, x2: sx, y2: y + h, color: t.grid })
    }
    const yTicks = ticks(yMin, yMax, 5)
    for (let i = 0; i < yTicks.length; i++) {
      const sy = this.toScreen(0, yTicks[i]).y
      c.drawLine({ x1: x, y1: sy, x2: x + w, y2: sy, color: t.grid })
    }
  }

  drawAxes() {
    const c = this.canvas
    const t = getTheme()
    const { x, y, w, h } = this.rect
    const { xMin, xMax, yMin, yMax } = this.view

    c.setPaint({ color: t.axis, line_width: 2 })
    // X 轴（若 y=0 在视口内）
    if (yMin <= 0 && yMax >= 0) {
      const sy = this.toScreen(0, 0).y
      c.drawLine({ x1: x, y1: sy, x2: x + w, y2: sy, color: t.axis })
    }
    // Y 轴（若 x=0 在视口内）
    if (xMin <= 0 && xMax >= 0) {
      const sx = this.toScreen(0, 0).x
      c.drawLine({ x1: sx, y1: y, x2: sx, y2: y + h, color: t.axis })
    }
  }

  drawCurve(res) {
    const c = this.canvas
    const t = getTheme()
    const pts = res.points
    if (!pts || pts.length < 2) return

    c.setPaint({ color: t.curve, line_width: 2 })

    let start = null
    for (let i = 0; i < pts.length; i++) {
      const p = pts[i]
      const sp = this.toScreen(p.x, p.y)
      if (p.break) {
        start = null
        continue
      }
      if (start === null) {
        start = sp
        continue
      }
      // 裁剪：跳过明显在画布外的点，减少无效 drawLine
      c.drawLine({ x1: start.x, y1: start.y, x2: sp.x, y2: sp.y, color: t.curve })
      start = sp
    }
  }
}
