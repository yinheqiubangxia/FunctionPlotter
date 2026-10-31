using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FunctionPlotter.Core;

namespace FunctionPlotter.Tests
{
    /// <summary>
    /// 轻量自检：在任意平台（包含 Linux/macOS 构建机）上直接 `dotnet run` 即可验证
    /// 解析器、采样器与坐标换算是否正确，不依赖 Windows / WinUI。
    /// </summary>
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            Section("基础四则运算与优先级");
            Near("1+2*3", 7, 0);
            Near("(1+2)*3", 9, 0);
            Near("10/4", 2.5, 0);
            Near("2-3-4", -5, 0);
            Near("2^3^2", 512, 0);
            Near("-2^2", -4, 0);
            Near("2^-1", 0.5, 0);
            Near("(-2)^2", 4, 0);

            Section("隐式乘法");
            Near("2x", 6, 3);
            Near("3(x+1)", 12, 3);
            Near("(x+1)(x-1)", 8, 3);
            Near("2sin(0)", 0, 0, 1e-12);
            Near("x x", 9, 3);
            Near("2 3", 6, 0);

            Section("函数");
            Near("sin(pi/2)", 1, 0);
            Near("cos(0)+tan(0)", 1, 0);
            Near("ln(e)", 1, 0);
            Near("log(100)", 2, 0);
            Near("log(8,2)", 3, 0);
            Near("log2(32)", 5, 0);
            Near("sqrt(16)", 4, 0);
            Near("abs(-3)", 3, 0);
            Near("|x|", 2, -2);
            Near("max(1,2)+min(3,4)", 5, 0);
            Near("mod(7,3)", 1, 0);
            Near("atan2(1,1)", Math.PI / 4, 0);
            Near("hypot(3,4)", 5, 0);
            Near("exp(0)+floor(1.7)+ceil(1.2)", 4, 0);
            Near("round(2.5)", 3, 0);

            Section("常量");
            Near("pi", Math.PI, 0);
            Near("tau", Math.PI * 2, 0);
            Near("e^2", Math.E * Math.E, 0);
            Near("phi", (1 + Math.Sqrt(5)) / 2, 0);

            Section("输入容错");
            Near("y = sin(x)", 1, Math.PI / 2);
            Near("f(x)=2x+1", 7, 3);
            Near("2×3", 6, 0);
            Near("6÷2", 3, 0);
            Near("（x+1）", 4, 3);
            Near("  1.5e2 + 0 ", 150, 0);

            Section("语法错误提示");
            Throws("sin(");
            Throws("foo(x)");
            Throws("1+");
            Throws("");
            Throws("(x+1");
            Throws("x)");
            Throws("2 3 )");
            Throws("sin()");

            Section("采样：连续性处理");
            var sin = ExpressionCompiler.Compile("sin(x)");
            var sinSegments = CurveSampler.Sample(sin, -10, 10, -6, 6, 800);
            Check("sin 是单段曲线", sinSegments.Count == 1, sinSegments.Count.ToString());
            Check("sin 采样点数正确", sinSegments.Sum(s => s.Length) == 800, sinSegments.Sum(s => s.Length).ToString());

            var tan = ExpressionCompiler.Compile("tan(x)");
            var tanSegments = CurveSampler.Sample(tan, -10, 10, -6, 6, 800);
            Check("tan 在渐近线处断开", tanSegments.Count >= 5, tanSegments.Count.ToString());
            Check("tan 不产生竖线", tanSegments.All(s => s.Length > 2), "ok");

            var reciprocal = ExpressionCompiler.Compile("1/x");
            var reciprocalSegments = CurveSampler.Sample(reciprocal, -2, 2, -6, 6, 400);
            Check("1/x 分成左右两支", reciprocalSegments.Count == 2, reciprocalSegments.Count.ToString());

            var sqrt = ExpressionCompiler.Compile("sqrt(x)");
            var sqrtSegments = CurveSampler.Sample(sqrt, -5, 5, -6, 6, 400);
            Check("sqrt(x) 在负半轴无输出", sqrtSegments.Count == 1 && sqrtSegments[0].All(p => p.X >= 0), "ok");

            Section("自适应 Y 轴");
            Check("sin 自适应范围", CurveSampler.TryEstimateYRange(sin, -10, 10, out double y0, out double y1) && y0 > -1.5 && y1 < 1.5 && y1 > 1.0, $"[{y0:0.00},{y1:0.00}]");
            CurveSampler.TryEstimateYRange(tan, -10, 10, out double t0, out double t1);
            Check("tan 自适应范围不被尖峰撑爆", Math.Abs(t1 - t0) < 60, $"[{t0:0.00},{t1:0.00}]");

            Section("坐标换算");
            var view = new Viewport(-10, 10, -5, 5);
            Check("ToPixelX 边界", Math.Abs(view.ToPixelX(-10, 800)) < 1e-9 && Math.Abs(view.ToPixelX(10, 800) - 800) < 1e-9,
                $"{view.ToPixelX(-10, 800):0.##} / {view.ToPixelX(10, 800):0.##}");
            Check("ToPixelY 边界（屏幕 Y 轴向下）", Math.Abs(view.ToPixelY(5, 400)) < 1e-9 && Math.Abs(view.ToPixelY(-5, 400) - 400) < 1e-9,
                $"{view.ToPixelY(5, 400):0.##} / {view.ToPixelY(-5, 400):0.##}");
            Check("像素→世界→像素 往返一致", Math.Abs(view.ToPixelX(view.ToWorldX(317, 800), 800) - 317) < 1e-9, "ok");

            var zoomed = view.ZoomAt(0.5, 0, 0);
            Check("缩放以锚点为中心", Math.Abs(zoomed.ToPixelX(0, 800) - view.ToPixelX(0, 800)) < 1e-9 && Math.Abs(zoomed.Width - 10) < 1e-9, zoomed.ToString());

            var panned = view.Pan(2, -1);
            Check("平移只改变范围", Math.Abs(panned.XMin - (-8)) < 1e-9 && Math.Abs(panned.YMax - 4) < 1e-9, panned.ToString());

            Section("刻度");
            Check("NiceStep 取 1/2/5 系列",
                Math.Abs(AxisTicks.NiceStep(0.7) - 1) < 1e-12 &&
                Math.Abs(AxisTicks.NiceStep(0.3) - 0.5) < 1e-12 &&
                Math.Abs(AxisTicks.NiceStep(3.1) - 5) < 1e-12 &&
                Math.Abs(AxisTicks.NiceStep(0.012) - 0.02) < 1e-12,
                $"{AxisTicks.NiceStep(0.7)} {AxisTicks.NiceStep(0.3)} {AxisTicks.NiceStep(3.1)} {AxisTicks.NiceStep(0.012)}");
            var ticks = AxisTicks.Ticks(-10, 10, 5);
            Check("刻度序列", ticks.Count == 5 && ticks[0] == -10 && ticks[4] == 10, string.Join(",", ticks));
            Check("刻度文字", AxisTicks.FormatLabel(0, 5) == "0" && AxisTicks.FormatLabel(0.5, 0.2) == "0.5" && AxisTicks.FormatLabel(-0.5, 0.2) == "-0.5" && AxisTicks.FormatLabel(1000, 500) == "1000",
                $"{AxisTicks.FormatLabel(0, 5)}|{AxisTicks.FormatLabel(0.5, 0.2)}|{AxisTicks.FormatLabel(-0.5, 0.2)}|{AxisTicks.FormatLabel(1000, 500)}");

            Console.WriteLine();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "通过 {0} 项，失败 {1} 项", _passed, _failed));
            return _failed == 0 ? 0 : 1;
        }

        private static void Section(string name)
        {
            Console.WriteLine();
            Console.WriteLine("== " + name + " ==");
        }

        private static void Near(string expression, double expected, double x, double tolerance = 1e-9)
        {
            try
            {
                double actual = ExpressionCompiler.Compile(expression).Evaluate(x);
                bool ok = Math.Abs(actual - expected) <= tolerance;
                Report(ok, $"{expression}  (x={x})  =>  {actual}", ok ? null : $"期望 {expected}");
            }
            catch (Exception ex)
            {
                Report(false, expression, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Throws(string expression)
        {
            try
            {
                ExpressionCompiler.Compile(expression);
                Report(false, $"应报错: \"{expression}\"", "没有抛出异常");
            }
            catch (ExpressionException ex)
            {
                Report(true, $"报错正确: \"{expression}\" -> {ex.Message}", null);
            }
            catch (Exception ex)
            {
                Report(false, expression, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Check(string name, bool condition, string detail)
        {
            Report(condition, name + "  [" + detail + "]", condition ? null : "断言失败");
        }

        private static void Report(bool ok, string message, string? error)
        {
            if (ok)
            {
                _passed++;
                Console.WriteLine("  PASS  " + message);
            }
            else
            {
                _failed++;
                Console.WriteLine("  FAIL  " + message + (error != null ? "   << " + error : string.Empty));
            }
        }
    }
}
