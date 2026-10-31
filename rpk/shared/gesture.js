/**
 * 手势控制器 —— 拖动平移 + 双指捏合缩放。
 *
 * Zepp OS 触控事件：canvas.addEventListener 提供 CLICK_DOWN / CLICK_UP / MOVE，
 * 事件对象带 x/y 坐标。MOVE 在多点触控时携带 touches 数组（含 1~2 个触点）。
 * 双指缩放没有独立 API，需用两个触点间距变化自行计算。
 */
export class GestureController {
  /**
   * @param {object} opts
   * @param {(dxPx:number, dyPx:number)=>void} opts.onPan      单指拖动回调（像素位移）
   * @param {(scale:number, cx:number, cy:number)=>void} opts.onPinch 双指缩放回调（相对上一帧的比例、焦点像素坐标）
   * @param {()=>void} opts.onTap 轻点（无位移的 CLICK_UP）
   */
  constructor(opts) {
    this.opts = opts
    this.state = { mode: 'idle', lastX: 0, lastY: 0, downX: 0, downY: 0, lastDist: 0 }
    this.TAP_SLOP = 6 // 判定为「点击」而非「拖动」的像素阈值
  }

  down(x, y) {
    this.state = {
      mode: 'pan',
      lastX: x, lastY: y,
      downX: x, downY: y,
      lastDist: 0
    }
  }

  /**
   * 多点移动。touches 为触点数组 [{x,y}, ...]。
   */
  move(touches) {
    if (touches && touches.length >= 2) {
      const a = touches[0]
      const b = touches[1]
      const dist = Math.hypot(a.x - b.x, a.y - b.y)
      const cx = (a.x + b.x) / 2
      const cy = (a.y + b.y) / 2
      if (this.state.mode !== 'pinch') {
        this.state.mode = 'pinch'
        this.state.lastDist = dist
        return
      }
      const scale = dist / this.state.lastDist
      this.state.lastDist = dist
      if (this.opts.onPinch && scale > 0 && scale !== 1) {
        this.opts.onPinch(scale, cx, cy)
      }
      return
    }

    if (touches && touches.length === 1) {
      const p = touches[0]
      const dx = p.x - this.state.lastX
      const dy = p.y - this.state.lastY
      this.state.lastX = p.x
      this.state.lastY = p.y
      if (this.opts.onPan) this.opts.onPan(dx, dy)
      return
    }

    // 兼容无 touches 数组的 MOVE 事件（只有单个 x/y）
    const x = this.state.lastX
    // 不做处理，依赖 CLICK_DOWN 记录的触点
    void x
  }

  up(x, y) {
    const dx = Math.abs(x - this.state.downX)
    const dy = Math.abs(y - this.state.downY)
    if (dx < this.TAP_SLOP && dy < this.TAP_SLOP && this.opts.onTap) {
      this.opts.onTap()
    }
    this.state.mode = 'idle'
  }
}
