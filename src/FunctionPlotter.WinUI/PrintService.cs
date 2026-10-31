using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Printing;
using Windows.Foundation;
using Windows.Graphics.Printing;

namespace FunctionPlotter.WinUI
{
    /// <summary>
    /// WinUI 3 打印服务。
    /// WinUI 3 没有托管的 PrintManager.GetForCurrentView()（那是 UWP 的 API），
    /// 桌面应用需要通过 IPrintManagerInterop 按 HWND 获取 PrintManager
    /// （接口 GUID 来自 Windows SDK 的 printmanagerinterop.h）。
    /// 打印页与屏幕共用同一套 PlotView 绘制管线（CreatePrintClone），所见即所得。
    /// </summary>
    public sealed class PrintService
    {
        // printmanagerinterop.h:
        // MIDL_INTERFACE("c5435a42-8d43-4e7b-a68a-ef311e392087") IPrintManagerInterop
        // 返回一律用 IntPtr（IInspectable 裸指针），再由 CsWinRT 的 FromAbi 包装，
        // 避免经典 COM 封送器直接返回 WinRT 投影类型导致 RCW 不兼容。
        [ComImport]
        [Guid("c5435a42-8d43-4e7b-a68a-ef311e392087")]
        [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
        private interface IPrintManagerInterop
        {
            [PreserveSig]
            int GetForWindow(IntPtr appWindow, ref Guid riid, out IntPtr printManager);

            [PreserveSig]
            int ShowPrintUIForWindowAsync(IntPtr appWindow, ref Guid riid, out IntPtr asyncOperation);
        }

        [DllImport("api-ms-win-core-winrt-l1-1-0.dll", PreserveSig = true)]
        private static extern int RoGetActivationFactory(
            [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            ref Guid iid,
            out IntPtr factory);

        private readonly IntPtr _hwnd;
        private readonly Func<string> _getExpressionText;
        private readonly Func<PlotView> _createPrintClone;

        private PrintManager _printManager;
        private PrintDocument _printDocument;
        private IPrintDocumentSource _printDocumentSource;
        private UIElement _pageElement;

        public PrintService(IntPtr hwnd, Func<string> getExpressionText, Func<PlotView> createPrintClone)
        {
            _hwnd = hwnd;
            _getExpressionText = getExpressionText;
            _createPrintClone = createPrintClone;
        }

        private static IPrintManagerInterop GetInterop()
        {
            Guid iid = typeof(IPrintManagerInterop).GUID;
            IntPtr factoryPtr;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory("Windows.Graphics.Printing.PrintManager", ref iid, out factoryPtr));
            try
            {
                return (IPrintManagerInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            }
            finally
            {
                Marshal.Release(factoryPtr);
            }
        }

        /// <summary>注册打印契约（窗口加载后调用），失败不阻塞应用。</summary>
        public bool Register()
        {
            try
            {
                if (!PrintManager.IsSupported()) return false;
                IPrintManagerInterop interop = GetInterop();
                Guid riid = typeof(PrintManager).GUID;
                IntPtr pmPtr;
                Marshal.ThrowExceptionForHR(interop.GetForWindow(_hwnd, ref riid, out pmPtr));
                _printManager = WinRT.MarshalInspectable<PrintManager>.FromAbi(pmPtr);
                Marshal.Release(pmPtr);
                _printManager.PrintTaskRequested += OnPrintTaskRequested;
                return true;
            }
            catch
            {
                _printManager = null;
                return false;
            }
        }

        /// <summary>注销打印契约（窗口关闭时调用）。</summary>
        public void Unregister()
        {
            if (_printManager != null)
            {
                _printManager.PrintTaskRequested -= OnPrintTaskRequested;
                _printManager = null;
            }
            _printDocument = null;
            _printDocumentSource = null;
            _pageElement = null;
        }

        /// <summary>弹出系统打印对话框（含预览）。</summary>
        public IAsyncOperation<bool> ShowPrintUIAsync()
        {
            IPrintManagerInterop interop = GetInterop();
            Guid riid = typeof(IAsyncOperation<bool>).GUID;
            IntPtr opPtr;
            Marshal.ThrowExceptionForHR(interop.ShowPrintUIForWindowAsync(_hwnd, ref riid, out opPtr));
            try
            {
                return WinRT.MarshalInspectable<IAsyncOperation<bool>>.FromAbi(opPtr);
            }
            finally
            {
                Marshal.Release(opPtr);
            }
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
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            string expression = _getExpressionText?.Invoke() ?? string.Empty;
            var header = new TextBlock
            {
                Text = "f(x) = " + expression,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 20,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black),
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