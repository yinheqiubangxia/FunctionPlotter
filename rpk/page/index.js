import { createWidget, widget, event } from '@zos/ui'
import { px } from '@zos/utils'
import { log as Logger } from '@zos/utils'

import { PlotRenderer } from '../shared/renderer'
import { GestureController } from '../shared/gesture'
import { SoftKeyboard } from '../shared/keyboard'
import { getTheme, setTheme } from '../shared/theme'

// 手环布局常量（designWidth 194）
const W = px(194)
const H = px(368)

// 分区：顶部表达式显示条、中部画布、底部键盘
const EXPR_H = px(48)
const PLOT_Y = EXPR_H
const PLOT_H = px(180)
const KEY_Y = PLOT_Y + PLOT_H
const KEY_H = H - KEY_Y

Page({
  onInit() {
    this.expr = ''
    this.theme = 'dark'
    setTheme(this.theme)
  },

  build() {
    const t = getTheme()

    // 表达式显示条
    this.exprWidget = createWidget(widget.TEXT, {
      x: px(8), y: px(6), w: px(178), h: px(36),
      text: 'f(x) = ',
      text_size: px(22),
      color: t.text,
      align_h: 'center_h',
      align_v: 'center_v'
    })

    // 画布
    this.canvas = createWidget(widget.CANVAS, {
      x: 0, y: PLOT_Y, w: W, h: PLOT_H
    })

    this.renderer = new PlotRenderer(this.canvas, 0, PLOT_Y, W, PLOT_H)
    this.renderer.redraw()

    // 手势
    this.gesture = new GestureController({
      onPan: (dx, dy) => this._pan(dx, dy),
      onPinch: (scale, cx, cy) => this._pinch(scale, cx, cy),
      onTap: () => {}
    })

    // 画布触控事件
    this.canvas.addEventListener(event.CLICK_DOWN, (info) => {
      this.gesture.down(info.x, info.y)
    })
    this.canvas.addEventListener(event.MOVE, (info) => {
      const touches = info.touches || [{ x: info.x, y: info.y }]
      this.gesture.move(touches)
    })
    this.canvas.addEventListener(event.CLICK_UP, (info) => {
      this.gesture.up(info.x, info.y)
    })

    // 软键盘
    this.keyboard = new SoftKeyboard(
      { createWidget, widget },
      {
        x: 0, y: KEY_Y, w: W, h: KEY_H,
        onKey: (k) => this._onKey(k)
      }
    )

    // 主题切换按钮（简单：右上角小按钮点击切换浅/深）
    this.themeBtn = createWidget(widget.BUTTON, {
      x: px(150), y: px(4), w: px(40), h: px(30),
      text: '🌗', text_size: px(18),
      normal_color: 0x00000000,
      press_color: 0x2f6bff,
      color: t.text
    })
    this.themeBtn.addEventListener('click', () => this._toggleTheme())
  },

  _onKey(key) {
    if (key === 'BACKSPACE') {
      this.expr = this.expr.slice(0, -1)
    } else {
      this.expr += key
    }
    this._refreshExpr()
    this._scheduleRedraw()
  },

  _refreshExpr() {
    this.exprWidget.setProperty('text', 'f(x) = ' + (this.expr || ''))
  },

  _toggleTheme() {
    this.theme = this.theme === 'dark' ? 'light' : 'dark'
    setTheme(this.theme)
    const t = getTheme()
    this.exprWidget.setProperty('color', t.text)
    this.renderer.redraw()
  },

  // 平移：像素位移 → 数据坐标位移，反向（手指右移，视图左移）
  _pan(dxPx, dyPx) {
    const v = this.renderer.view
    const spanX = v.xMax - v.xMin
    const spanY = v.yMax - v.yMin
    const ddx = -(dxPx / PLOT_H) * spanX * 2 // 近似用高度归一化
    const ddy = (dyPx / PLOT_H) * spanY * 2
    this.renderer.setView({
      xMin: v.xMin + ddx, xMax: v.xMax + ddx,
      yMin: v.yMin + ddy, yMax: v.yMax + ddy
    })
    this._scheduleRedraw()
  },

  // 缩放：以焦点 (cx,cy) 为锚点缩放数据坐标
  _pinch(scale, cx, cy) {
    const v = this.renderer.view
    const anchor = this.renderer.toData(cx, cy)
    const spanX = v.xMax - v.xMin
    const spanY = v.yMax - v.yMin
    const newSpanX = spanX / scale
    const newSpanY = spanY / scale
    // 以锚点保持不动
    const ax = anchor.x
    const ay = anchor.y
    this.renderer.setView({
      xMin: ax - (ax - v.xMin) * (newSpanX / spanX),
      xMax: ax + (v.xMax - ax) * (newSpanX / spanX),
      yMin: ay - (ay - v.yMin) * (newSpanY / spanY),
      yMax: ay + (v.yMax - ay) * (newSpanY / spanY)
    })
    this._scheduleRedraw()
  },

  // 节流重绘：最多每 16ms 一次（约 60fps 上限），手环实际会自然降频
  _scheduleRedraw() {
    if (this._redrawTimer) return
    this._redrawTimer = setTimeout(() => {
      this._redrawTimer = null
      this.renderer.setExpr(this.expr)
      this.renderer.redraw()
    }, 16)
  },

  onDestroy() {
    if (this._redrawTimer) clearTimeout(this._redrawTimer)
  }
})
