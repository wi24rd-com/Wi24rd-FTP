# Development notes

## Project layout

```
build.ps1                     Builds exe + installer
installer\Wi24rdFTP.iss       Inno Setup script
.github\workflows\            CI (build on push) and Release (build + publish on tag)
src\Wi24rdFTP\
  Wi24rdFTP.csproj            .NET 8 WPF
  app.manifest                asInvoker + PerMonitorV2 DPI
  App.xaml                    Colours and styles
  MainWindow.xaml(.cs)        Main window
  AboutWindow.xaml(.cs)       About dialog (version, licence, links)
  L.cs + Resources\*.resx     Localisation (en neutral, da)
  Services\Backend.cs         Runs PowerShell elevated, reads state.json
  Services\UpdateChecker.cs   GitHub Releases update check
  Services\SharedFile.cs      File-list model
  Scripts\Wi24rdFtp.ps1       Backend: Install / Start / Stop / Uninstall
```

## Build

Requirements on the build machine: Windows, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), optionally [Inno Setup 6](https://jrsoftware.org/isdl.php).

```powershell
.\build.ps1               # -> publish\Wi24rdFTP.exe  and  dist\Wi24rdFTP-Setup-<version>.exe
.\build.ps1 -SkipInstaller
.\build.ps1 -Version 1.2.3  # override the version in csproj/iss
```

The result is a single self-contained `.exe` – users need no .NET runtime. `build.ps1` stops a running Wi24rd FTP before publishing.

Run the GUI from Explorer (double-click), not from an elevated PowerShell: Windows blocks drag-and-drop into elevated windows, and the app shows a warning banner if it detects that.

## CI

Every push to `main` runs `.github/workflows/ci.yml`: a full publish on a Windows runner plus a PowerShell 5.1 syntax check of `Scripts\Wi24rdFtp.ps1`.

## Releasing a new version

```bash
git tag v1.0.1
git push --tags
```

`.github/workflows/release.yml` builds the exe + installer with the version taken from the tag, creates a GitHub Release with auto-generated notes and attaches:

* `Wi24rdFTP-Setup-1.0.1.exe` – the versioned installer
* `Wi24rdFTP-Setup.exe` – same file under a stable name, so `…/releases/latest/download/Wi24rdFTP-Setup.exe` always points at the newest version
* `SHA256SUMS.txt`

The app's update check compares its own version against the latest release tag.

## Code signing

Not set up yet. When a certificate is available (Azure Trusted Signing or an OV/EV certificate), signing is added as a step in `release.yml` before the installer is built; nothing in the app needs to change.
