# Port Monitor WPF — Design Document

**Data:** 2026-03-10
**Technologia:** WPF (.NET 8) + CommunityToolkit.Mvvm + MVVM
**Cel:** Przebudowa aplikacji Monitor Portow z Python/Tkinter na C#/XAML

## Funkcjonalnosc

### Funkcje bazowe (z oryginalu)
1. Lista portow w stanie LISTEN (TCP + UDP)
2. Informacje o procesach (PID, nazwa programu)
3. Reczne odswiezanie listy
4. Konczenie procesow z poziomu GUI

### Nowe funkcje
5. Auto-odswiezanie (timer konfigurowalny 1-30s, toggle on/off)
6. Filtrowanie i wyszukiwanie (po porcie, nazwie procesu, protokole)
7. Eksport danych do CSV
8. System tray icon + minimalizacja do traya

## Architektura

### Struktura projektu

```
PortMonitor.sln
└── PortMonitor/
    ├── App.xaml / App.xaml.cs          — Entry point, DI setup
    ├── MainWindow.xaml / .cs           — Shell window
    ├── Models/
    │   └── PortEntry.cs                — Port, Protocol, PID, ProcessName, LocalAddress, Status
    ├── ViewModels/
    │   └── MainViewModel.cs            — ObservableCollection<PortEntry>, komendy, filtrowanie
    ├── Services/
    │   ├── IPortScanner.cs             — Interfejs skanera
    │   ├── PortScannerService.cs       — GetExtendedTcpTable P/Invoke + UDP
    │   └── ProcessService.cs           — Kill process, get process info
    ├── Converters/
    │   └── StatusToBrushConverter.cs    — Kolorowanie wierszy wg statusu
    ├── Resources/
    │   ├── Styles.xaml                 — Globalne style
    │   └── app.ico                     — Ikona aplikacji / tray
    └── Helpers/
        └── NativeMethods.cs            — P/Invoke deklaracje (iphlpapi.dll)
```

### Zaleznosci NuGet
- `CommunityToolkit.Mvvm` — MVVM infrastruktura (ObservableProperty, RelayCommand)
- `Microsoft.Extensions.DependencyInjection` — DI container
- `Hardcodet.NotifyIcon.Wpf` — System tray icon

### Wzorzec DI
`App.xaml.cs` konfiguruje `ServiceProvider`, wstrzykuje `IPortScanner` do `MainViewModel`.

## UI Layout

```
┌─────────────────────────────────────────────────────┐
│  Monitor Portow                            [_][□][X] │
├─────────────────────────────────────────────────────┤
│ [Filtruj: _______________] [Protokol: ▼Wszystkie]   │
│ [Auto-odswiezanie: ☑ co 5s ▼]                      │
├─────────────────────────────────────────────────────┤
│ Port │ Protokol │ Adres      │ PID  │ Proces  │ St. │
│──────┼──────────┼────────────┼──────┼─────────┼─────│
│ 80   │ TCP      │ 0.0.0.0    │ 4120 │ nginx   │ LIST│
│ 443  │ TCP      │ 0.0.0.0    │ 4120 │ nginx   │ LIST│
│ ...  │          │            │      │         │     │
├─────────────────────────────────────────────────────┤
│ [Odswiez] [Zakoncz proces] [Eksport CSV]            │
│ Portow: 42  │  Ostatnie odswiezenie: 14:32:05       │
└─────────────────────────────────────────────────────┘
```

## Przeplyw danych

```
DispatcherTimer (co Xs)
        │
        ▼
PortScannerService.GetActivePortsAsync()
   ├── GetExtendedTcpTable (P/Invoke iphlpapi.dll) → TCP porty + PID
   ├── GetExtendedUdpTable (P/Invoke iphlpapi.dll) → UDP porty + PID
   └── Process.GetProcessById(pid).ProcessName
        │
        ▼
MainViewModel.PortEntries (ObservableCollection)
        │
        ├── CollectionViewSource (filtrowanie + sortowanie)
        │
        ▼
DataGrid (binding do filtered view)
```

## Skanowanie portow — P/Invoke

Uzycie `GetExtendedTcpTable` i `GetExtendedUdpTable` z `iphlpapi.dll`:
- Zwraca porty + PID (czego .NET `IPGlobalProperties` nie daje)
- Wymaga deklaracji struktur: `MIB_TCPROW_OWNER_PID`, `MIB_TCPTABLE_OWNER_PID`
- Analogicznie dla UDP: `MIB_UDPROW_OWNER_PID`, `MIB_UDPTABLE_OWNER_PID`

## Filtrowanie

- `ICollectionView` z `CollectionViewSource` na `PortEntries`
- Predykat filtrujacy: tekst wyszukiwania (port/nazwa procesu) + filtr protokolu (TCP/UDP/Wszystkie)
- Sortowanie natywne DataGrid po kliknieciu naglowka kolumny

## Obsluga bledow

| Scenariusz | Obsluga |
|---|---|
| Kill procesu systemowego (PID 0, 4) | AccessDeniedException → komunikat "Brak uprawnien" |
| Proces zniknal miedzy skanem a Kill | InvalidOperationException → "Proces juz nie istnieje" |
| Nazwa procesu niedostepna | Wyswietl `<nieznany>` |
| Wielokrotne klikniecie Zakoncz | Obslugiwane przez catch |
| Brak portow | Pusta lista, brak bledu |

## Uprawnienia

- Odczyt portow: zwykle uprawnienia
- Kill procesow innych uzytkownikow: wymaga podwyzszonych uprawnien
- NIE wymuszamy "Run as Admin" — informujemy o braku uprawnien w razie potrzeby

## Wydajnosc

- Skanowanie na osobnym watku (`Task.Run`) — UI responsywny
- Aktualizacja kolekcji batch'em przy duzej liczbie portow
- Timer pauzowany podczas trwajacego skanu (brak nakladania sie)

## System Tray

- Zamkniecie okna (X) → minimalizacja do traya
- Podwojne klikniecie tray icon → przywrocenie okna
- Context menu: Pokaz / Zakoncz
- Auto-odswiezanie dziala w tle nawet po minimalizacji

## Eksport CSV

- `SaveFileDialog` z filtrem `*.csv`
- Eksportuje wszystkie lub przefiltrowane wiersze
- Kodowanie UTF-8 z BOM (dla poprawnego otwarcia w Excelu)
