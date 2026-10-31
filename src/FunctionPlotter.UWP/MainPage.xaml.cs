using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace FunctionPlotter.UWP
{
    /// <summary>
    /// 主页面：一个输入框 + 一块实时画布。
    /// 与 WinUI 3 版逻辑一一对应，只把 Window 换成了 Page、去掉 AppWindow 相关的窗口调整。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly PlotView _plot = new PlotView();

        public MainPage()
        {
            InitializeComponent();

            PlotHost.Children.Add(_plot);
            _plot.StatusChanged += OnPlotStatus;

            RootGrid.ActualThemeChanged += (_, __) => UpdateStatusColor();
            RootGrid.Loaded += RootGrid_Loaded;

            // 首次渲染前先放入一个示例，用户打开就能看到东西
            FunctionBox.Text = "sin(x)/x";
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
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
