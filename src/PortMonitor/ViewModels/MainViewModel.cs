using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PortMonitor.Models;
using PortMonitor.Services;

namespace PortMonitor.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPortScanner _portScanner;
    private readonly DispatcherTimer _timer;
    private bool _isScanning;

    public ObservableCollection<PortEntry> PortEntries { get; } = [];
    public ICollectionView PortEntriesView { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KillProcessCommand))]
    private PortEntry? _selectedEntry;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _selectedProtocol = "Wszystkie";

    [ObservableProperty]
    private bool _isAutoRefresh;

    [ObservableProperty]
    private int _refreshIntervalSeconds = 5;

    [ObservableProperty]
    private int _portCount;

    [ObservableProperty]
    private string _lastRefreshTime = "\u2014";

    public string[] ProtocolOptions { get; } = ["Wszystkie", "TCP", "UDP"];
    public int[] IntervalOptions { get; } = [1, 2, 5, 10, 15, 30];

    public MainViewModel(IPortScanner portScanner)
    {
        _portScanner = portScanner;

        PortEntriesView = CollectionViewSource.GetDefaultView(PortEntries);
        PortEntriesView.Filter = FilterPorts;

        _timer = new DispatcherTimer();
        _timer.Tick += async (_, _) => await RefreshAsync();
        UpdateTimerInterval();
    }

    partial void OnFilterTextChanged(string value) => PortEntriesView.Refresh();
    partial void OnSelectedProtocolChanged(string value) => PortEntriesView.Refresh();

    partial void OnIsAutoRefreshChanged(bool value)
    {
        if (value)
        {
            UpdateTimerInterval();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    partial void OnRefreshIntervalSecondsChanged(int value) => UpdateTimerInterval();

    private void UpdateTimerInterval()
    {
        _timer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
        if (IsAutoRefresh)
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    private bool FilterPorts(object obj)
    {
        if (obj is not PortEntry entry) return false;

        if (SelectedProtocol != "Wszystkie" && entry.Protocol != SelectedProtocol)
            return false;

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            string filter = FilterText.Trim();
            bool matches = entry.Port.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || entry.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || entry.LocalAddress.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || entry.Pid.ToString().Contains(filter);
            if (!matches) return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_isScanning) return;
        _isScanning = true;

        try
        {
            var ports = await _portScanner.GetActivePortsAsync();

            PortEntries.Clear();
            foreach (var entry in ports)
                PortEntries.Add(entry);

            PortCount = PortEntries.Count;
            LastRefreshTime = DateTime.Now.ToString("HH:mm:ss");
        }
        finally
        {
            _isScanning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanKillProcess))]
    private void KillProcess()
    {
        if (SelectedEntry is null) return;

        var result = MessageBox.Show(
            $"Zakonczyc proces \"{SelectedEntry.ProcessName}\" (PID: {SelectedEntry.Pid})?",
            "Potwierdzenie",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var (success, error) = ProcessService.KillProcess(SelectedEntry.Pid);

        if (!success)
        {
            MessageBox.Show(error!, "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            _ = RefreshAsync();
        }
    }

    private bool CanKillProcess() => SelectedEntry is not null;

    [RelayCommand]
    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"porty_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Port;Protokol;Adres;PID;Proces;Status");

        foreach (PortEntry entry in PortEntriesView)
        {
            sb.AppendLine($"{entry.Port};{entry.Protocol};{entry.LocalAddress};{entry.Pid};{entry.ProcessName};{entry.Status}");
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        MessageBox.Show($"Wyeksportowano do:\n{dialog.FileName}", "Eksport CSV", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
