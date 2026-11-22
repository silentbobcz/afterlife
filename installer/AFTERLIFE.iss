; AFTERLIFE installer (Inno Setup 6), a second download next to the release zip.
; Built by .github/workflows/build.yml from the same folder the zip is made from:
;   ISCC.exe /DAppVersion=1.0.0-beta.1 /DDistDir=<path to dist\AFTERLIFE> installer\AFTERLIFE.iss
; Per-user install without administrator rights. The installer and the uninstaller only handle the files
; of the app itself: the library and the settings (%LOCALAPPDATA%\AFTERLIFE, %APPDATA%\AFTERLIFE) are never
; touched; "Remove AFTERLIFE from this PC..." in the app's settings is the way to delete them.

#ifndef AppVersion
  #error Pass the version on the command line: ISCC.exe /DAppVersion=1.0.0-beta.1 AFTERLIFE.iss
#endif
#ifndef DistDir
  #define DistDir "..\dist\AFTERLIFE"
#endif
#ifndef OutputName
  #define OutputName "AFTERLIFE-Offline-" + AppVersion + "-win-x64-Setup"
#endif

; Windows file version info takes numbers only: 1.0.0-beta.1 -> 1.0.0
#define DashPos Pos("-", AppVersion)
#if DashPos > 0
  #define AppVersionNumeric Copy(AppVersion, 1, DashPos - 1)
#else
  #define AppVersionNumeric AppVersion
#endif

[Setup]
; the AppId identifies the installation for upgrades and the uninstaller: never change it
AppId={{7771FB68-637C-417B-8E67-E495D8DF7508}
AppName=AFTERLIFE
AppVersion={#AppVersion}
AppVerName=AFTERLIFE {#AppVersion}
AppPublisher=Silentbobcz
AppPublisherURL=https://github.com/silentbobcz/afterlife
AppSupportURL=https://github.com/silentbobcz/afterlife/issues
AppUpdatesURL=https://github.com/silentbobcz/afterlife/releases
AppCopyright=(c) 2026 Silentbobcz, MIT License
VersionInfoVersion={#AppVersionNumeric}
VersionInfoProductName=AFTERLIFE
VersionInfoDescription=AFTERLIFE Setup
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\AFTERLIFE
DisableProgramGroupPage=yes
DefaultGroupName=AFTERLIFE
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
SetupIconFile=..\src\Afterlife.App\Assets\afterlife.ico
UninstallDisplayIcon={app}\AFTERLIFE.exe
UninstallDisplayName=AFTERLIFE
OutputDir=..\dist
OutputBaseFilename={#OutputName}
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#DistDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\AFTERLIFE"; Filename: "{app}\AFTERLIFE.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\AFTERLIFE"; Filename: "{app}\AFTERLIFE.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\AFTERLIFE.exe"; Description: "{cm:LaunchProgram,AFTERLIFE}"; Flags: nowait postinstall skipifsilent
