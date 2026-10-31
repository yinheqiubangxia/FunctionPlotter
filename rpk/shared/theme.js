/**
 * HyperOS 柔光玻璃主题色板。
 *
 * 手环端深色主题为默认（OLED 省电），浅色主题可选。
 * 说明：Zepp OS 的 canvas 无法做真正的 blur 毛玻璃（无 GPU 后处理），
 * 因此「磨砂质感」通过「半透明叠加层 + 低对比柔和色 + 圆角」来近似实现。
 * 所有颜色为 0xRRGGBB（不透明度由绘制时 alpha 控制）。
 */

const LIGHT = {
  name: 'light',
  bg: 0xf5f6fa,          // 背景（暖白）
  card: 0xffffff,        // 卡片底
  cardAlpha: 0.78,       // 卡片半透明（柔光玻璃）
  text: 0x1a1a1a,        // 主文字
  textSub: 0x8a8f99,     // 次要文字
  axis: 0x3a3f4a,        // 坐标轴
  grid: 0xd0d4dd,        // 网格
  gridAlpha: 0.55,
  curve: 0x2f6bff,       // 曲线主色（澎湃蓝）
  accent: 0x2f6bff,
  danger: 0xe5484d,      // 错误
  key: 0xffffff,         // 键盘按键底
  keyText: 0x1a1a1a,
  keyBorder: 0x1a1a1a
}

const DARK = {
  name: 'dark',
  bg: 0x0c0d12,          // 背景（近黑）
  card: 0x1c1e26,        // 卡片底
  cardAlpha: 0.72,
  text: 0xf2f3f7,
  textSub: 0x8a8f99,
  axis: 0xc8ccd6,
  grid: 0x3a3f4a,
  gridAlpha: 0.6,
  curve: 0x5b8cff,       // 澎湃蓝亮版
  accent: 0x5b8cff,
  danger: 0xff6b6b,
  key: 0x23252e,
  keyText: 0xf2f3f7,
  keyBorder: 0xf2f3f7
}

let current = DARK

export function setTheme(name) {
  current = name === 'light' ? LIGHT : DARK
}

export function getTheme() {
  return current
}
