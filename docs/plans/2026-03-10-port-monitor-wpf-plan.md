# Port Monitor WPF — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Rebuild Port Monitor from Python/Tkinter to C#/WPF with extended features (auto-refresh, filtering, CSV export, system tray).

**Architecture:** WPF (.NET 10) + CommunityToolkit.Mvvm (MVVM pattern) + P/Invoke for port scanning via iphlpapi.dll. DI via Microsoft.Extensions.DependencyInjection. System tray via Hardcodet.NotifyIcon.Wpf.

**Tech Stack:** C# 13, .NET 10, WPF, XAML, CommunityToolkit.Mvvm, iphlpapi.dll P/Invoke

**Design doc:** `docs/plans/2026-03-10-port-monitor-wpf-design.md`

---

## Task 1: Scaffold WPF Project

**Files:**
- Create: `src/PortMonitor/PortMonitor.csproj`
- Create: `src/PortMonitor/App.xaml`
- Create: `src/PortMonitor/App.xaml.cs`
- Create: `src/PortMonitor/MainWindow.xaml`
- Create: `src/PortMonitor/MainWindow.xaml.cs`
- Create: `PortMonitor.sln`

**Step 1: Create WPF project via dotnet CLI**

```bash
cd I:/Port-monitor
mkdir -p src/PortMonitor
cd src/PortMonitor
dotnet new wpf --name PortMonitor --framework net10.0
```

This creates the basic WPF scaffold with App.xaml, MainWindow.xaml, and .csproj.

**Step 2: Create solution and add project**

```bash
cd I:/Port-monitor
dotnet new sln --name PortMonitor
dotnet sln add src/PortMonitor/PortMonitor.csproj
```

**Step 3: Add NuGet packages**

```bash
cd I:/Port-monitor/src/PortMonitor
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Hardcodet.NotifyIcon.Wpf
```

**Step 4: Verify it builds and runs**

```bash
cd I:/Port-monitor
dotnet build
```

Expected: Build succeeded, 0 errors.

**Step 5: Commit**

```bash
git add PortMonitor.sln src/
git commit -m "feat: scaffold WPF project with NuGet dependencies"
```

---

## Task 2: Model — PortEntry

**Files:**
- Create: `src/PortMonitor/Models/PortEntry.cs`

**Step 1: Create the PortEntry model**

```csharp
namespace PortMonitor.Models;

public class PortEntry
{
    public int Port { get; init; }
    public string Protocol { get; init; } = string.Empty;  // "TCP" or "UDP"
    public int Pid { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string LocalAddress { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;  // "LISTEN", "ESTABLISHED", etc.
}
```

**Step 2: Verify build**

```bash
dotnet build
```

**Step 3: Commit**

```bash
git add src/PortMonitor/Models/
git commit -m "feat: add PortEntry model"
```

---

## Task 3: P/Invoke — NativeMethods

**Files:**
- Create: `src/PortMonitor/Helpers/NativeMethods.cs`

**Step 1: Create P/Invoke declarations for iphlpapi.dll**

```csharp
using System.Runtime.InteropServices;

namespace PortMonitor.Helpers;

internal static class NativeMethods
{
    private const string IphlpapiDll = "iphlpapi.dll";

    public const int AF_INET = 2;

    // TCP table class: owner PID
    public const int TCP_TABLE_OWNER_PID_ALL = 5;
    // UDP table class: owner PID
    public const int UDP_TABLE_OWNER_PID = 1;

    [DllImport(IphlpapiDll, SetLastError = true)]
    public static extern uint GetExtendedTcpTable(
        nint pTcpTable, ref int pdwSize, bool bOrder,
        int ulAf, int tableClass, uint reserved = 0);

    [DllImport(IphlpapiDll, SetLastError = true)]
    public static extern uint GetExtendedUdpTable(
        nint pUdpTable, ref int pdwSize, bool bOrder,
        int ulAf, int tableClass, uint reserved = 0);

    // TCP states
    public enum TcpState
    {
        Closed = 1,
        Listen = 2,
        SynSent = 3,
        SynReceived = 4,
        Established = 5,
        FinWait1 = 6,
        FinWait2 = 7,
        CloseWait = 8,
        Closing = 9,
        LastAck = 10,
        TimeWait = 11,
        DeleteTcb = 12
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_TCPROW_OWNER_PID
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
        public uint dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_TCPTABLE_OWNER_PID
    {
        public uint dwNumEntries;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_UDPROW_OWNER_PID
    {
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_UDPTABLE_OWNER_PID
    {
        public uint dwNumEntries;
    }
}
```

**Step 2: Verify build**

```bash
dotnet build
```

**Step 3: Commit**

```bash
git add src/PortMonitor/Helpers/
git commit -m "feat: add P/Invoke declarations for iphlpapi.dll"
```

---

## Task 4: Service — IPortScanner + PortScannerService

**Files:**
- Create: `src/PortMonitor/Services/IPortScanner.cs`
- Create: `src/PortMonitor/Services/PortScannerService.cs`

**Step 1: Create the IPortScanner interface**

```csharp
using PortMonitor.Models;

namespace PortMonitor.Services;

public interface IPortScanner
{
    Task<List<PortEntry>> GetActivePortsAsync();
}
```

**Step 2: Create PortScannerService implementation**

```csharp
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using PortMonitor.Helpers;
using PortMonitor.Models;

namespace PortMonitor.Services;

public class PortScannerService : IPortScanner
{
    public Task<List<PortEntry>> GetActivePortsAsync()
    {
        return Task.Run(() =>
        {
            var entries = new List<PortEntry>();
            entries.AddRange(GetTcpPorts());
            entries.AddRange(GetUdpPorts());
            return entries.OrderBy(e => e.Port).ToList();
        });
    }

    private static List<PortEntry> GetTcpPorts()
    {
        var entries = new List<PortEntry>();
        int size = 0;

        // First call: get required buffer size
        NativeMethods.GetExtendedTcpTable(nint.Zero, ref size, true,
            NativeMethods.AF_INET, NativeMethods.TCP_TABLE_OWNER_PID_ALL);

        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            uint result = NativeMethods.GetExtendedTcpTable(buffer, ref size, true,
                NativeMethods.AF_INET, NativeMethods.TCP_TABLE_OWNER_PID_ALL);

            if (result != 0) return entries;

            int rowCount = Marshal.ReadInt32(buffer);
            nint rowPtr = buffer + Marshal.SizeOf<NativeMethods.MIB_TCPTABLE_OWNER_PID>();
            int rowSize = Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>();

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(rowPtr);
                var state = (NativeMethods.TcpState)row.dwState;
                int localPort = (ushort)IPAddress.NetworkToHostOrder((short)row.dwLocalPort);
                string localAddr = new IPAddress(row.dwLocalAddr).ToString();
                string processName = GetProcessName((int)row.dwOwningPid);

                entries.Add(new PortEntry
                {
                    Port = localPort,
                    Protocol = "TCP",
                    Pid = (int)row.dwOwningPid,
                    ProcessName = processName,
                    LocalAddress = localAddr,
                    Status = state.ToString()
                });

                rowPtr += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return entries;
    }

    private static List<PortEntry> GetUdpPorts()
    {
        var entries = new List<PortEntry>();
        int size = 0;

        NativeMethods.GetExtendedUdpTable(nint.Zero, ref size, true,
            NativeMethods.AF_INET, NativeMethods.UDP_TABLE_OWNER_PID);

        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            uint result = NativeMethods.GetExtendedUdpTable(buffer, ref size, true,
                NativeMethods.AF_INET, NativeMethods.UDP_TABLE_OWNER_PID);

            if (result != 0) return entries;

            int rowCount = Marshal.ReadInt32(buffer);
            nint rowPtr = buffer + Marshal.SizeOf<NativeMethods.MIB_UDPTABLE_OWNER_PID>();
            int rowSize = Marshal.SizeOf<NativeMethods.MIB_UDPROW_OWNER_PID>();

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MIB_UDPROW_OWNER_PID>(rowPtr);
                int localPort = (ushort)IPAddress.NetworkToHostOrder((short)row.dwLocalPort);
                string localAddr = new IPAddress(row.dwLocalAddr).ToString();
                string processName = GetProcessName((int)row.dwOwningPid);

                entries.Add(new PortEntry
                {
                    Port = localPort,
                    Protocol = "UDP",
                    Pid = (int)row.dwOwningPid,
                    ProcessName = processName,
                    LocalAddress = localAddr,
                    Status = "—"
                });

                rowPtr += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return entries;
    }

    private static string GetProcessName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return "<nieznany>";
        }
    }
}
```

**Step 3: Verify build**

```bash
dotnet build
```

**Step 4: Commit**

```bash
git add src/PortMonitor/Services/
git commit -m "feat: add port scanner service with P/Invoke"
```

---

## Task 5: Service — ProcessService

**Files:**
- Create: `src/PortMonitor/Services/ProcessService.cs`

**Step 1: Create ProcessService**

```csharp
using System.ComponentModel;
using System.Diagnostics;

namespace PortMonitor.Services;

public class ProcessService
{
    /// <summary>
    /// Kills a process by PID. Returns (success, errorMessage).
    /// </summary>
    public static (bool Success, string? Error) KillProcess(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            string name = process.ProcessName;
            process.Kill();
            return (true, null);
        }
        catch (ArgumentException)
        {
            return (false, "Proces juz nie istnieje.");
        }
        catch (Win32Exception)
        {
            return (false, "Brak uprawnien. Uruchom aplikacje jako Administrator.");
        }
        catch (InvalidOperationException)
        {
            return (false, "Proces juz nie istnieje.");
        }
    }
}
```

**Step 2: Verify build**

```bash
dotnet build
```

**Step 3: Commit**

```bash
git add src/PortMonitor/Services/ProcessService.cs
git commit -m "feat: add process kill service"
```

---

## Task 6: ViewModel — MainViewModel

**Files:**
- Create: `src/PortMonitor/ViewModels/MainViewModel.cs`

**Step 1: Create MainViewModel with all commands and properties**

```csharp
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
    private string _lastRefreshTime = "—";

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

        // Export filtered view
        foreach (PortEntry entry in PortEntriesView)
        {
            sb.AppendLine($"{entry.Port};{entry.Protocol};{entry.LocalAddress};{entry.Pid};{entry.ProcessName};{entry.Status}");
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        MessageBox.Show($"Wyeksportowano do:\n{dialog.FileName}", "Eksport CSV", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
```

**Step 2: Verify build**

```bash
dotnet build
```

**Step 3: Commit**

```bash
git add src/PortMonitor/ViewModels/
git commit -m "feat: add MainViewModel with commands, filtering, auto-refresh"
```

---

## Task 7: Converter — StatusToBrushConverter

**Files:**
- Create: `src/PortMonitor/Converters/StatusToBrushConverter.cs`

**Step 1: Create the converter**

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PortMonitor.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Listen" => new SolidColorBrush(Color.FromRgb(220, 255, 220)),      // light green
            "Established" => new SolidColorBrush(Color.FromRgb(220, 235, 255)), // light blue
            "TimeWait" => new SolidColorBrush(Color.FromRgb(255, 255, 220)),    // light yellow
            "CloseWait" => new SolidColorBrush(Color.FromRgb(255, 230, 220)),   // light orange
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

**Step 2: Verify build, commit**

```bash
dotnet build
git add src/PortMonitor/Converters/
git commit -m "feat: add StatusToBrushConverter"
```

---

## Task 8: XAML — MainWindow UI

**Files:**
- Modify: `src/PortMonitor/MainWindow.xaml`
- Modify: `src/PortMonitor/MainWindow.xaml.cs`

**Step 1: Replace MainWindow.xaml with full UI**

```xml
<Window x:Class="PortMonitor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:converters="clr-namespace:PortMonitor.Converters"
        Title="Monitor Portow" Height="600" Width="900"
        MinHeight="400" MinWidth="700"
        WindowStartupLocation="CenterScreen"
        Closing="Window_Closing">
    <Window.Resources>
        <converters:StatusToBrushConverter x:Key="StatusToBrush"/>
    </Window.Resources>

    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Row 0: Filter bar -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Filtruj:" VerticalAlignment="Center" Margin="0,0,6,0"/>
            <TextBox Text="{Binding FilterText, UpdateSourceTrigger=PropertyChanged}"
                     Width="250" VerticalContentAlignment="Center" Padding="4,2"/>
            <TextBlock Text="Protokol:" VerticalAlignment="Center" Margin="16,0,6,0"/>
            <ComboBox ItemsSource="{Binding ProtocolOptions}"
                      SelectedItem="{Binding SelectedProtocol}"
                      Width="110" VerticalContentAlignment="Center"/>
        </StackPanel>

        <!-- Row 1: Auto-refresh -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,8">
            <CheckBox Content="Auto-odswiezanie co"
                      IsChecked="{Binding IsAutoRefresh}"
                      VerticalAlignment="Center"/>
            <ComboBox ItemsSource="{Binding IntervalOptions}"
                      SelectedItem="{Binding RefreshIntervalSeconds}"
                      Width="60" Margin="6,0,4,0" VerticalContentAlignment="Center"/>
            <TextBlock Text="s" VerticalAlignment="Center"/>
        </StackPanel>

        <!-- Row 2: DataGrid -->
        <DataGrid Grid.Row="2"
                  ItemsSource="{Binding PortEntriesView}"
                  SelectedItem="{Binding SelectedEntry}"
                  AutoGenerateColumns="False"
                  IsReadOnly="True"
                  SelectionMode="Single"
                  CanUserSortColumns="True"
                  GridLinesVisibility="Horizontal"
                  AlternatingRowBackground="#F8F8F8"
                  RowBackground="White"
                  HeadersVisibility="Column">
            <DataGrid.RowStyle>
                <Style TargetType="DataGridRow">
                    <Setter Property="Background"
                            Value="{Binding Status, Converter={StaticResource StatusToBrush}}"/>
                </Style>
            </DataGrid.RowStyle>
            <DataGrid.Columns>
                <DataGridTextColumn Header="Port" Binding="{Binding Port}" Width="80"/>
                <DataGridTextColumn Header="Protokol" Binding="{Binding Protocol}" Width="80"/>
                <DataGridTextColumn Header="Adres" Binding="{Binding LocalAddress}" Width="130"/>
                <DataGridTextColumn Header="PID" Binding="{Binding Pid}" Width="70"/>
                <DataGridTextColumn Header="Proces" Binding="{Binding ProcessName}" Width="*"/>
                <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="110"/>
            </DataGrid.Columns>
        </DataGrid>

        <!-- Row 3: Buttons -->
        <StackPanel Grid.Row="3" Orientation="Horizontal" Margin="0,8,0,0">
            <Button Content="Odswiez" Command="{Binding RefreshCommand}"
                    Padding="16,6" Margin="0,0,8,0"/>
            <Button Content="Zakoncz proces" Command="{Binding KillProcessCommand}"
                    Padding="16,6" Margin="0,0,8,0"/>
            <Button Content="Eksport CSV" Command="{Binding ExportCsvCommand}"
                    Padding="16,6"/>
        </StackPanel>

        <!-- Row 4: Status bar -->
        <StatusBar Grid.Row="4" Margin="0,8,0,0">
            <StatusBarItem>
                <TextBlock>
                    <Run Text="Portow: "/><Run Text="{Binding PortCount, Mode=OneWay}"/>
                    <Run Text="  |  Ostatnie odswiezenie: "/><Run Text="{Binding LastRefreshTime, Mode=OneWay}"/>
                </TextBlock>
            </StatusBarItem>
        </StatusBar>
    </Grid>
</Window>
```

**Step 2: Replace MainWindow.xaml.cs with code-behind for tray support**

```csharp
using System.Windows;
using PortMonitor.ViewModels;

namespace PortMonitor;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
    }
}
```

**Step 3: Verify build**

```bash
dotnet build
```

**Step 4: Commit**

```bash
git add src/PortMonitor/MainWindow.xaml src/PortMonitor/MainWindow.xaml.cs
git commit -m "feat: add MainWindow XAML UI with DataGrid, filtering, status bar"
```

---

## Task 9: App.xaml — DI Setup + Tray Icon

**Files:**
- Modify: `src/PortMonitor/App.xaml`
- Modify: `src/PortMonitor/App.xaml.cs`

**Step 1: Update App.xaml — remove StartupUri, add tray icon resource**

```xml
<Application x:Class="PortMonitor.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:tb="http://www.hardcodet.net/taskbar"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <tb:TaskbarIcon x:Key="TrayIcon"
                        ToolTipText="Monitor Portow"
                        DoubleClickCommand="{x:Static local:App.ShowWindowCommand}"
                        xmlns:local="clr-namespace:PortMonitor">
            <tb:TaskbarIcon.ContextMenu>
                <ContextMenu>
                    <MenuItem Header="Pokaz" Click="ShowWindow_Click"/>
                    <Separator/>
                    <MenuItem Header="Zakoncz" Click="ExitApp_Click"/>
                </ContextMenu>
            </tb:TaskbarIcon.ContextMenu>
        </tb:TaskbarIcon>
    </Application.Resources>
</Application>
```

**Step 2: Update App.xaml.cs with DI container and tray logic**

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using PortMonitor.Services;
using PortMonitor.ViewModels;

namespace PortMonitor;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private ServiceProvider? _serviceProvider;
    private MainWindow? _mainWindow;

    public static ICommand ShowWindowCommand { get; } = new RelayCommand(ShowMainWindow);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<IPortScanner, PortScannerService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _serviceProvider = services.BuildServiceProvider();

        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        _mainWindow.Show();

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.Icon = SystemIcons.Application;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ShowMainWindow()
    {
        if (Current.MainWindow is { } window)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private void ShowWindow_Click(object sender, RoutedEventArgs e) => ShowMainWindow();

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        _trayIcon?.Dispose();
        Shutdown();
    }
}
```

**Step 3: Verify build and run**

```bash
dotnet build
dotnet run --project src/PortMonitor
```

Expected: Application starts, shows port list, tray icon appears. Closing window hides to tray.

**Step 4: Commit**

```bash
git add src/PortMonitor/App.xaml src/PortMonitor/App.xaml.cs
git commit -m "feat: add DI setup, system tray icon, minimize-to-tray"
```

---

## Task 10: Polish & Final Verification

**Files:**
- Possibly adjust: any file with build warnings

**Step 1: Build in Release mode**

```bash
dotnet build -c Release
```

**Step 2: Publish as single-file exe**

```bash
dotnet publish src/PortMonitor -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/
```

**Step 3: Run and manually verify all features**

1. Application starts, port list loads
2. Click Odswiez — list refreshes
3. Type in filter — list filters by text
4. Change protocol dropdown — filters by TCP/UDP
5. Toggle auto-refresh — list updates automatically
6. Select a port, click Zakoncz proces — confirmation dialog, process killed
7. Click Eksport CSV — save dialog, CSV file saved with correct content
8. Click X — window hides to tray
9. Double-click tray icon — window reappears
10. Right-click tray → Zakoncz — application closes

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: Port Monitor WPF — complete implementation"
```

---

## Summary

| Task | Component | Est. Size |
|------|-----------|-----------|
| 1 | Scaffold project + NuGet | Setup |
| 2 | PortEntry model | ~15 lines |
| 3 | NativeMethods P/Invoke | ~80 lines |
| 4 | PortScannerService | ~120 lines |
| 5 | ProcessService | ~30 lines |
| 6 | MainViewModel | ~150 lines |
| 7 | StatusToBrushConverter | ~25 lines |
| 8 | MainWindow XAML + code-behind | ~100 lines XAML + 20 lines C# |
| 9 | App.xaml DI + tray | ~70 lines |
| 10 | Polish, publish, verify | — |

**Total:** ~600 lines of code across 10 tasks.
