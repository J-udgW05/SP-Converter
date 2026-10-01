using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Wpf.Ui;
using SPConverter.ViewModels;
using SPConverter.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace SPConverter.Views
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private enum AppPage
        {
            Converter,
            Settings,
            Help
        }

        private const double MinimumVisibleEdge = 100;

        private readonly IServiceProvider _serviceProvider;
        private readonly SettingsViewModel _settingsViewModel;

        private string _notificationTargetFolder = string.Empty;
        private System.Windows.Threading.DispatcherTimer? _notificationTimer;
        private AppPage _currentPage = AppPage.Converter;
        private bool _closeAfterConversionStopped;

        public MainWindow(
            MainViewModel viewModel,
            IServiceProvider serviceProvider,
            ISnackbarService snackbarService,
            SettingsViewModel settingsViewModel)
        {
            DataContext = viewModel;
            _serviceProvider = serviceProvider;
            _settingsViewModel = settingsViewModel;
            InitializeComponent();
            RestoreWindowPlacement();

            ContentFrame.Navigated += OnContentFrameNavigated;

            ContentFrame.Navigate(_serviceProvider.GetRequiredService<MassConvertPage>());

            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            SPConverter.Services.NotificationService.OnShowNotification += ShowNotificationToast;
            _settingsViewModel.ThemeChanging += OnThemeChanging;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Prebuild pages after startup so the first navigation does not stall.
            var warmUp = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.ApplicationIdle)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            warmUp.Tick += (_, _) =>
            {
                warmUp.Stop();
                var size = new Size(Math.Max(ContentFrame.ActualWidth, MinWidth), Math.Max(ContentFrame.ActualHeight, 400));
                foreach (Page page in new Page[]
                         {
                             _serviceProvider.GetRequiredService<SettingsPage>(),
                             _serviceProvider.GetRequiredService<HelpPage>()
                         })
                {
                    page.Measure(size);
                    page.Arrange(new Rect(size));
                }
            };
            warmUp.Start();
        }

        private void OnThemeChanging(object? sender, EventArgs e)
        {
            Animations.BeginThemeCrossfade(RootGrid);
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            var conversion = _serviceProvider.GetRequiredService<MassConvertViewModel>();
            if (conversion.IsConverting && !_closeAfterConversionStopped)
            {
                // Stop the conversion cleanly first to avoid partially written files.
                e.Cancel = true;
                base.OnClosing(e);
                if (await ConfirmCloseDuringConversionAsync())
                {
                    await conversion.CancelAndWaitAsync();
                    _closeAfterConversionStopped = true;
                    Close();
                }
                return;
            }

            base.OnClosing(e);
            if (e.Cancel) return;

            Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
            {
                _settingsViewModel.SaveWindowPlacement(new SettingsViewModel.WindowPlacementData
                {
                    Left = bounds.Left,
                    Top = bounds.Top,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    IsMaximized = WindowState == WindowState.Maximized
                });
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            SPConverter.Services.NotificationService.OnShowNotification -= ShowNotificationToast;
            _settingsViewModel.ThemeChanging -= OnThemeChanging;
            base.OnClosed(e);
        }

        private static async System.Threading.Tasks.Task<bool> ConfirmCloseDuringConversionAsync()
        {
            var dialog = new Wpf.Ui.Controls.MessageBox
            {
                Title = ResourceText("Close_ConvertingTitle", "Conversion in progress"),
                Content = ResourceText("Close_ConvertingMessage", "Stop the conversion and close the app?"),
                PrimaryButtonText = ResourceText("Close_Stop", "Stop and close"),
                PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
                CloseButtonText = ResourceText("Close_Continue", "Continue")
            };

            return await dialog.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
        }

        private static string ResourceText(string key, string fallback)
        {
            return Application.Current?.Resources[key] as string ?? fallback;
        }

        private void RestoreWindowPlacement()
        {
            SettingsViewModel.WindowPlacementData? placement = _settingsViewModel.WindowPlacement;
            if (placement == null || !IsPlacementVisible(placement))
            {
                FitAndCenterOnStartup();
                return;
            }

            Rect workArea = SystemParameters.WorkArea;
            MinWidth = Math.Min(MinWidth, workArea.Width);
            MinHeight = Math.Min(MinHeight, workArea.Height);
            Width = Math.Clamp(placement.Width, MinWidth, Math.Max(MinWidth, SystemParameters.VirtualScreenWidth));
            Height = Math.Clamp(placement.Height, MinHeight, Math.Max(MinHeight, SystemParameters.VirtualScreenHeight));
            Left = placement.Left;
            Top = placement.Top;

            if (placement.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private static bool IsPlacementVisible(SettingsViewModel.WindowPlacementData placement)
        {
            if (!double.IsFinite(placement.Left) || !double.IsFinite(placement.Top)
                || !double.IsFinite(placement.Width) || !double.IsFinite(placement.Height)
                || placement.Width <= 0 || placement.Height <= 0)
            {
                return false;
            }

            var virtualScreen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            // The title bar must remain reachable after a display layout change.
            var titleBar = new Rect(placement.Left, placement.Top, placement.Width, 32);
            titleBar.Intersect(virtualScreen);
            return !titleBar.IsEmpty && titleBar.Width >= MinimumVisibleEdge && titleBar.Height >= 16;
        }

        private void FitAndCenterOnStartup()
        {
            Rect workArea = SystemParameters.WorkArea;

            double targetWidth = double.IsNaN(Width) ? MinWidth : Width;
            double targetHeight = double.IsNaN(Height) ? MinHeight : Height;

            MinWidth = Math.Min(MinWidth, workArea.Width);
            MinHeight = Math.Min(MinHeight, workArea.Height);
            Width = Math.Min(targetWidth, workArea.Width);
            Height = Math.Min(targetHeight, workArea.Height);

            Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
            Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
        }

        private void OnContentFrameNavigated(object sender, NavigationEventArgs e)
        {
            // Keeps Alt+Left and mouse Back from leaving the header buttons out of sync.
            while (ContentFrame.CanGoBack)
            {
                ContentFrame.RemoveBackEntry();
            }

            if (e.Content is UIElement page)
            {
                Animations.PlayEntrance(page);
            }
        }

        private void ShowNotificationToast(SPConverter.Services.NotificationRequest req)
        {
            Dispatcher.InvokeAsync(() =>
            {
                NotificationTitle.Text = req.Title;
                NotificationMessage.Text = req.Message;
                _notificationTargetFolder = req.TargetFolder;

                NotificationOpenFolderBtn.Visibility = !string.IsNullOrWhiteSpace(req.TargetFolder) && System.IO.Directory.Exists(req.TargetFolder)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                NotificationIcon.Symbol = req.IsSuccess
                    ? Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24
                    : Wpf.Ui.Controls.SymbolRegular.Warning24;

                NotificationToast.Visibility = Visibility.Visible;
                Animations.PlayEntrance(NotificationToast, offsetY: 16, durationMs: 250, useBitmapCache: true);

                _notificationTimer?.Stop();
                _notificationTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(7)
                };
                _notificationTimer.Tick += (s, e) => HideNotificationToast();
                _notificationTimer.Start();
            });
        }

        private void HideNotificationToast()
        {
            _notificationTimer?.Stop();
            if (NotificationToast.Visibility != Visibility.Visible) return;

            Animations.FadeOut(NotificationToast, () => NotificationToast.Visibility = Visibility.Collapsed);
        }

        private void OnNotificationOpenFolderClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_notificationTargetFolder) && System.IO.Directory.Exists(_notificationTargetFolder))
            {
                System.Diagnostics.Process.Start("explorer.exe", _notificationTargetFolder);
            }
            HideNotificationToast();
        }

        private void OnNotificationCloseClick(object sender, RoutedEventArgs e)
        {
            HideNotificationToast();
        }

        private void OnSettingsToggleClick(object sender, RoutedEventArgs e)
        {
            NavigateTo(_currentPage == AppPage.Settings ? AppPage.Converter : AppPage.Settings);
        }

        private void OnHelpToggleClick(object sender, RoutedEventArgs e)
        {
            NavigateTo(_currentPage == AppPage.Help ? AppPage.Converter : AppPage.Help);
        }

        private void NavigateTo(AppPage page)
        {
            if (page == _currentPage) return;

            Page target = page switch
            {
                AppPage.Settings => _serviceProvider.GetRequiredService<SettingsPage>(),
                AppPage.Help => _serviceProvider.GetRequiredService<HelpPage>(),
                _ => _serviceProvider.GetRequiredService<MassConvertPage>()
            };

            ContentFrame.Navigate(target);
            _currentPage = page;

            SettingsToggleButton.SetResourceReference(
                ContentControl.ContentProperty,
                page == AppPage.Settings ? "Nav_Convert" : "Nav_Settings");
            HelpToggleButton.SetResourceReference(
                ContentControl.ContentProperty,
                page == AppPage.Help ? "Nav_Convert" : "Nav_Help");
        }
    }
}
