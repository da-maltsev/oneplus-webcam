#define MyAppName "OnePlus Webcam"
#define MyAppVersion "1.0.3"
#define MyAppPublisher "Daniil Maltsev"
#define MyAppExeName "OnePlusWebcam.exe"

[Setup]
AppId={{A7C3E1D0-8B44-4F2A-9C11-0E4B1PLUSCAM}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\OnePlus Webcam
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=OnePlusWebcam-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
MinVersion=10.0
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "autostart"; Description: "Start with Windows"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\..\publish\OnePlusWebcam.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "vcam.ini"; DestDir: "{app}"; Flags: ignoreversion
Source: "vcam.ini"; DestDir: "{app}\tools\akvcam"; Flags: ignoreversion
Source: "register-vcam.cmd"; DestDir: "{app}\tools\akvcam"; Flags: ignoreversion
Source: "vendor\scrcpy\*"; DestDir: "{app}\tools\scrcpy"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "vendor\ffmpeg\ffmpeg.exe"; DestDir: "{app}\tools\ffmpeg"; Flags: ignoreversion
Source: "vendor\ffmpeg\ffplay.exe"; DestDir: "{app}\tools\ffmpeg"; Flags: ignoreversion
Source: "vendor\akvcam\*"; DestDir: "{app}\tools\akvcam"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "vendor\akvirtualcamera-windows-9.4.1.exe"; DestDir: "{app}\tools\akvcam"; DestName: "akvirtualcamera-setup.exe"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OnePlusWebcam"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\tools\akvcam\akvirtualcamera-setup.exe"; Parameters: "/S"; StatusMsg: "Installing virtual camera driver..."; Flags: runhidden waituntilterminated skipifdoesntexist
Filename: "{app}\tools\akvcam\register-vcam.cmd"; StatusMsg: "Registering OnePlus Webcam..."; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "Launch OnePlus Webcam"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{app}\tools\akvcam\AkVCamManager.exe"; Parameters: "remove-device OnePlusWebcam"; RunOnceId: "RemoveVCam"; Flags: runhidden skipifdoesntexist
Filename: "{app}\tools\akvcam\AkVCamManager.exe"; Parameters: "update"; RunOnceId: "UpdateVCam"; Flags: runhidden skipifdoesntexist

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsAdmin then
  begin
    MsgBox('OnePlus Webcam must be installed as administrator so Windows can register the virtual webcam driver.' + #13#10#13#10 +
      'Right-click OnePlusWebcam-Setup.exe and choose Run as administrator.', mbError, MB_OK);
    Result := False;
  end;
end;
