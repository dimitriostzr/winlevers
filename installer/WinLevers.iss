; Inno Setup script for the WinLevers installer. Built by scripts/Build-Release.ps1,
; which passes the version, the architecture and the published folder:
;
;   ISCC /DAppVersion=0.1.0 /DArch=x64 /DSourceDir=<publish folder> /DOutputDir=<dir> WinLevers.iss
;
; Per-user and unelevated on purpose: the app writes only HKCU values and runs
; without administrator rights, so the installer has no reason to ask for them
; either. It installs to %LOCALAPPDATA%\Programs\WinLevers.
;
; The journal, snapshots, logs and settings live in %LOCALAPPDATA%\WinLevers,
; outside the install folder, and the uninstaller leaves them there. Removing
; the app must not remove the record of what it changed.

#ifndef AppVersion
  #error AppVersion is required, e.g. /DAppVersion=0.1.0
#endif
#ifndef Arch
  #error Arch is required: x64 or arm64
#endif
#ifndef SourceDir
  #error SourceDir is required: the dotnet publish output folder
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\release"
#endif

#define AppName "WinLevers"
#define AppExe "WinLevers.App.exe"

[Setup]
; Never change the AppId: it is how an upgrade finds the copy it replaces.
AppId={{6F1C2B7E-3D4A-4E8B-9A57-2C1D8E4F0B93}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Dimitrios T
AppPublisherURL=https://github.com/dimitriostzr/winlevers
AppSupportURL=https://github.com/dimitriostzr/winlevers
AppUpdatesURL=https://github.com/dimitriostzr/winlevers/releases
AppCopyright=Copyright (c) 2026 Dimitrios T
VersionInfoVersion={#AppVersion}

PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE

#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0.17763

SetupIconFile=..\src\WinLevers.App\Assets\WinLevers.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
CloseApplications=yes

OutputDir={#OutputDir}
OutputBaseFilename={#AppName}-{#AppVersion}-win-{#Arch}-setup
Compression=lzma2/ultra64
SolidCompression=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
