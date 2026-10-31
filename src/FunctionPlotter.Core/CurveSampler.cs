using System;
using System.Collections.Generic;

namespace FunctionPlotter.Core
{
    /// <summary>世界坐标下的一个采样点。</summary>
    public struct PlotPoint
    {
        public PlotPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }
    }

    /// <summary>
    /// 把函数离散成一条或多条折线。遇到 NaN / 无穷 / 跨越渐近线的跳变时断开，
    /// 这样 tan(x)、1/x 这类函数不会被错误地连成一条竖线。
    /// </summary>
    public static class CurveSampler
    {
        public static List<PlotPoint[]> Sample(
            CompiledExpression expression,
            double xMin,
            double xMax,
            double yMin,
            double yMax,
            int sampleCount)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));

            var segments = new List<PlotPoint[]>();
            if (!(xMax > xMin) || sampleCount < 2) return segments;

            double ySpan = Math.Max(yMax - yMin, 1e-9);
            // 画面之外的数值会被夹到这个范围内，避免坐标溢出；同时也用于跳变判定
            double guard = Math.Max(Math.Max(Math.Abs(yMin), Math.Abs(yMax)), ySpan) * 4.0 + 1.0;

            var current = new List<PlotPoint>(sampleCount);
            double step = (xMax - xMin) / (sampleCount - 1);
            double previousY = double.NaN;
            bool hasPrevious = false;

            for (int i = 0; i < sampleCount; i++)
            {
                double x = i == sampleCount - 1 ? xMax : xMin + step * i;
                double y = expression.Evaluate(x);

                if (double.IsNaN(y) || double.IsInfinity(y))
                {
                    Flush(segments, current);
                    hasPrevious = false;
                    continue;
                }

                if (hasPrevious)
                {
                    // 上一帧在画面上方、这一帧在下方（或反之）=> 中间穿过了渐近线
                    bool crossesAsymptote = (previousY > yMax && y < yMin) || (previousY < yMin && y > yMax);
                    // 单步跳变超过可见高度的若干倍，也按断点处理
                    bool extremeJump = Math.Abs(y - previousY) > guard * 2.0;

                    if (crossesAsymptote || extremeJump) Flush(segments, current);
                }

                current.Add(new PlotPoint(x, Clamp(y, -guard, guard)));
                previousY = y;
                hasPrevious = true;
            }

            Flush(segments, current);
            return segments;
        }

        /// <summary>
        /// 估算一个"好看"的 Y 轴范围：忽略极值附近的离群点（tan、1/x 的尖峰），
        /// 让曲线主体撑满画面。
        /// </summary>
        public static bool TryEstimateYRange(
            CompiledExpression expression,
            double xMin,
            double xMax,
            out double yMin,
            out double yMax,
            int sampleCount = 1200)
        {
            yMin = 0;
            yMax = 0;
            if (expression == null || !(xMax > xMin)) return false;

            var values = new List<double>(sampleCount);
            double step = (xMax - xMin) / (sampleCount - 1);
            for (int i = 0; i < sampleCount; i++)
            {
                double x = xMin + step * i;
                double y = expression.Evaluate(x);
                if (double.IsNaN(y) || double.IsInfinity(y)) continue;
                if (Math.Abs(y) > 1e12) continue;
                values.Add(y);
            }

            if (values.Count < 8) return false;
            values.Sort();

            double low = Percentile(values, 0.04);
            double high = Percentile(values, 0.96);

            if (!(high > low))
            {
                // 常函数或几乎水平：给一个固定高度
                double center = values[values.Count / 2];
                low = center - 1.0;
                high = center + 1.0;
            }

            double padding = (high - low) * 0.12;
            low -= padding;
            high += padding;

            // 如果 0 就在附近，把 0 收进来，坐标轴才不会贴着边缘
            double height = high - low;
            if (low > 0 && low < height * 0.35) low = 0;
            if (high < 0 && -high < height * 0.35) high = 0;

            if (!(high > low))
            {
                low -= 1.0;
                high += 1.0;
            }

            yMin = low;
            yMax = high;
            return true;
        }

        private static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            double position = p * (sorted.Count - 1);
            int index = (int)Math.Floor(position);
            double fraction = position - index;
            if (index >= sorted.Count - 1) return sorted[sorted.Count - 1];
            return sorted[index] * (1.0 - fraction) + sorted[index + 1] * fraction;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void Flush(List<PlotPoint[]> segments, List<PlotPoint> current)
        {
            if (current.Count >= 2)
            {
                segments.Add(current.ToArray());
            }
            current.Clear();
        }
    }
}
