using System.Windows;
using PortMonitor.ViewModels;

namespace PortMonitor;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        try
        {
            Icon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute));
        }
        catch { /* fallback: no icon */ }

        Loaded += async (_, _) => await viewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
