#define MyAppName "TalkToMe"
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "TalkToMe"
#define MyAppExeName "TalkToMe.App.exe"

[Setup]
AppId={{09E332AA-22B2-4C2F-826B-1EE8E7CBAF90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=TalkToMe-Setup
SetupIconFile=..\icons\TalkToMe-icon-pack\TalkToMe.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Excludes: "Models\*,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TalkToMe"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--show-window"
Name: "{userdesktop}\TalkToMe"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--show-window"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TalkToMe"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--minimized"; Description: "Start TalkToMe in the system tray"; Flags: nowait postinstall; Check: ShouldStartApplication

[Code]
function HasCommandLineParameter(const Parameter: String): Boolean;
var
  Index: Integer;
begin
  Result := False;
  for Index := 1 to ParamCount do
  begin
    if CompareText(ParamStr(Index), Parameter) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function ShouldStartApplication(): Boolean;
begin
  Result := (not WizardSilent) or HasCommandLineParameter('/TALKTOMERESTART');
end;
