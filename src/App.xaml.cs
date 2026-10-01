using System.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SPConverter.Contracts;
using SPConverter.Services;
using SPConverter.ViewModels;

namespace SPConverter;

public partial class App : Application
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton<IFileManagementService, LocalFileService>();
            services.AddSingleton<IImageConverterService, MagickImageConverter>();
            services.AddSingleton<Wpf.Ui.ISnackbarService, Wpf.Ui.SnackbarService>();

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddTransient<SingleConvertViewModel>();
            services.AddSingleton<MassConvertViewModel>();

            services.AddTransient<SPConverter.Views.MainWindow>();
            services.AddTransient<SPConverter.Views.Pages.SingleConvertPage>();
            // Pages are reused: recreating them on every navigation costs several frames.
            services.AddSingleton<SPConverter.Views.Pages.MassConvertPage>();
            services.AddSingleton<SPConverter.Views.Pages.SettingsPage>();
            services.AddSingleton<SPConverter.Views.Pages.HelpPage>();
        }).Build();

    static App()
    {
        // Must run before App.xaml creates any storyboard.
        SPConverter.Views.Animations.ConfigureFrameRate();
    }

    public static T? GetService<T>() where T : class
    {
        return _host.Services.GetService(typeof(T)) as T;
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _host.Start();

        EventManager.RegisterClassHandler(
            typeof(System.Windows.Controls.ContextMenu),
            System.Windows.Controls.ContextMenu.OpenedEvent,
            new RoutedEventHandler((menu, _) => SPConverter.Views.Animations.PlayEntrance((UIElement)menu, offsetY: -8, durationMs: 200)));

        var settingsVm = GetService<SettingsViewModel>();
        var mainWindow = GetService<SPConverter.Views.MainWindow>();
        
        if (mainWindow != null)
        {
            Application.Current.MainWindow = mainWindow;
            
            // The backdrop must be applied before the window is shown (WindowChrome Freezable exception).
            settingsVm?.ApplyWindowBackground();
            
            mainWindow.Show();
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _host.StopAsync().Wait();
        _host.Dispose();
    }
}
