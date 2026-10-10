; Inno Setup script for MessageTrigger.
; Build with installer\build-installer.ps1 (publishes the app, then runs ISCC).

#define AppName "MessageTrigger"
#define AppVersion "1.0.0"
#define AppExe "MessageTrigger.exe"

[Setup]
AppId={{7D3F2A64-5B1E-4C8A-9E27-0A6C41B8F3D5}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=coolfuton8
AppPublisherURL=https://github.com/coolfuton8/MessageTrigger
AppSupportURL=https://github.com/coolfuton8/MessageTrigger/issues
; Per-user install (no admin): the app writes config.json next to the exe, so it must live in a writable folder.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
