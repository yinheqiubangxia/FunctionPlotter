using System;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Printing;

namespace FunctionPlotter.UWP
{
    /// <summary>
    /// UWP 打印服务：把当前函数图像送进系统打印对话框。
    /// 打印页与屏幕共用同一套 PlotView 绘制管线（CreatePrintClone），
    /// 相同表达式、相同视口、相同主题配色 —— 打印输出与屏幕显示一致。
    /// </summary>
    public sealed class PrintService
    {
        private readonly Func<string> _getExpressionText;
        private readonly Func<PlotView> _createPrintClone;

        private PrintDocument _printDocument;
        private IPrintDocumentSource _printDocumentSource;
        private UIElement _pageElement;

        /// <param name="getExpressionText">取当前表达式文本（打印页眉用）。</param>
        /// <param name="createPrintClone">创建与屏幕内容一致的离线 PlotView。</param>
        public PrintService(Func<string> getExpressionText, Func<PlotView> createPrintClone)
        {
            _getExpressionText = getExpressionText;
            _createPrintClone = createPrintClone;
        }

        /// <summary>注册打印契约（页面加载后调用）。</summary>
        public void Register()
        {
            PrintManager printManager = PrintManager.GetForCurrentView();
            printManager.PrintTaskRequested += OnPrintTaskRequested;
        }

        /// <summary>注销打印契约（页面卸载时调用）。</summary>
        public void Unregister()
        {
            PrintManager printManager = PrintManager.GetForCurrentView();
            printManager.PrintTaskRequested -= OnPrintTaskRequested;
            DetachDocument();
        }

        /// <summary>主动弹出系统打印对话框。</summary>
        public async System.Threading.Tasks.Task<bool> ShowPrintUIAsync()
        {
            return await PrintManager.ShowPrintUIAsync();
        }

        private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            PrintTask task = args.Request.CreatePrintTask("FunctionPlotter · 函数图像", sourceRequested =>
            {
                EnsureDocument();
                sourceRequested.SetSource(_printDocumentSource);
            });
            task.Options.Orientation = PrintOrientation.Landscape;
        }

        private void EnsureDocument()
        {
            if (_printDocument != null) return;
            _printDocument = new PrintDocument();
            _printDocumentSource = _printDocument.DocumentSource;
            _printDocument.Paginate += OnPaginate;
            _printDocument.GetPreviewPage += OnGetPreviewPage;
            _printDocument.AddPages += OnAddPages;
        }

        private void DetachDocument()
        {
            if (_printDocument == null) return;
            _printDocument.Paginate -= OnPaginate;
            _printDocument.GetPreviewPage -= OnGetPreviewPage;
            _printDocument.AddPages -= OnAddPages;
            _printDocument = null;
            _printDocumentSource = null;
            _pageElement = null;
        }

        private void OnPaginate(object sender, PaginateEventArgs e)
        {
            _pageElement = BuildPrintPage(e.PrintTaskOptions);
            _printDocument.SetPreviewPageCount(1, PreviewPageCountType.Final);
        }

        private void OnGetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            if (e.PageNumber == 1 && _pageElement != null)
            {
                _printDocument.SetPreviewPage(1, _pageElement);
            }
        }

        private void OnAddPages(object sender, AddPagesEventArgs e)
        {
            if (_pageElement != null)
            {
                _printDocument.AddPage(_pageElement);
            }
            _printDocument.AddPagesComplete();
        }

        /// <summary>
        /// 组装打印页：页眉（标题 + 表达式）+ 与屏幕一致的图像区。
        /// 尺寸取打印机可打印区域，保证预览与实际输出一致。
        /// </summary>
        private UIElement BuildPrintPage(PrintTaskOptions options)
        {
            PrintPageDescription desc = options.GetPageDescription(0);
            Rect imageable = desc.ImageableRect;
            double width = imageable.Width > 0 ? imageable.Width : 1056;
            double height = imageable.Height > 0 ? imageable.Height : 816;

            var root = new Grid
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(Windows.UI.Colors.White),
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            string expression = _getExpressionText?.Invoke() ?? string.Empty;
            var header = new TextBlock
            {
                Text = "f(x) = " + expression,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 20,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(Windows.UI.Colors.Black),
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            PlotView plot = _createPrintClone();
            plot.HideCursorLayerForPrint();
            var frame = new Border
            {
                BorderThickness = new Thickness(1),
                Child = plot,
            };
            Grid.SetRow(frame, 1);
            root.Children.Add(frame);

            return root;
        }
    }
}
