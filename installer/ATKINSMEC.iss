; ATKINSMEC - Inno Setup script
;
; Optional. It produces a single Setup.exe instead of the zip, and needs
; Inno Setup 6 (free, jrsoftware.org/isdl.php) installed on the machine
; that BUILDS the installer. Nothing extra is needed on the machines that
; RUN it.
;
; PrivilegesRequired=lowest is the point: everything lands under
; {userappdata}, so no administrator rights are asked for.
;
; Build it with:  build-installer.ps1   (it is picked up automatically)
; or by hand:     ISCC.exe installer\ATKINSMEC.iss
;
; NOTE: this script has never been compiled - Inno Setup is not installed
; on the machine it was written on. The zip installer is the tested one.

#define AppName "ATKINSMEC"
#define AppVersion "1.0.0"
#define AppPublisher "ATKINS"

[Setup]
AppId={{8E5A2C41-6B3D-4F17-9A0E-2C7D5B914E63}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={userappdata}\Autodesk\Revit\Addins
DisableDirPage=yes
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName} for Revit
OutputDir=output
OutputBaseFilename=ATKINSMEC-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

; No administrator rights: the add-in installs into the current user's
; own profile, which is where Revit looks for per-user add-ins
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Types]
Name: "full";   Description: "Every Revit version found"
Name: "custom"; Description: "Choose versions"; Flags: iscustom

[Components]
Name: "v2022"; Description: "Revit 2022"; Types: full custom; Check: RevitPresent('2022')
Name: "v2023"; Description: "Revit 2023"; Types: full custom; Check: RevitPresent('2023')
Name: "v2024"; Description: "Revit 2024"; Types: full custom; Check: RevitPresent('2024')
Name: "v2025"; Description: "Revit 2025"; Types: full custom; Check: RevitPresent('2025')
Name: "v2026"; Description: "Revit 2026"; Types: full custom; Check: RevitPresent('2026')
Name: "v2027"; Description: "Revit 2027"; Types: full custom; Check: RevitPresent('2027')

[Files]
Source: "payload\2022\*"; DestDir: "{app}\2022"; Components: v2022; Flags: recursesubdirs ignoreversion
Source: "payload\2023\*"; DestDir: "{app}\2023"; Components: v2023; Flags: recursesubdirs ignoreversion
Source: "payload\2024\*"; DestDir: "{app}\2024"; Components: v2024; Flags: recursesubdirs ignoreversion
Source: "payload\2025\*"; DestDir: "{app}\2025"; Components: v2025; Flags: recursesubdirs ignoreversion
Source: "payload\2026\*"; DestDir: "{app}\2026"; Components: v2026; Flags: recursesubdirs ignoreversion
Source: "payload\2027\*"; DestDir: "{app}\2027"; Components: v2027; Flags: recursesubdirs ignoreversion

[Code]

// A version is offered when Revit is installed, or when its add-ins
// folder already exists (Revit installed somewhere non-standard)
function RevitPresent(Version: String): Boolean;
begin
  Result :=
    FileExists(ExpandConstant('{commonpf}\Autodesk\Revit ' + Version + '\Revit.exe')) or
    DirExists(ExpandConstant('{userappdata}\Autodesk\Revit\Addins\') + Version);
end;

// Revit holds its add-in DLLs open, so replacing them while it runs
// silently fails. Refuse rather than half-install.
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if Exec(ExpandConstant('{cmd}'),
          '/C tasklist /FI "IMAGENAME eq Revit.exe" | find /I "Revit.exe"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      MsgBox('Revit is running.' + #13#10#13#10 +
             'Windows will not let its add-in files be replaced while it ' +
             'is open, so the install would appear to work and change ' +
             'nothing.' + #13#10#13#10 +
             'Close Revit and run this again.',
             mbError, MB_OK);

      Result := False;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if Exec(ExpandConstant('{cmd}'),
          '/C tasklist /FI "IMAGENAME eq Revit.exe" | find /I "Revit.exe"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      MsgBox('Close Revit first, then uninstall.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

// Settings are the user's own work - the ACC model list and the duct
// insulation rules - so they are kept unless asked for
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Settings: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Settings := ExpandConstant('{userappdata}\ATKINSMEC');

    if DirExists(Settings) then
    begin
      if MsgBox('Remove your ATKINSMEC settings as well?' + #13#10#13#10 +
                'That is the saved ACC model list and the duct ' +
                'insulation rules, in' + #13#10 + Settings + #13#10#13#10 +
                'Choose No to keep them for a later reinstall.',
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(Settings, True, True, True);
      end;
    end;
  end;
end;
