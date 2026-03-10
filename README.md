# Monitor Portow

Aplikacja desktopowa do monitorowania portow sieciowych na systemie Windows. Umozliwia identyfikacje programow blokujacych porty oraz zakonczenie tych procesow z poziomu interfejsu graficznego.

Zbudowana w technologii **C# / WPF (.NET 10)** z wzorcem **MVVM**.

## Funkcje

- **Lista portow** — wyswietla wszystkie zajete porty TCP i UDP z informacja o PID i nazwie procesu
- **Filtrowanie i wyszukiwanie** — filtruj po numerze portu, nazwie procesu, adresie lub protokole (TCP/UDP)
- **Auto-odswiezanie** — konfigurowalny timer (1-30 sekund) z mozliwoscia wlaczenia/wylaczenia
- **Zakonczenie procesu** — zamknij aplikacje blokujaca wybrany port bezposrednio z GUI
- **Eksport do CSV** — zapisz liste portow (cala lub przefiltrowana) do pliku CSV
- **System tray** — minimalizacja do zasobnika systemowego, powrot podwojnym kliknieciem
- **Kolorowanie wierszy** — wizualne rozroznienie statusow (Listen, Established, TimeWait, CloseWait)

## Wymagania

- Windows 10 lub nowszy (x64)
- .NET 10 Runtime (jesli uzywasz wersji framework-dependent) lub brak wymagan (wersja self-contained)

## Uruchomienie

### Z kodu zrodlowego

```bash
dotnet run --project src/PortMonitor
```

### Z pliku EXE

Pobierz `PortMonitor.exe` z [Releases](../../releases) i uruchom.

### Instalator

Pobierz `MonitorPortow_Setup_1.0.0.exe` z [Releases](../../releases). Instaluje do `Program Files (x86)` z opcja ikony na pulpicie i autostartu z systemem.

## Budowanie

```bash
# Debug
dotnet build

# Release — self-contained single-file EXE
dotnet publish src/PortMonitor -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish/
```

### Budowanie instalatora (wymaga Inno Setup 6)

```bash
"C:/Program Files (x86)/Inno Setup 6/ISCC.exe" installer/setup.iss
```

## Architektura

```
src/PortMonitor/
├── Models/          PortEntry — model danych portu
├── ViewModels/      MainViewModel — logika MVVM (CommunityToolkit.Mvvm)
├── Services/        PortScannerService — skanowanie portow (P/Invoke iphlpapi.dll)
│                    ProcessService — konczenie procesow
├── Converters/      StatusToBrushConverter — kolorowanie wierszy
├── Helpers/         NativeMethods — deklaracje P/Invoke
└── Resources/       app.ico — ikona aplikacji
```

**Technologie:** C# 13, .NET 10, WPF, XAML, CommunityToolkit.Mvvm, Hardcodet.NotifyIcon.Wpf

## Licencja

[GNU General Public License v3.0](LICENSE)
