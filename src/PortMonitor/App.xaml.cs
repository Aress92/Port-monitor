using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using PortMonitor.Services;
using PortMonitor.ViewModels;

namespace PortMonitor;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<IPortScanner, PortScannerService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Monitor Portow"
        };

        // Load app icon from embedded resource
        var iconUri = new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute);
        using var stream = GetResourceStream(iconUri)?.Stream;
        if (stream is not null)
            _trayIcon.Icon = new System.Drawing.Icon(stream);
        else
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        var contextMenu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = "Pokaz" };
        showItem.Click += (_, _) => ShowMainWindow();
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Zakoncz" };
        exitItem.Click += (_, _) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenu = contextMenu;
    }

    private void ShowMainWindow()
    {
        if (MainWindow is { } window)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
