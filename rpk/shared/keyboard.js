/**
 * 函数输入软键盘（自研）。
 *
 * 选型理由见 README：手环无法运行 Android IME（如 KittenBoard/「喵喵键盘」），
 * 也没有系统级软键盘 API，因此必须用 hmUI widget 自绘屏内键盘。
 * 针对「函数表达式」场景做优化：数字、运算符、括号、x、π、以及 sin/cos/tan 等
 * 常用函数做成单键快速插入，减少击键次数。
 *
 * 布局（手环 194×368 竖屏，键盘占下半屏，约 4 行）：
 *   行1: 7 8 9 ( )
 *   行2: 4 5 6 × ÷
 *   行3: 1 2 3 + -
 *   行4: 0 . x ^ π
 *   行5: sin cos tan √ ⌫ ←
 */
export const KEY_ROWS = [
  ['7', '8', '9', '(', ')'],
  ['4', '5', '6', '*', '/'],
  ['1', '2', '3', '+', '-'],
  ['0', '.', 'x', '^', 'π'],
  ['sin(', 'cos(', 'tan(', '√(', '⌫']
]

// 快捷函数映射（插入带左括号的文本）
export const KEY_INSERT = {
  'sin(': 'sin(',
  'cos(': 'cos(',
  'tan(': 'tan(',
  '√(': 'sqrt(',
  'π': 'pi'
}

export class SoftKeyboard {
  /**
   * @param {object} ui 注入的 hmUI 对象（createWidget/widget）
   * @param {object} opts
   * @param {number} opts.x, opts.y, opts.w, opts.h 键盘区域
   * @param {(key:string)=>void} opts.onKey 按键回调
   */
  constructor(ui, opts) {
    this.ui = ui
    this.rect = opts
    this.onKey = opts.onKey
    this.widgets = []
    this._build()
  }

  _build() {
    const { createWidget, widget } = this.ui
    const { x, y, w, h } = this.rect
    const rows = KEY_ROWS.length
    const cols = KEY_ROWS[0].length
    const gap = 3
    const keyH = Math.floor((h - gap * (rows + 1)) / rows)
    const keyW = Math.floor((w - gap * (cols + 1)) / cols)

    KEY_ROWS.forEach((row, r) => {
      row.forEach((label, cidx) => {
        const kx = x + gap + cidx * (keyW + gap)
        const ky = y + gap + r * (keyH + gap)
        const btn = createWidget(widget.BUTTON, {
          x: kx, y: ky, w: keyW, h: keyH,
          text: label,
          text_size: Math.floor(keyH * 0.5),
          radius: 12,
          normal_color: 0x1c1e26,
          press_color: 0x2f6bff,
          color: 0xf2f3f7
        })
        const self = this
        btn.addEventListener('click', () => {
          self._handle(label)
        })
        this.widgets.push(btn)
      })
    })
  }

  _handle(label) {
    let insert = label
    if (label === '⌫') {
      this.onKey('BACKSPACE')
      return
    }
    if (KEY_INSERT[label]) insert = KEY_INSERT[label]
    this.onKey(insert)
  }
}
