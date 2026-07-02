; ============================================================
;  ShieldAV Antivirus – Inno Setup 6 Installer Script
;  Self-contained .NET 8 — kein .NET auf Ziel-PC noetig!
; ============================================================

#define MyAppName      "ShieldAV Antivirus"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "ShieldAV"
#define MyAppExeName   "ShieldAV.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ShieldAV
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE.txt
OutputDir=installer_output
OutputBaseFilename=ShieldAV_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Installer – Kein .NET erforderlich
MinVersion=10.0.17763
ArchitecturesInstallIn64BitMode=x64os
ArchitecturesAllowed=x64os

[Languages]
Name: "german";  MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart";   Description: "ShieldAV beim Windows-Start ausfuehren"; GroupDescription: "Autostart:"

[Files]
; ── Alle self-contained Dateien aus publish\ ───────────────
; Enthält .NET 8 Runtime, WinForms, alle DLLs — kein .NET nötig!
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── Begleittexte ───────────────────────────────────────────
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md";   DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}";                        Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}";  Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";                  Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "ShieldAV"; \
  ValueData: """{app}\{#MyAppExeName}"""; \
  Tasks: autostart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ShieldAV"; \
  ValueType: string; ValueName: "InstallPath"; \
  ValueData: "{app}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; \
  Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\ShieldAV"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not Is64BitInstallMode then
  begin
    MsgBox('ShieldAV benoetigt Windows 10/11 (64-Bit).', mbError, MB_OK);
    Result := False;
  end;
  // Kein .NET-Check noetig — Runtime ist im Installer enthalten!
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    ForceDirectories(ExpandConstant('{userappdata}\ShieldAV\Quarantine'));
end;
