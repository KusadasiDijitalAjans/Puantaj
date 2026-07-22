; Puantaj V1.0.2 - Inno Setup script
; Bu betik yalnızca Windows üzerinde, Inno Setup 6 (https://jrsoftware.org/isinfo.php) ile
; derlenebilir (ISCC.exe Puantaj.iss). macOS/Linux üzerinde derlenemez.
; Önce "dotnet publish src\PuantajApp\PuantajApp.csproj -c Release" çalıştırılmış olmalıdır
; (csproj zaten RuntimeIdentifier=win-x64, SelfContained=true, PublishSingleFile=true içerir).

#define MyAppName "Puantaj"
#define MyAppVersion "1.0.2"
#define MyAppPublisher "Puantaj"
#define MyAppExeName "PuantajApp.exe"
#define MyPublishDir "..\src\PuantajApp\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{6C3E2E1A-8B7A-4E4E-9C3D-9C1E7B6D9F31}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Puantaj_Setup_v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek görevler:"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Puantaj'ı şimdi başlat"; Flags: nowait postinstall skipifsilent
