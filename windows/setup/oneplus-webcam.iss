#define MyAppName "OnePlus Webcam"
#define MyAppVersion "1.0.0"
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "autostart"; Description: "Start with Windows"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\..\publish\OnePlusWebcam.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "vcam.ini"; DestDir: "{app}"; Flags: ignoreversion
Source: "vendor\scrcpy\*"; DestDir: "{app}\tools\scrcpy"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "vendor\ffmpeg\ffmpeg.exe"; DestDir: "{app}\tools\ffmpeg"; Flags: ignoreversion
Source: "vendor\ffmpeg\ffplay.exe"; DestDir: "{app}\tools\ffmpeg"; Flags: ignoreversion
Source: "vendor\akvirtualcamera-windows-9.4.1.exe"; DestDir: "{tmp}"; DestName: "akvcam-setup.exe"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OnePlusWebcam"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch OnePlus Webcam"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\tools\akvcam\AkVCamManager.exe"; Parameters: "remove-device OnePlusWebcam"; RunOnceId: "RemoveVCam"; Flags: runhidden
Filename: "{app}\tools\akvcam\AkVCamManager.exe"; Parameters: "update"; RunOnceId: "UpdateVCam"; Flags: runhidden

[Code]
function FindAkVCamManager(): String;
begin
  Result := ExpandConstant('{app}\tools\akvcam\AkVCamManager.exe');
  if FileExists(Result) then
    Exit;

  Result := ExpandConstant('{pf}\akvirtualcamera\x64\AkVCamManager.exe');
  if FileExists(Result) then
    Exit;

  Result := ExpandConstant('{pf}\AkVirtualCamera\AkVCamManager.exe');
  if FileExists(Result) then
    Exit;

  Result := '';
end;

procedure CopyAkVCamManager();
var
  Src, DestDir, Dest: String;
begin
  Src := FindAkVCamManager();
  DestDir := ExpandConstant('{app}\tools\akvcam');
  Dest := DestDir + '\AkVCamManager.exe';
  if (Src <> '') and (Src <> Dest) and FileExists(Src) then
  begin
    ForceDirectories(DestDir);
    FileCopy(Src, Dest, False);
  end;
end;

function RunAkVCam(Params: String): Boolean;
var
  Manager: String;
  ResultCode: Integer;
begin
  Result := False;
  Manager := ExpandConstant('{app}\tools\akvcam\AkVCamManager.exe');
  if not FileExists(Manager) then
    Manager := FindAkVCamManager();
  if Manager = '' then
    Exit;
  Result := Exec(Manager, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  SetupExe: String;
begin
  if CurStep = ssPostInstall then
  begin
    SetupExe := ExpandConstant('{tmp}\akvcam-setup.exe');
    if not Exec(SetupExe, '/S', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      Exec(SetupExe, '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    CopyAkVCamManager();
    RunAkVCam('load "' + ExpandConstant('{app}\vcam.ini') + '"');
    RunAkVCam('set-page-size 128000000');
  end;
end;
