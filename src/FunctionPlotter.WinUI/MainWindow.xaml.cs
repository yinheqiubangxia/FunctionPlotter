using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FunctionPlotter.WinUI
{
    /// <summary>
    /// 主窗口：一个输入框 + 一块实时画布，窗口本身保持小尺寸。
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly PlotView _plot = new PlotView();

        public MainWindow()
        {
            InitializeComponent();

            Title = "函数绘图器";
            PlotHost.Children.Add(_plot);
            _plot.StatusChanged += OnPlotStatus;

            RootGrid.ActualThemeChanged += (_, __) => UpdateStatusColor();
            RootGrid.Loaded += RootGrid_Loaded;

            // 首次渲染前先放入一个示例，用户打开就能看到东西
            FunctionBox.Text = "sin(x)/x";
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            // 按当前缩放比例把小窗口摆好（AppWindow 用的是物理像素）
            double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
            AppWindow.Resize(new SizeInt32((int)Math.Round(620 * scale), (int)Math.Round(520 * scale)));

            FunctionBox.Focus(FocusState.Programmatic);
            FunctionBox.SelectAll();
        }

        private void FunctionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 逐字符实时重绘：解析 + 采样 + 绘制都在毫秒级
            _plot.SetExpression(FunctionBox.Text);
        }

        private void OnPlotStatus(string message)
        {
            StatusText.Text = message;
            UpdateStatusColor();
        }

        private void UpdateStatusColor()
        {
            Palette palette = Palette.For(RootGrid.ActualTheme);
            bool hasError = !string.IsNullOrEmpty(_plot.ErrorMessage);
            StatusText.Foreground = new SolidColorBrush(hasError ? palette.Error : palette.SecondaryText);
        }

        private void Fit_Click(object sender, RoutedEventArgs e) => _plot.FitView();

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => _plot.ZoomBy(0.8);

        private void ZoomOut_Click(object sender, RoutedEventArgs e) => _plot.ZoomBy(1.25);

        private void Reset_Click(object sender, RoutedEventArgs e) => _plot.ResetView();

        private void Example_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && !string.IsNullOrEmpty(item.Text))
            {
                FunctionBox.Text = item.Text;
                FunctionBox.Focus(FocusState.Programmatic);
                FunctionBox.SelectAll();
                _plot.FitView();
            }
        }

        private void Fit_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _plot.FitView();
            args.Handled = true;
        }

        private void ZoomIn_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _plot.ZoomBy(0.8);
            args.Handled = true;
        }

        private void ZoomOut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _plot.ZoomBy(1.25);
            args.Handled = true;
        }

        private void Reset_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _plot.ResetView();
            args.Handled = true;
        }
    }
}
