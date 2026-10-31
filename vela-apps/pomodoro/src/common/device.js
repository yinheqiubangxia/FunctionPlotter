/**
 * 设备探测与多屏适配公共模块（Vela JS 快应用）。
 *
 * 用法：import { Device } from '../../common/device'
 *
 * 适配思路：
 *  1. 优先用运行时 device.getInfoSync() 拿到真实屏幕宽高与型号；
 *  2. 按「屏幕形态」归类：capsule(胶囊手环) / bandRect(矩形手环Pro) / watchRect(矩形手表)；
 *  3. 已知型号走精确表，未知型号（手环11 / 10 Pro / Watch 6 等未收录设备）按分辨率区间兜底归类；
 *  4. 胶囊屏需要「安全区」内缩，弧形边缘不放置可交互元素。
 */
import device from '@system.device'

// 已知设备精确表（来源：小米 IoT 多屏适配官方文档 iot.mi.com/vela/quickapp）
// shape: 'capsule' | 'rect' ；dpWidth 用于布局密度换算
const KNOWN_DEVICES = [
  { keys: ['M2345B1', 'band9'],        name: '小米手环 9',      w: 192, h: 490, shape: 'capsule', dpWidth: 96 },
  { keys: ['band10'],                  name: '小米手环 10',     w: 212, h: 520, shape: 'capsule', dpWidth: 106 },
  { keys: ['band11'],                  name: '小米手环 11',     w: 212, h: 520, shape: 'capsule', dpWidth: 106 }, // 待官方确认，按 10 同族
  { keys: ['band9pro', 'M2403B1'],     name: '小米手环 9 Pro',  w: 336, h: 480, shape: 'rect',    dpWidth: 168 },
  { keys: ['band10pro'],               name: '小米手环 10 Pro', w: 336, h: 480, shape: 'rect',    dpWidth: 168 }, // 待官方确认，按 9 Pro 同族
  { keys: ['M2427W1', 'watch5'],       name: 'Redmi Watch 5',   w: 432, h: 514, shape: 'rect',    dpWidth: 216 },
  { keys: ['watch6'],                  name: 'Redmi Watch 6',   w: 432, h: 514, shape: 'rect',    dpWidth: 216 }  // 待官方确认，按 5 同族
]

function classify(info) {
  const model = String(info.model || info.product || '').toLowerCase()
  const w = info.screenWidth || info.windowWidth || 192
  const h = info.screenHeight || info.windowHeight || 490

  // 1) 精确匹配
  for (let i = 0; i < KNOWN_DEVICES.length; i++) {
    const d = KNOWN_DEVICES[i]
    for (let k = 0; k < d.keys.length; k++) {
      if (model.indexOf(d.keys[k]) >= 0) return Object.assign({ matched: true }, d)
    }
  }

  // 2) 分辨率兜底归类（覆盖未收录的新型号）
  if (w <= 220 && h >= 480) {
    return { matched: false, name: '未知胶囊手环', w, h, shape: 'capsule', dpWidth: Math.round(w / 2) }
  }
  if (w <= 360) {
    return { matched: false, name: '未知矩形手环', w, h, shape: 'rect', dpWidth: Math.round(w / 2) }
  }
  return { matched: false, name: '未知矩形手表', w, h, shape: 'rect', dpWidth: Math.round(w / 2) }
}

function getDevice() {
  let info = {}
  try {
    info = device.getInfoSync ? device.getInfoSync() : {}
  } catch (e) {
    info = {}
  }
  const d = classify(info)
  // 安全区内缩：胶囊屏弧形边缘上下各缩 12%，左右缩 6%；矩形屏仅上下留状态栏
  if (d.shape === 'capsule') {
    d.safe = {
      x: Math.round(d.w * 0.06),
      y: Math.round(d.h * 0.10),
      w: Math.round(d.w * 0.88),
      h: Math.round(d.h * 0.80)
    }
  } else {
    d.safe = { x: 0, y: 0, w: d.w, h: d.h }
  }
  return d
}

export { getDevice, KNOWN_DEVICES }
