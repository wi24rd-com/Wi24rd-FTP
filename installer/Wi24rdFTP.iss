; Wi24rd FTP – Inno Setup 6 script
; Byg først med ..\build.ps1 (laver ..\publish\Wi24rdFTP.exe + Scripts\)

#define AppName      "Wi24rd FTP"
#ifndef AppVersion
#define AppVersion   "1.0.0"
#endif
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
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl"

[CustomMessages]
english.InstallingBackend=Setting up Windows' built-in FTP server and OpenSSH – follow the progress in the console window (can take 5-10 minutes)…
danish.InstallingBackend=Sætter Windows' indbyggede FTP-server og OpenSSH op – følg med i konsolvinduet (kan tage 5-10 minutter)…

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Sikrer IIS FTP-feature, FTP-site, firewall-regler, mapper og konto.
; Kører eleveret (installeren er admin). Kan tage et par minutter første gang.
; Kører i et synligt konsolvindue, så brugeren kan følge med (IIS-features + OpenSSH kan tage 5-10 min).
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Scripts\Wi24rdFtp.ps1"" -Action Install"; \
  StatusMsg: "{cm:InstallingBackend}"; \
  Flags: waituntilterminated

; runasoriginaluser: ellers arver programmet installerens admin-token, og drag-and-drop fra Stifinder blokeres (UIPI)
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

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
