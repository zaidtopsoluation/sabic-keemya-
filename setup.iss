; TS Emergency Management System installer (Inno Setup 6). Build with build_installer.ps1, which publishes the app into PublishOutput first.
; New install: asks for the station role, name and the two server IPs, then writes appsettings.json.
; Upgrade: an existing appsettings.json is kept and the setup pages are skipped.

#define MyAppName "TS Emergency Management System"
#define MyAppVersion "1.0.14"
#define MyAppPublisher "Top Solutions"
#define MyAppExeName "TS_EMS.exe"
#define SourceDir "PublishOutput"

[Setup]
; Same AppId as the earlier EMS Command Center installer, so this upgrades it in place.
AppId={{5E58A48B-22F2-4467-92B1-4A59EE9A0D8D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=no
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=installer
OutputBaseFilename=TS_EMS_Setup_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=Frontend\Assets\app.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
; The app writes session.txt, language.txt and siren_comm.log next to its exe; Program Files is read-only for normal users.
Name: "{app}"; Permissions: users-modify

[InstallDelete]
; Upgrade from the Keemya-named builds: drop the old exe and shortcuts.
Type: files; Name: "{app}\Keemya.Frontend.*"
Type: files; Name: "{autodesktop}\Keemya EMS.lnk"
Type: filesandordirs; Name: "{autoprograms}\Keemya EMS"

[Files]
; appsettings.json is written by the setup pages below (or kept on upgrade), never copied from the build.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "appsettings.json,*.pdb,Obfuscated\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  RolePage: TInputOptionWizardPage;
  StationPage: TInputQueryWizardPage;
  DbPage: TInputQueryWizardPage;

const
  RoleServer1 = 0;
  RoleServer2 = 1;
  RoleDispatcher = 2;

function ConfigExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\appsettings.json'));
end;

function WebView2Installed: Boolean;
var
  Version: String;
begin
  Result :=
    RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
    RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
  Result := Result and (Version <> '') and (Version <> '0.0.0.0');
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not WebView2Installed then
    MsgBox('Microsoft Edge WebView2 Runtime was not found. The map screen needs it.' #13#10 +
           'Install it from https://developer.microsoft.com/microsoft-edge/webview2/ after this setup.',
           mbInformation, MB_OK);
end;

procedure InitializeWizard;
begin
  RolePage := CreateInputOptionPage(wpSelectDir,
    'Station role', 'What is this PC used for?',
    'A server is connected to the sirens and runs MySQL. A dispatcher is an operator screen only.',
    True, False);
  RolePage.Add('Server 1 (RCB) - siren controller with its own MySQL');
  RolePage.Add('Server 2 (PCB) - siren controller with its own MySQL');
  RolePage.Add('Dispatcher - operator screen, no siren connection, no database');
  RolePage.SelectedValueIndex := RoleDispatcher;

  StationPage := CreateInputQueryPage(RolePage.ID,
    'Station and servers', 'Name this station and enter the two server addresses.',
    'Use the IP address (or computer name) of each server PC.');
  StationPage.Add('Station name (shown on screen, e.g. Admin ECC):', False);
  StationPage.Add('Server 1 (RCB) IP address:', False);
  StationPage.Add('Server 2 (PCB) IP address:', False);

  DbPage := CreateInputQueryPage(StationPage.ID,
    'Database login', 'MySQL account used by the application.',
    'Use the same account on every station. It must exist on both servers.');
  DbPage.Add('MySQL user:', False);
  DbPage.Add('MySQL password:', True);
  DbPage.Values[0] := 'keemya_app';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  // Upgrade: keep the existing station configuration untouched.
  Result := ((PageID = RolePage.ID) or (PageID = StationPage.ID) or (PageID = DbPage.ID)) and ConfigExists;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = StationPage.ID) and (StationPage.Values[0] = '') then
  begin
    case RolePage.SelectedValueIndex of
      RoleServer1: StationPage.Values[0] := 'RCB-Controller';
      RoleServer2: StationPage.Values[0] := 'PCB-Controller';
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = StationPage.ID then
  begin
    if (Trim(StationPage.Values[0]) = '') or (Trim(StationPage.Values[1]) = '') or (Trim(StationPage.Values[2]) = '') then
    begin
      MsgBox('Please fill in the station name and both server addresses.', mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = DbPage.ID then
  begin
    if Trim(DbPage.Values[0]) = '' then
    begin
      MsgBox('Please enter the MySQL user.', mbError, MB_OK);
      Result := False;
    end
    else if (Pos(';', DbPage.Values[0]) > 0) or (Pos(';', DbPage.Values[1]) > 0) then
    begin
      // ';' would break the MySQL connection string.
      MsgBox('The MySQL user and password cannot contain ";".', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function JsonEscape(const S: String): String;
begin
  Result := S;
  StringChangeEx(Result, '\', '\\', True);
  StringChangeEx(Result, '"', '\"', True);
end;

function ConnString(const Host: String): String;
begin
  Result := 'Server=' + Trim(Host) + ';Port=3306;Database=keemya;User=' + Trim(DbPage.Values[0]) +
            ';Password=' + DbPage.Values[1] + ';';
end;

procedure WriteAppSettings;
var
  Server1, Server2, PrimaryHost, SecondaryHost, PrimaryName, SecondaryName, Role, Json: String;
begin
  Server1 := StationPage.Values[1];
  Server2 := StationPage.Values[2];
  Role := 'Server';
  case RolePage.SelectedValueIndex of
    RoleServer1:
      begin
        // A server uses its own MySQL first, so it keeps working if the other server or the network is down.
        PrimaryHost := 'localhost'; SecondaryHost := Server2;
        PrimaryName := 'Server 1 (RCB)'; SecondaryName := 'Server 2 (PCB)';
      end;
    RoleServer2:
      begin
        PrimaryHost := 'localhost'; SecondaryHost := Server1;
        PrimaryName := 'Server 2 (PCB)'; SecondaryName := 'Server 1 (RCB)';
      end;
  else
    begin
      Role := 'Dispatcher';
      PrimaryHost := Server1; SecondaryHost := Server2;
      PrimaryName := 'Server 1 (RCB)'; SecondaryName := 'Server 2 (PCB)';
    end;
  end;

  Json :=
    '{' + #13#10 +
    '  "ConnectionStrings": {' + #13#10 +
    '    "DefaultConnection": "' + JsonEscape(ConnString(PrimaryHost)) + '",' + #13#10 +
    '    "PrimaryConnection": "' + JsonEscape(ConnString(PrimaryHost)) + '",' + #13#10 +
    '    "SecondaryConnection": "' + JsonEscape(ConnString(SecondaryHost)) + '"' + #13#10 +
    '  },' + #13#10 +
    '  "ServerNodes": {' + #13#10 +
    '    "PrimaryName": "' + PrimaryName + '",' + #13#10 +
    '    "SecondaryName": "' + SecondaryName + '",' + #13#10 +
    '    "FailoverTimeoutSeconds": 2' + #13#10 +
    '  },' + #13#10 +
    '  "StationName": "' + JsonEscape(Trim(StationPage.Values[0])) + '",' + #13#10 +
    '  "Role": "' + Role + '",' + #13#10 +
    '  "PttRelayPort": "COM3"' + #13#10 +
    '}' + #13#10;

  if not SaveStringToFile(ExpandConstant('{app}\appsettings.json'), Json, False) then
    MsgBox('Could not write appsettings.json. Please create it manually in ' + ExpandConstant('{app}'), mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not ConfigExists then
    WriteAppSettings;
end;
