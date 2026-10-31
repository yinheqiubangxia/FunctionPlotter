using System;
using System.Threading.Tasks;
using FunctionPlotter.Core.Ai;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FunctionPlotter.WinUI
{
    /// <summary>
    /// 主窗口：表达式输入 + AI 自然语言绘图 + 实时画布 + 打印。
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly PlotView _plot = new PlotView();
        private PrintService _printService;
        private bool _aiBusy;

        public MainWindow()
        {
            InitializeComponent();

            Title = "函数绘图器";
            PlotHost.Children.Add(_plot);
            _plot.StatusChanged += OnPlotStatus;

            RootGrid.ActualThemeChanged += (_, __) => UpdateStatusColor();
            RootGrid.Loaded += RootGrid_Loaded;
            Closed += MainWindow_Closed;

            PopulateModelCombo();

            _printService = new PrintService(
                WinRT.Interop.WindowNative.GetWindowHandle(this),
                () => FunctionBox.Text,
                () => _plot.CreatePrintClone());
            if (!_printService.Register()) _printService = null;

            // 首次渲染前先放入一个示例，用户打开就能看到东西
            FunctionBox.Text = "sin(x)/x";
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            // 按当前缩放比例把小窗口摆好（AppWindow 用的是物理像素）
            double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
            AppWindow.Resize(new SizeInt32((int)Math.Round(620 * scale), (int)Math.Round(560 * scale)));

            FunctionBox.Focus(FocusState.Programmatic);
            FunctionBox.SelectAll();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _printService?.Unregister();
        }

        private void FunctionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 逐字符实时重绘：解析 + 采样 + 绘制都在毫秒级
            _plot.SetExpression(FunctionBox.Text);
        }

        private void OnPlotStatus(string message)
        {
            if (!_aiBusy) StatusText.Text = message;
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

        #region 打印

        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_printService == null)
            {
                StatusText.Text = "当前环境不支持打印";
                return;
            }
            try
            {
                await _printService.ShowPrintUIAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = "打印失败：" + ex.Message;
            }
        }

        #endregion

        #region AI 自然语言绘图

        private void PopulateModelCombo()
        {
            ModelCombo.Items.Clear();
            foreach (LLMProvider provider in LLMProvider.BuiltIn)
            {
                var item = new ComboBoxItem
                {
                    Content = provider.DisplayName,
                    Tag = provider.Id,
                };
                ModelCombo.Items.Add(item);
                if (provider.Id == AiSettings.ProviderId) ModelCombo.SelectedItem = item;
            }
            if (ModelCombo.SelectedItem == null && ModelCombo.Items.Count > 0)
            {
                ModelCombo.SelectedIndex = 0;
            }
        }

        private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
            {
                AiSettings.ProviderId = id;
                AiSettings.Save();
            }
        }

        private void AiBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                _ = RunAiPlotAsync();
            }
        }

        private void AiSend_Click(object sender, RoutedEventArgs e)
        {
            _ = RunAiPlotAsync();
        }

        /// <summary>
        /// 自然语言 → 表达式 → 绘图：调用当前选中的大模型，
        /// 成功后把表达式填回输入框（复用实时重绘链路），并按模型给出的范围调整视口。
        /// </summary>
        private async Task RunAiPlotAsync()
        {
            if (_aiBusy) return;
            string userText = AiBox.Text?.Trim();
            if (string.IsNullOrEmpty(userText))
            {
                StatusText.Text = "先描述一下想画的函数，例如：绘制 y=sin(x) 在 -2π 到 2π 的图像";
                return;
            }

            LLMProvider provider = AiSettings.CurrentProvider;
            string apiKey = AiSettings.GetApiKey(provider.Id);
            if (string.IsNullOrEmpty(apiKey))
            {
                StatusText.Text = "请先在右侧 ⚙ 设置里填写 " + provider.DisplayName + " 的 API Key";
                await ShowAiSettingsDialogAsync();
                return;
            }

            _aiBusy = true;
            AiSendButton.IsEnabled = false;
            StatusText.Text = "正在请求 " + provider.DisplayName + "（" + AiSettings.EffectiveModel + "）…";

            try
            {
                var service = new AiPlotService(AiSettings.EffectiveBaseUrl, apiKey, AiSettings.EffectiveModel);
                AiPlotResult result = await service.DescribeToExpressionAsync(userText);

                FunctionBox.Text = result.Expression; // 触发 TextChanged → 实时重绘
                if (result.XMin.HasValue && result.XMax.HasValue)
                {
                    _plot.SetXRange(result.XMin.Value, result.XMax.Value);
                }
                else
                {
                    _plot.ResetView();
                }

                StatusText.Text = string.IsNullOrEmpty(result.Note)
                    ? "AI 已绘制：f(x) = " + result.Expression
                    : "AI 已绘制：" + result.Note;
            }
            catch (Exception ex)
            {
                StatusText.Text = "AI 调用失败：" + ex.Message;
            }
            finally
            {
                _aiBusy = false;
                AiSendButton.IsEnabled = true;
                UpdateStatusColor();
            }
        }

        /// <summary>AI 设置对话框：API Key（按厂商分别保存）、模型名、自定义接口地址。</summary>
        private async void AiSettings_Click(object sender, RoutedEventArgs e)
        {
            await ShowAiSettingsDialogAsync();
        }

        private async Task ShowAiSettingsDialogAsync()
        {
            LLMProvider provider = AiSettings.CurrentProvider;

            var panel = new StackPanel { Spacing = 10, MinWidth = 380 };

            panel.Children.Add(new TextBlock
            {
                Text = "当前模型：" + provider.DisplayName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });

            var keyBox = new PasswordBox
            {
                Password = AiSettings.GetApiKey(provider.Id),
                PlaceholderText = "粘贴该厂商的 API Key（保存到本机配置文件）",
            };
            panel.Children.Add(new TextBlock { Text = "API Key", FontSize = 12 });
            panel.Children.Add(keyBox);

            panel.Children.Add(new TextBlock
            {
                Text = "模型名（留空则用默认 " + (string.IsNullOrEmpty(provider.DefaultModel) ? "—" : provider.DefaultModel) + "）",
                FontSize = 12,
            });
            var modelBox = new TextBox
            {
                Text = AiSettings.ModelName ?? string.Empty,
                PlaceholderText = provider.DefaultModel,
                FontFamily = new FontFamily("Consolas"),
            };
            panel.Children.Add(modelBox);

            TextBox baseUrlBox = null;
            if (provider.Id == "custom")
            {
                panel.Children.Add(new TextBlock { Text = "接口地址（OpenAI 兼容，如 http://localhost:11434/v1）", FontSize = 12 });
                baseUrlBox = new TextBox
                {
                    Text = AiSettings.CustomBaseUrl ?? string.Empty,
                    PlaceholderText = "https://your-endpoint/v1",
                    FontFamily = new FontFamily("Consolas"),
                };
                panel.Children.Add(baseUrlBox);
            }

            var dialog = new ContentDialog
            {
                Title = "AI 模型设置",
                Content = panel,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                AiSettings.SetApiKey(provider.Id, keyBox.Password.Trim());
                AiSettings.ModelName = string.IsNullOrWhiteSpace(modelBox.Text) ? null : modelBox.Text.Trim();
                if (baseUrlBox != null) AiSettings.CustomBaseUrl = baseUrlBox.Text.Trim();
                AiSettings.Save();
                StatusText.Text = "AI 设置已保存";
            }
        }

        #endregion
    }
}
