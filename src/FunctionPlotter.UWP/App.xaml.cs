using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FunctionPlotter.UWP
{
    sealed partial class App : Application
    {
        private Frame _rootFrame;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // 桌面端启动时把窗口设成设计尺寸（UWP 的窗口初始大小）
            ApplicationView.PreferredLaunchViewSize = new Size(620, 520);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            _rootFrame = Window.Current.Content as Frame;

            if (_rootFrame == null)
            {
                _rootFrame = new Frame();
                _rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = _rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (_rootFrame.Content == null)
                {
                    _rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                Window.Current.Activate();
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
