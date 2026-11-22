# Contributing to Wi24rd FTP

Thank you for looking. Wi24rd FTP is maintained by one person in spare time, so
small, focused changes are the ones that get in.

## Before you start

- **A bug:** open an issue with the bug form. Leave out passwords, public
  addresses and the names of your files.
- **A security problem:** do not open an issue. Use **Report a vulnerability**
  under the Security tab; see [SECURITY.md](SECURITY.md).
- **A new feature:** open an issue first and say what problem it solves.

## Setting up

You need Windows, the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
and, for the installer, [Inno Setup 6](https://jrsoftware.org/isdl.php).

```powershell
.\build.ps1 -SkipInstaller    # builds publish\Wi24rdFTP.exe
```

[DEVELOPMENT.md](DEVELOPMENT.md) has the project layout, where the program keeps
its files, and how a release is made.

## What a change has to keep

- **The window never runs elevated.** Everything that needs administrator
  rights stays in `Scripts\Wi24rdFtp.ps1`.
- **Stop leaves nothing open.** After Stop, the firewall rules are disabled, the
  service is stopped and the account is disabled with an unknown password.
- **The recipient can only read.**
- **`Wi24rdFtp.ps1` runs in Windows PowerShell 5.1** and is saved as UTF-8 with
  a byte order mark. CI checks that it parses.
- **English and Danish.** A new text goes into both resource files.
- **No telemetry.** The one network request the program makes is the update check.

## A pull request

- One change per pull request.
- Say what you tried on a real Windows machine: FTP, SFTP, Start, Stop and
  uninstall, as far as the change touches them. If something could not be
  tried, say that.
- A change to the window comes with a screenshot.

## Licence

By contributing you agree that your contribution is licensed under MIT, like
the rest of Wi24rd FTP.
