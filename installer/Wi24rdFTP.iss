; Wi24rd FTP – Inno Setup 6 script
; Byg først med ..\build.ps1 (laver ..\publish\Wi24rdFTP.exe + Scripts\)

#define AppName      "Wi24rd FTP"
#define AppVersion   "1.0.0"
#define AppPublisher "Wi24rd"
#define AppExe       "Wi24rdFTP.exe"

[Setup]
AppId={{A00276B2-A46C-40FE-8ECF-A9D28D4558A5}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Wi24rd FTP
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Wi24rdFTP-Setup-{#AppVersion}
SetupIconFile=..\src\Wi24rdFTP\Assets\wi24rd-ftp.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#AppName}
MinVersion=10.0.19041

[Languages]
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl"

[Tasks]
Name: "desktopicon"; Description: "Opret genvej på skrivebordet"; GroupDescription: "Genveje:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Sikrer IIS FTP-feature, FTP-site, firewall-regler, mapper og konto.
; Kører eleveret (installeren er admin). Kan tage et par minutter første gang.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Scripts\Wi24rdFtp.ps1"" -Action Install"; \
  StatusMsg: "Installerer Windows' indbyggede FTP-server (kan tage et par minutter)…"; \
  Flags: runhidden waituntilterminated

Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stopper server, lukker porte, fjerner site, regler og konto. Mappen C:\Wi24rdFTP bevares.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Scripts\Wi24rdFtp.ps1"" -Action Uninstall"; \
  Flags: runhidden waituntilterminated; RunOnceId: "Wi24rdFtpUninstall"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsWin64 then
  begin
    MsgBox('Wi24rd FTP kræver 64-bit Windows 10/11.', mbError, MB_OK);
    Result := False;
  end;
end;
