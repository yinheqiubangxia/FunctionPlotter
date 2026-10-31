using System;
using System.Collections.Generic;
using System.Globalization;

namespace FunctionPlotter.Core
{
    /// <summary>
    /// 可见区域（世界坐标）。纯计算、不可变，便于在 UI 层之外单独测试。
    /// </summary>
    public struct Viewport
    {
        public Viewport(double xMin, double xMax, double yMin, double yMax)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
        }

        public double XMin { get; }

        public double XMax { get; }

        public double YMin { get; }

        public double YMax { get; }

        public double Width => XMax - XMin;

        public double Height => YMax - YMin;

        public static Viewport Default => new Viewport(-10, 10, -6, 6);

        public bool IsValid =>
            XMax > XMin && YMax > YMin &&
            !double.IsNaN(XMin) && !double.IsNaN(XMax) &&
            !double.IsNaN(YMin) && !double.IsNaN(YMax) &&
            !double.IsInfinity(Width) && !double.IsInfinity(Height);

        /// <summary>把世界坐标 X 映射到像素。</summary>
        public double ToPixelX(double worldX, double pixelWidth) => (worldX - XMin) / Width * pixelWidth;

        /// <summary>把世界坐标 Y 映射到像素（屏幕 Y 轴向下，所以这里翻转）。</summary>
        public double ToPixelY(double worldY, double pixelHeight) => pixelHeight - (worldY - YMin) / Height * pixelHeight;

        /// <summary>把像素 X 映射回世界坐标。</summary>
        public double ToWorldX(double pixelX, double pixelWidth) => XMin + pixelX / pixelWidth * Width;

        /// <summary>把像素 Y 映射回世界坐标。</summary>
        public double ToWorldY(double pixelY, double pixelHeight) => YMin + (pixelHeight - pixelY) / pixelHeight * Height;

        /// <summary>以某个世界坐标为锚点缩放。factor &lt; 1 为放大。</summary>
        public Viewport ZoomAt(double factor, double anchorX, double anchorY)
        {
            if (!IsValid || factor <= 0) return this;
            if (Width * factor < 1e-9 || Height * factor < 1e-9) return this;

            return new Viewport(
                anchorX + (XMin - anchorX) * factor,
                anchorX + (XMax - anchorX) * factor,
                anchorY + (YMin - anchorY) * factor,
                anchorY + (YMax - anchorY) * factor);
        }

        /// <summary>平移视图（参数为世界坐标增量）。</summary>
        public Viewport Pan(double deltaX, double deltaY)
        {
            if (!IsValid) return this;
            return new Viewport(XMin + deltaX, XMax + deltaX, YMin + deltaY, YMax + deltaY);
        }

        /// <summary>保持 X 范围不变，替换 Y 范围。</summary>
        public Viewport WithYRange(double yMin, double yMax)
        {
            if (double.IsNaN(yMin) || double.IsNaN(yMax) || !(yMax > yMin)) return this;
            return new Viewport(XMin, XMax, yMin, yMax);
        }

        public Viewport WithXRange(double xMin, double xMax)
        {
            if (double.IsNaN(xMin) || double.IsNaN(xMax) || !(xMax > xMin)) return this;
            return new Viewport(xMin, xMax, YMin, YMax);
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "x[{0:0.###},{1:0.###}] y[{2:0.###},{3:0.###}]", XMin, XMax, YMin, YMax);
    }

    /// <summary>坐标轴刻度：只取 1、2、5 × 10^n 这种"整"的步长。</summary>
    public static class AxisTicks
    {
        public static double NiceStep(double rawStep)
        {
            if (double.IsNaN(rawStep) || double.IsInfinity(rawStep) || rawStep <= 0) return 1.0;

            double exponent = Math.Floor(Math.Log10(rawStep));
            double magnitude = Math.Pow(10, exponent);
            double fraction = rawStep / magnitude;

            double nice;
            if (fraction <= 1.0) nice = 1.0;
            else if (fraction <= 2.0) nice = 2.0;
            else if (fraction <= 5.0) nice = 5.0;
            else nice = 10.0;

            return nice * magnitude;
        }

        /// <summary>列出 [min, max] 内所有 step 的整数倍。</summary>
        public static List<double> Ticks(double min, double max, double step, int maxCount = 200)
        {
            var result = new List<double>();
            if (double.IsNaN(step) || step <= 0 || !(max > min)) return result;

            double first = Math.Ceiling(min / step) * step;
            int guard = 0;
            for (double value = first; value <= max + step * 1e-6 && guard < maxCount; value += step, guard++)
            {
                // 修正浮点累加误差，保证 0 恰好是 0
                result.Add(Math.Abs(value) < step * 1e-9 ? 0.0 : value);
            }

            return result;
        }

        /// <summary>按步长决定刻度文字的小数位数。</summary>
        public static string FormatLabel(double value, double step)
        {
            if (Math.Abs(value) < step * 1e-9) return "0";

            double abs = Math.Abs(value);
            if (abs >= 1e6 || abs < 1e-4)
            {
                return value.ToString("0.###E+0", CultureInfo.InvariantCulture);
            }

            int decimals = step >= 1 ? 0 : (int)Math.Min(6, Math.Ceiling(-Math.Log10(step)));
            decimals = Math.Max(0, decimals);
            return value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }
}
