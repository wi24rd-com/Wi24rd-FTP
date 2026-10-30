# Wi24rd FTP

[![CI](https://github.com/wi24rd-com/Wi24rd-FTP/actions/workflows/ci.yml/badge.svg)](https://github.com/wi24rd-com/Wi24rd-FTP/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/wi24rd-com/Wi24rd-FTP?label=download)](https://github.com/wi24rd-com/Wi24rd-FTP/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Super enkel, midlertidig fildeling oven på Windows 11's indbyggede **IIS FTP-server** (ftpsvc) og **OpenSSH** (sshd).
Træk filer ind → Start → giv modtageren adresse + brugernavn + engangsadgangskode → Stop. Færdig.

**Download:** [Wi24rdFTP-Setup.exe (nyeste version)](https://github.com/wi24rd-com/Wi24rd-FTP/releases/latest/download/Wi24rdFTP-Setup.exe) · Windows 10/11 64-bit · ingen .NET-installation nødvendig.

> Installeren er endnu ikke kodesigneret, så SmartScreen kan vise "Windows beskyttede din pc" første gang. Klik *Flere oplysninger → Kør alligevel*, eller verificér SHA256 mod `SHA256SUMS.txt` på release-siden.

Sproget følger Windows' visningssprog: dansk på dansk Windows, ellers engelsk.

## Sådan virker det

| Knap | Hvad sker der |
|---|---|
| **Start FTP / SFTP** | Ny engangsadgangskode til kontoen `wi24rdftp` · kontoen aktiveres · ftpsvc (eller sshd) startes · firewall-regel for port 21 (+ passiv 50000-50100) eller port 22 aktiveres · adresse og adgangskode vises i GUI'en |
| **Stop FTP / SFTP** | Firewall-regler deaktiveres · tjenesten stoppes · kontoen deaktiveres og får ny, ukendt adgangskode · bruger spørges: *slet filerne eller lad dem blive?* |
| **Luk program** | Kører Stop-rutinen (hvis noget kører) → spørger om filerne → lukker |

* Filer kopieres til `C:\Wi24rdFTP\Share` – originalerne røres aldrig.
* Modtageren har **kun læseadgang** (IIS-autorisation = Read, og NTFS-deny på skriv/slet for `wi24rdftp`).
* Alt der kræver admin (tjenester, firewall, konto, IIS-features) ligger i `Scripts\Wi24rdFtp.ps1`, som GUI'en kører eleveret (UAC-prompt ved Start/Stop).
  GUI'en selv kører **uden** admin – det er bevidst: Windows blokerer drag-and-drop fra Explorer ind i elevede vinduer.
* Første gang: hvis ftpsvc mangler, viser GUI'en et gult banner med **Installér nu** (installeren gør det samme automatisk).

## Byg

Krav på byggemaskinen: Windows, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), valgfrit [Inno Setup 6](https://jrsoftware.org/isdl.php).

```powershell
.\build.ps1               # -> publish\Wi24rdFTP.exe  og  dist\Wi24rdFTP-Setup-1.0.0.exe
.\build.ps1 -SkipInstaller
```

Resultatet er én selvstændig `.exe` (self-contained) – brugeren behøver ingen .NET-runtime.

## Installér

Kør `dist\Wi24rdFTP-Setup-1.0.0.exe`. Installeren:

1. Lægger programmet i `C:\Program Files\Wi24rd FTP`
2. Kører `Wi24rdFtp.ps1 -Action Install` som aktiverer Windows-features `IIS-WebServerRole`, `IIS-FTPServer`, `IIS-FTPSvc`, `IIS-ManagementScriptingTools`, opretter FTP-sitet `Wi24rdFTP`, de (deaktiverede) firewall-regler, mapperne og den (deaktiverede) konto.

Afinstallation stopper alt og fjerner site, regler, konto og sshd-konfiguration. `C:\Wi24rdFTP` bevares.

## Mapper og filer

| Sti | Indhold |
|---|---|
| `C:\Wi24rdFTP\Share` | Filer der deles (= FTP-rod / SFTP-chroot ser `/Share`) |
| `%ProgramData%\Wi24rdFTP\state.json` | Backendens resultat (status, IP'er, adgangskode mens serveren kører) |
| `%ProgramData%\Wi24rdFTP\log\backend-ÅÅÅÅMMDD.log` | Transcript af hvert backend-kald – kig her ved fejl |
| `%ProgramData%\ssh\sshd_config` | Får en `# WI24RD-FTP BEGIN … END`-blok (Match User wi24rdftp, ChrootDirectory, internal-sftp) i SFTP-mode |

## Udgiv en ny version

```bash
git tag v1.0.1
git push --tags
```

GitHub Actions bygger exe + installer, laver en Release med auto-genererede noter og lægger `Wi24rdFTP-Setup-1.0.1.exe`, `Wi24rdFTP-Setup.exe` og `SHA256SUMS.txt` som assets. Appen tjekker GitHub Releases ved opstart (ét anonymt kald, ingen telemetri) og viser et banner, hvis der findes en nyere version.

## Sikkerhedsnoter

* **FTP er ukrypteret** – adgangskode og filer går i klartekst. Brug **SFTP** når modtageren kan (de fleste klienter: FileZilla, WinSCP, `sftp`). SFTP-mode installerer OpenSSH Server (Windows-capability, kræver internet første gang).
* Adgangskoden lever kun fra Start til Stop. Kontoen er deaktiveret og skjult på login-skærmen når serveren ikke kører.
* Udefra-adgang kræver stadig port-forward på routeren/firewallen til maskinens IP – det kan programmet ikke gøre for dig.
* Passivt FTP-portområde er 50000-50100 (sat i IIS *FTP Firewall Support*). Skal evt. også åbnes på router/forti.

## Projektstruktur

```
build.ps1                     Bygger exe + installer
installer\Wi24rdFTP.iss      Inno Setup-script
src\Wi24rdFTP\
  Wi24rdFTP.csproj           .NET 8 WPF
  app.manifest                asInvoker + PerMonitorV2 DPI
  App.xaml                    Farver og styles (Veeam-stil)
  MainWindow.xaml(.cs)        GUI
  Services\Backend.cs         Kører PowerShell eleveret, læser state.json
  Services\SharedFile.cs      Model for fillisten
  Scripts\Wi24rdFtp.ps1      Backend: Install / Start / Stop / Uninstall
```
