; Inno Setup Script for Video Piper
; https://github.com/mi222eh/Video-Piper

#ifndef MyAppName
  #define MyAppName "Video Piper"
#endif

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef MyAppPublisher
  #define MyAppPublisher "Video Piper"
#endif

#ifndef MyAppURL
  #define MyAppURL "https://github.com/mi222eh/Video-Piper"
#endif

#ifndef MyAppExeName
  #define MyAppExeName "VideoPiper.exe"
#endif

#ifndef SourceDir
  #define SourceDir "..\video-piper\publish"
#endif

#ifndef OutputDir
  #define OutputDir "output"
#endif

[Setup]
; Unique application GUID
AppId={{D8C9A312-3D28-4B11-8B39-44F4E4078D1C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation paths
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

; Architecture (64-bit Windows)
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; Privileges: allows both per-user and system-wide installation
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Output file
OutputDir={#OutputDir}
OutputBaseFilename=VideoPiper-Setup-{#MyAppVersion}
SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; UI & Behavior
WizardStyle=modern
DisableDirPage=no
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "sv"; MessagesFile: "compiler:Languages\Swedish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall skipifsilent
