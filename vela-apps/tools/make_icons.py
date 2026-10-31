# -*- coding: utf-8 -*-
"""为三个 Vela 应用绘制统一风格图标（HyperOS 柔光玻璃 · 澎湃蓝渐变 + 白色线性符号）。
输出 192x192 PNG，4 倍超采样抗锯齿。"""
import math
import os
from PIL import Image, ImageDraw

BASE = os.path.dirname(os.path.abspath(__file__))
OUT = {
    'plotter': os.path.join(BASE, '..', 'plotter', 'src', 'common', 'icon.png'),
    'pomodoro': os.path.join(BASE, '..', 'pomodoro', 'src', 'common', 'icon.png'),
    'converter': os.path.join(BASE, '..', 'converter', 'src', 'common', 'icon.png'),
}
S = 4            # 超采样倍率
SIZE = 192
W = SIZE * S

TOP = (47, 107, 255)     # #2F6BFF 澎湃蓝
BOT = (91, 140, 255)     # #5B8CFF 亮蓝
WHITE = (255, 255, 255, 255)


def base_tile():
    """圆角方形 + 竖向渐变 + 顶部柔光高光。"""
    img = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    grad = Image.new('RGBA', (W, W))
    gd = ImageDraw.Draw(grad)
    for y in range(W):
        t = y / (W - 1)
        c = tuple(round(TOP[i] + (BOT[i] - TOP[i]) * t) for i in range(3))
        gd.line([(0, y), (W, y)], fill=c + (255,))
    mask = Image.new('L', (W, W), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([0, 0, W - 1, W - 1], radius=int(W * 0.24), fill=255)
    img.paste(grad, (0, 0), mask)
    # 柔光高光：顶部一条半透明白色渐隐带
    hl = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    hd = ImageDraw.Draw(hl)
    for y in range(int(W * 0.42)):
        a = round(46 * (1 - y / (W * 0.42)))
        hd.line([(0, y), (W, y)], fill=(255, 255, 255, a))
    img = Image.alpha_composite(img, Image.composite(hl, Image.new('RGBA', (W, W), (0, 0, 0, 0)), mask))
    return img


def glyph_plotter(img):
    d = ImageDraw.Draw(img)
    lw = int(W * 0.045)
    # 坐标轴
    d.line([(W * 0.22, W * 0.24), (W * 0.22, W * 0.76)], fill=WHITE, width=lw)
    d.line([(W * 0.18, W * 0.72), (W * 0.80, W * 0.72)], fill=WHITE, width=lw)
    # 正弦曲线
    pts = []
    for i in range(121):
        t = i / 120
        x = W * (0.28 + t * 0.50)
        y = W * 0.48 - math.sin(t * math.pi * 2) * W * 0.16
        pts.append((x, y))
    d.line(pts, fill=WHITE, width=lw, joint='curve')


def glyph_pomodoro(img):
    d = ImageDraw.Draw(img)
    lw = int(W * 0.075)
    box = [W * 0.22, W * 0.22, W * 0.78, W * 0.78]
    # 底环（半透明白）
    d.arc(box, 0, 360, fill=(255, 255, 255, 90), width=lw)
    # 进度弧 270°
    d.arc(box, -90, 180, fill=WHITE, width=lw)
    # 中心圆点（表针轴心）
    r = W * 0.045
    d.ellipse([W * 0.5 - r, W * 0.5 - r, W * 0.5 + r, W * 0.5 + r], fill=WHITE)


def glyph_converter(img):
    d = ImageDraw.Draw(img)
    lw = int(W * 0.055)
    # 上行：左 -> 右
    y1 = W * 0.36
    d.line([(W * 0.24, y1), (W * 0.72, y1)], fill=WHITE, width=lw)
    d.line([(W * 0.58, y1 - W * 0.09), (W * 0.74, y1)], fill=WHITE, width=lw)
    d.line([(W * 0.58, y1 + W * 0.09), (W * 0.74, y1)], fill=WHITE, width=lw)
    # 下行：右 -> 左
    y2 = W * 0.64
    d.line([(W * 0.76, y2), (W * 0.28, y2)], fill=WHITE, width=lw)
    d.line([(W * 0.42, y2 - W * 0.09), (W * 0.26, y2)], fill=WHITE, width=lw)
    d.line([(W * 0.42, y2 + W * 0.09), (W * 0.26, y2)], fill=WHITE, width=lw)


def main():
    glyphs = {'plotter': glyph_plotter, 'pomodoro': glyph_pomodoro, 'converter': glyph_converter}
    tiles = []
    for name, path in OUT.items():
        img = base_tile()
        glyphs[name](img)
        img = img.resize((SIZE, SIZE), Image.LANCZOS)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        img.save(path)
        tiles.append(img)
        print('written:', os.path.normpath(path))
    # 三联预览图（深色底，模拟手环桌面）
    sheet = Image.new('RGBA', (SIZE * 3 + 80, SIZE + 40), (12, 13, 18, 255))
    for i, t in enumerate(tiles):
        sheet.paste(t, (20 + i * (SIZE + 20), 20), t)
    preview = os.path.join(BASE, 'icons-preview.png')
    sheet.save(preview)
    print('preview:', preview)


if __name__ == '__main__':
    main()
