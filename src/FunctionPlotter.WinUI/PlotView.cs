using System;
using System.Collections.Generic;
using System.Globalization;
using FunctionPlotter.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace FunctionPlotter.WinUI
{
    /// <summary>
    /// 纯 WinUI 自绘的坐标系控件：网格、坐标轴、刻度、曲线、十字光标。
    /// 没有引入 Win2D 之类的额外依赖，直接使用 XAML 矢量图形，
    /// 因此在 UWP / WinUI 3 之间迁移时只需要换命名空间。
    /// </summary>
    public sealed class PlotView : Canvas
    {
        private const double XDivisionWidth = 80;   // 期望每个纵向网格的像素宽度
        private const double YDivisionHeight = 58;  // 期望每个横向网格的像素高度

        private readonly List<PlotPoint[]> _segments = new List<PlotPoint[]>();
        private readonly Line _crossVertical = new Line();
        private readonly Line _crossHorizontal = new Line();
        private readonly TextBlock _readoutText = new TextBlock();
        private readonly Border _readout = new Border();

        private CompiledExpression _expression;
        private Viewport _viewport = Viewport.Default;

        private bool _isPanning;
        private Point _panOrigin;
        private Point _pointerPosition;
        private bool _hasPointer;

        public PlotView()
        {
            IsHitTestVisible = true;
            UseLayoutRounding = false;

            _crossVertical.StrokeThickness = 1;
            _crossHorizontal.StrokeThickness = 1;
            _crossVertical.IsHitTestVisible = false;
            _crossHorizontal.IsHitTestVisible = false;
            var dash = new DoubleCollection { 4, 4 };
            _crossVertical.StrokeDashArray = dash;
            _crossHorizontal.StrokeDashArray = dash;

            _readoutText.FontSize = 12;
            _readoutText.FontFamily = new FontFamily("Consolas");
            _readoutText.LineHeight = 17;
            _readoutText.IsHitTestVisible = false;

            _readout.CornerRadius = new CornerRadius(6);
            _readout.BorderThickness = new Thickness(1);
            _readout.Padding = new Thickness(8, 5, 8, 5);
            _readout.Child = _readoutText;
            _readout.IsHitTestVisible = false;
            _readout.Visibility = Visibility.Collapsed;

            SizeChanged += (_, __) => Redraw();
            ActualThemeChanged += (_, __) => Redraw();

            PointerWheelChanged += OnPointerWheelChanged;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += (_, __) => _isPanning = false;
            PointerExited += (_, __) =>
            {
                _hasPointer = false;
                _crossVertical.Visibility = Visibility.Collapsed;
                _crossHorizontal.Visibility = Visibility.Collapsed;
                _readout.Visibility = Visibility.Collapsed;
            };
            DoubleTapped += (_, __) => FitView();
        }

        /// <summary>绘制状态变化时通知外部（状态栏文字）。</summary>
        public event Action<string> StatusChanged;

        public string ErrorMessage { get; private set; } = string.Empty;

        public Viewport Viewport => _viewport;

        /// <summary>设置（或重新解析）函数表达式，随即重绘。输入过程中逐字符调用也不会卡。</summary>
        public void SetExpression(string text)
        {
            try
            {
                _expression = ExpressionCompiler.Compile(text ?? string.Empty);
                ErrorMessage = null;
            }
            catch (ExpressionException ex)
            {
                _expression = null;
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                _expression = null;
                ErrorMessage = ex.Message;
            }

            Redraw();
            RaiseStatus();
        }

        /// <summary>按当前 X 范围自动适配 Y 轴。</summary>
        public void FitView()
        {
            if (_expression != null &&
                CurveSampler.TryEstimateYRange(_expression, _viewport.XMin, _viewport.XMax, out double yMin, out double yMax))
            {
                _viewport = _viewport.WithYRange(yMin, yMax);
            }
            else
            {
                _viewport = _viewport.WithYRange(-6, 6);
            }

            Redraw();
            RaiseStatus();
        }

        /// <summary>回到默认视图：x ∈ [-10, 10]。</summary>
        public void ResetView()
        {
            _viewport = Viewport.Default;
            if (_expression != null)
            {
                _viewport = _viewport.WithXRange(-10, 10);
                FitViewInternal();
            }
            Redraw();
            RaiseStatus();
        }

        public void ZoomBy(double factor)
        {
            _viewport = _viewport.ZoomAt(factor, (_viewport.XMin + _viewport.XMax) / 2, (_viewport.YMin + _viewport.YMax) / 2);
            Redraw();
            RaiseStatus();
        }

        /// <summary>把 X 轴范围设为指定区间并重新适配 Y 轴（AI 绘图给出范围时使用）。</summary>
        public void SetXRange(double xMin, double xMax)
        {
            if (double.IsNaN(xMin) || double.IsNaN(xMax) || double.IsInfinity(xMin) || double.IsInfinity(xMax)) return;
            if (xMin >= xMax) return;
            _viewport = _viewport.WithXRange(xMin, xMax);
            FitViewInternal();
            Redraw();
            RaiseStatus();
        }

        /// <summary>
        /// 为打印创建一个离线副本：同一表达式、同一视口、同一绘制管线，
        /// 保证打印输出与屏幕所见一致（所见即所得）。
        /// </summary>
        public PlotView CreatePrintClone()
        {
            return new PlotView
            {
                _expression = _expression,
                _viewport = _viewport,
            };
        }

        /// <summary>打印时隐藏交互层（十字光标与读数框不属于图像本身）。</summary>
        public void HideCursorLayerForPrint()
        {
            _hasPointer = false;
            Redraw();
        }

        private void FitViewInternal()
        {
            if (_expression != null &&
                CurveSampler.TryEstimateYRange(_expression, _viewport.XMin, _viewport.XMax, out double yMin, out double yMax))
            {
                _viewport = _viewport.WithYRange(yMin, yMax);
            }
        }

        private void RaiseStatus()
        {
            string message;
            if (ErrorMessage != null)
            {
                message = ErrorMessage;
            }
            else if (_expression == null)
            {
                message = "输入一个关于 x 的表达式";
            }
            else
            {
                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "已绘制 · x ∈ [{0:0.###}, {1:0.###}] · y ∈ [{2:0.###}, {3:0.###}]",
                    _viewport.XMin, _viewport.XMax, _viewport.YMin, _viewport.YMax);
            }

            StatusChanged?.Invoke(message);
        }

        #region 绘制

        private void Redraw()
        {
            Children.Clear();

            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 12 || height < 12) return;

            Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
            var palette = Palette.For(ActualTheme);
            Background = new SolidColorBrush(palette.Background);

            _segments.Clear();
            if (_expression != null && _viewport.IsValid)
            {
                int samples = Math.Max(240, Math.Min(4000, (int)(width * 1.8)));
                _segments.AddRange(CurveSampler.Sample(_expression, _viewport.XMin, _viewport.XMax, _viewport.YMin, _viewport.YMax, samples));
            }

            DrawGridAndAxes(palette, width, height);
            DrawCurve(palette, width, height);
            DrawCursorLayer(palette, width, height);
        }

        private void DrawGridAndAxes(Palette palette, double width, double height)
        {
            var gridBrush = new SolidColorBrush(palette.Grid);
            var axisBrush = new SolidColorBrush(palette.Axis);
            var textBrush = new SolidColorBrush(palette.Text);

            double xStep = AxisTicks.NiceStep(_viewport.Width / Math.Max(3, Math.Round(width / XDivisionWidth)));
            double yStep = AxisTicks.NiceStep(_viewport.Height / Math.Max(3, Math.Round(height / YDivisionHeight)));

            double axisX = _viewport.ToPixelX(0, width);
            double axisY = _viewport.ToPixelY(0, height);
            bool axisXVisible = axisX >= 0 && axisX <= width;
            bool axisYVisible = axisY >= 0 && axisY <= height;

            // 网格
            foreach (double value in AxisTicks.Ticks(_viewport.XMin, _viewport.XMax, xStep))
            {
                double px = Math.Round(_viewport.ToPixelX(value, width)) + 0.5;
                if (px < 0 || px > width) continue;
                AddLine(px, 0, px, height, gridBrush, 1);
            }

            foreach (double value in AxisTicks.Ticks(_viewport.YMin, _viewport.YMax, yStep))
            {
                double py = Math.Round(_viewport.ToPixelY(value, height)) + 0.5;
                if (py < 0 || py > height) continue;
                AddLine(0, py, width, py, gridBrush, 1);
            }

            // 坐标轴
            if (axisXVisible) AddLine(Math.Round(axisX) + 0.5, 0, Math.Round(axisX) + 0.5, height, axisBrush, 1.4);
            if (axisYVisible) AddLine(0, Math.Round(axisY) + 0.5, width, Math.Round(axisY) + 0.5, axisBrush, 1.4);

            // 刻度文字：轴不在视野内时贴边显示，避免标签跟着跑出画面
            double labelTop = axisYVisible ? Math.Min(axisY + 5, height - 17) : (axisY > height ? height - 17 : 3);
            foreach (double value in AxisTicks.Ticks(_viewport.XMin, _viewport.XMax, xStep))
            {
                double px = _viewport.ToPixelX(value, width);
                if (px < 12 || px > width - 6) continue;

                var label = CreateLabel(AxisTicks.FormatLabel(value, xStep), textBrush);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                SetLeft(label, Math.Round(px - label.DesiredSize.Width / 2));
                SetTop(label, Math.Round(labelTop));
                Children.Add(label);
            }

            double labelLeft = axisXVisible ? Math.Min(axisX + 5, width - 40) : (axisX > width ? width - 42 : 4);
            foreach (double value in AxisTicks.Ticks(_viewport.YMin, _viewport.YMax, yStep))
            {
                double py = _viewport.ToPixelY(value, height);
                if (py < 8 || py > height - 14) continue;
                // 原点由 X 轴的 "0" 标出，这里跳过避免两个 0 叠在一起
                if (value == 0 && axisXVisible) continue;

                var label = CreateLabel(AxisTicks.FormatLabel(value, yStep), textBrush);
                SetLeft(label, Math.Round(labelLeft));
                SetTop(label, Math.Round(py - 8));
                Children.Add(label);
            }
        }

        private void DrawCurve(Palette palette, double width, double height)
        {
            if (_segments.Count == 0) return;

            var stroke = new SolidColorBrush(palette.Curve);
            foreach (PlotPoint[] segment in _segments)
            {
                var points = new PointCollection();
                foreach (PlotPoint point in segment)
                {
                    points.Add(new Point(_viewport.ToPixelX(point.X, width), _viewport.ToPixelY(point.Y, height)));
                }

                var polyline = new Polyline
                {
                    Stroke = stroke,
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    IsHitTestVisible = false,
                    Points = points,
                };

                Children.Add(polyline);
            }
        }

        private void DrawCursorLayer(Palette palette, double width, double height)
        {
            var crossBrush = new SolidColorBrush(palette.Crosshair);
            _crossVertical.Stroke = crossBrush;
            _crossHorizontal.Stroke = crossBrush;
            _crossVertical.X1 = 0;
            _crossVertical.Y1 = 0;
            _crossVertical.X2 = 0;
            _crossVertical.Y2 = height;
            _crossHorizontal.X1 = 0;
            _crossHorizontal.Y1 = 0;
            _crossHorizontal.X2 = width;
            _crossHorizontal.Y2 = 0;

            _readout.Background = new SolidColorBrush(palette.ReadoutBackground);
            _readout.BorderBrush = new SolidColorBrush(palette.Grid);
            _readoutText.Foreground = new SolidColorBrush(palette.Text);

            Children.Add(_crossVertical);
            Children.Add(_crossHorizontal);
            Children.Add(_readout);

            if (_hasPointer)
            {
                UpdateCursorLayer(width, height);
            }
            else
            {
                _crossVertical.Visibility = Visibility.Collapsed;
                _crossHorizontal.Visibility = Visibility.Collapsed;
                _readout.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateCursorLayer(double width, double height)
        {
            double x = Math.Max(0, Math.Min(width, _pointerPosition.X));
            double y = Math.Max(0, Math.Min(height, _pointerPosition.Y));

            _crossVertical.Visibility = Visibility.Visible;
            _crossHorizontal.Visibility = Visibility.Visible;
            _crossVertical.X1 = Math.Round(x) + 0.5;
            _crossVertical.X2 = Math.Round(x) + 0.5;
            _crossHorizontal.Y1 = Math.Round(y) + 0.5;
            _crossHorizontal.Y2 = Math.Round(y) + 0.5;

            double worldX = _viewport.ToWorldX(x, width);
            double worldY = _viewport.ToWorldY(y, height);

            string valueText;
            if (_expression != null)
            {
                double value = _expression.Evaluate(worldX);
                valueText = double.IsNaN(value) || double.IsInfinity(value)
                    ? "f(x) = 无定义"
                    : "f(x) = " + value.ToString("0.####", CultureInfo.InvariantCulture);
            }
            else
            {
                valueText = "f(x) = —";
            }

            _readoutText.Text = "x = " + worldX.ToString("0.####", CultureInfo.InvariantCulture) + "\n" + valueText;
            _readout.Visibility = Visibility.Visible;
            _readout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double boxWidth = _readout.DesiredSize.Width;
            double boxHeight = _readout.DesiredSize.Height;
            double left = x + 16;
            double top = y + 16;
            if (left + boxWidth > width - 4) left = x - boxWidth - 16;
            if (top + boxHeight > height - 4) top = y - boxHeight - 16;

            SetLeft(_readout, Math.Max(4, Math.Round(left)));
            SetTop(_readout, Math.Max(4, Math.Round(top)));
        }

        private void AddLine(double x1, double y1, double x2, double y2, Brush stroke, double thickness)
        {
            Children.Add(new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = stroke,
                StrokeThickness = thickness,
                IsHitTestVisible = false,
            });
        }

        private static TextBlock CreateLabel(string text, Brush foreground)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = foreground,
                IsHitTestVisible = false,
            };
        }

        #endregion

        #region 交互

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 12 || height < 12) return;

            PointerPoint point = e.GetCurrentPoint(this);
            int delta = point.Properties.MouseWheelDelta;
            if (delta == 0) return;

            double factor = delta > 0 ? 0.88 : 1.0 / 0.88;
            double anchorX = _viewport.ToWorldX(point.Position.X, width);
            double anchorY = _viewport.ToWorldY(point.Position.Y, height);

            _viewport = _viewport.ZoomAt(factor, anchorX, anchorY);
            _pointerPosition = point.Position;
            _hasPointer = true;
            Redraw();
            RaiseStatus();
            e.Handled = true;
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed) return;

            _isPanning = true;
            _panOrigin = point.Position;
            _hasPointer = true;
            _pointerPosition = point.Position;
            CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 12 || height < 12) return;

            PointerPoint point = e.GetCurrentPoint(this);

            if (_isPanning)
            {
                double dx = -(point.Position.X - _panOrigin.X) / width * _viewport.Width;
                double dy = (point.Position.Y - _panOrigin.Y) / height * _viewport.Height;
                _viewport = _viewport.Pan(dx, dy);
                _panOrigin = point.Position;
                _hasPointer = true;
                _pointerPosition = point.Position;
                Redraw();
                RaiseStatus();
                return;
            }

            _hasPointer = true;
            _pointerPosition = point.Position;
            UpdateCursorLayer(width, height);
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isPanning) return;
            _isPanning = false;
            ReleasePointerCapture(e.Pointer);
        }

        #endregion
    }
}
