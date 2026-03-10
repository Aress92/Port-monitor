[Setup]
AppId={{E8F3A1B2-7C4D-4E5F-9A6B-1C2D3E4F5A6B}
AppName=Monitor Portow
AppVersion=1.0.0
AppPublisher=Port Monitor
AppPublisherURL=https://github.com
DefaultDirName={autopf}\MonitorPortow
DefaultGroupName=Monitor Portow
UninstallDisplayIcon={app}\PortMonitor.exe
OutputDir=..\output
OutputBaseFilename=MonitorPortow_Setup_1.0.0
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=lowest
; SetupIconFile=..\src\PortMonitor\Resources\app.ico

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "desktopicon"; Description: "Utworz ikone na pulpicie"; GroupDescription: "Dodatkowe ikony:"
Name: "startupicon"; Description: "Uruchamiaj przy starcie systemu"; GroupDescription: "Opcje:"

[Files]
Source: "..\publish\PortMonitor.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Monitor Portow"; Filename: "{app}\PortMonitor.exe"
Name: "{group}\Odinstaluj Monitor Portow"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Monitor Portow"; Filename: "{app}\PortMonitor.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MonitorPortow"; ValueData: """{app}\PortMonitor.exe"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\PortMonitor.exe"; Description: "Uruchom Monitor Portow"; Flags: nowait postinstall skipifsilent
