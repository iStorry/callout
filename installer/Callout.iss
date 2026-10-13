; Inno Setup script for Callout. Built by .github/workflows/release.yml:
;   iscc /DAppVersion=0.1.0 /DArch=x64 /DSourceDir=<published app folder> /DOutputDir=<dir> installer\Callout.iss
; Arch is "x64" or "arm64".

#ifndef AppVersion
  #error Pass /DAppVersion=<major.minor.patch>
#endif
#ifndef Arch
  #error Pass /DArch=x64 or /DArch=arm64
#endif
#ifndef SourceDir
  #error Pass /DSourceDir=<published app folder>
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

#if Arch == "x64"
  #define AllowedArchitectures "x64compatible"
#elif Arch == "arm64"
  #define AllowedArchitectures "arm64"
#else
  #error Arch must be x64 or arm64
#endif

#define AppName "Callout"
#define AppExe "Callout.exe"
#define AppUrl "https://github.com/iStorry/callout"

[Setup]
; Never change AppId: it is how upgrades find the existing installation.
AppId={{2E9430F8-3AD4-4165-BB4A-B3CBFF8C990D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=iStorry
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
; Per-user install into %LocalAppData%\Programs: no administrator prompt.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed={#AllowedArchitectures}
ArchitecturesInstallIn64BitMode={#AllowedArchitectures}
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=Callout-Setup-{#Arch}
SetupIconFile=..\src\Callout\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Settings and downloaded models (up to ~1 GB) live here; uninstalling should give the space back.
Type: filesandordirs; Name: "{localappdata}\{#AppName}"
