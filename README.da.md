![Wi24rd FTP](docs/banner.jpg)

# Wi24rd FTP

[English](README.md) · **Dansk**

[![CI](https://github.com/wi24rd-com/Wi24rd-FTP/actions/workflows/ci.yml/badge.svg)](https://github.com/wi24rd-com/Wi24rd-FTP/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/wi24rd-com/Wi24rd-FTP?label=download)](https://github.com/wi24rd-com/Wi24rd-FTP/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Super enkel, midlertidig fildeling oven på Windows 11's indbyggede **IIS FTP-server** (ftpsvc) og **OpenSSH** (sshd).
Træk filer ind → Start → giv modtageren adresse + brugernavn + engangsadgangskode → Stop. Færdig.

**Download:** [Wi24rdFTP-Setup.exe (nyeste version)](https://github.com/wi24rd-com/Wi24rd-FTP/releases/latest/download/Wi24rdFTP-Setup.exe) · Windows 10/11 64-bit · ingen .NET-installation nødvendig.

> Installeren er endnu ikke kodesigneret, så SmartScreen kan vise "Windows beskyttede din pc" første gang. Klik *Flere oplysninger → Kør alligevel*, eller verificér SHA256 mod `SHA256SUMS.txt` på release-siden.

Sproget følger Windows' visningssprog: dansk på dansk Windows, ellers engelsk.

**Gratis – og det bliver det ved med at være**

- ✅ Gratis og open source (MIT) – ingen betalingsversion, ingen prøveperiode
- ✅ Ingen reklamer, ingen konto, ingen registrering
- ✅ Ingen tracking eller telemetri – den eneste forbindelse programmet selv laver, er ét anonymt kald til GitHub ved opstart for at tjekke efter en nyere version
- ✅ Alt kører lokalt på din egen pc – dine filer går aldrig gennem en tredjepart

![Wi24rd FTP hovedvindue](docs/screenshot.png)

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

## Installér

Kør `dist\Wi24rdFTP-Setup-1.0.0.exe`. Installeren:

1. Lægger programmet i `C:\Program Files\Wi24rd FTP`
2. Kører `Wi24rdFtp.ps1 -Action Install` som aktiverer Windows-features `IIS-WebServerRole`, `IIS-FTPServer`, `IIS-FTPSvc`, `IIS-ManagementScriptingTools`, opretter FTP-sitet `Wi24rdFTP`, de (deaktiverede) firewall-regler, mapperne og den (deaktiverede) konto.

Afinstallation stopper alt og fjerner site, regler, konto og sshd-konfiguration. `C:\Wi24rdFTP` bevares.

## Hvor ligger tingene

* Delte filer: `C:\Wi24rdFTP\Share` (linket **Åbn mappe** i programmet)
* Log, hvis noget fejler: `%ProgramData%\Wi24rdFTP\log\`

## Sikkerhedsnoter

* **FTP er ukrypteret** – adgangskode og filer går i klartekst. Brug **SFTP** når modtageren kan (de fleste klienter: FileZilla, WinSCP, `sftp`). SFTP-mode installerer OpenSSH Server (Windows-capability, kræver internet første gang).
* Adgangskoden lever kun fra Start til Stop. Kontoen er deaktiveret og skjult på login-skærmen når serveren ikke kører.
* Udefra-adgang kræver stadig port-forward på routeren/firewallen til maskinens IP – det kan programmet ikke gøre for dig.
* Appen tjekker GitHub Releases ved opstart (ét anonymt kald, ingen telemetri) og viser et banner, hvis der findes en nyere version.
* Passivt FTP-portområde er 50000-50100 (sat i IIS *FTP Firewall Support*). Skal evt. også åbnes på router/forti.

## Udviklernoter

Projektstruktur, byggevejledning og udgivelsesproces står i [DEVELOPMENT.md](DEVELOPMENT.md) (engelsk).

## Licens

[MIT](LICENSE) · © 2026 Wi24rd
