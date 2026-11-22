# Security

## Reporting a vulnerability

Please report it privately: open the **Security** tab of this repository and
choose **Report a vulnerability**. Do not open a public issue for it.

Say what you found, how to reproduce it and what an attacker gains. Leave out
real passwords, addresses and file names; describe them instead.

Wi24rd FTP is maintained by one person in spare time. Reports are read, but
there is no promised response time.

## Supported versions

The newest release. Older versions get no fixes; the app shows a banner when a
newer one exists.

## What Wi24rd FTP does on your machine

It is a control panel for two servers that are part of Windows: the IIS FTP
service and OpenSSH. It does not contain a server of its own.

- The window runs without administrator rights. What needs them (services,
  firewall rules, the account, Windows features) is done by one PowerShell
  script, `Scripts\Wi24rdFtp.ps1`, which Windows asks you to approve at Start
  and Stop.
- **Start** makes a new one-time password for the local account `wi24rdftp`,
  enables the account, starts the service and enables the firewall rule.
- **Stop** disables the firewall rules, stops the service, disables the account
  and gives it a new password that nobody knows.
- The recipient can read the shared folder and nothing else: read-only in IIS,
  and a deny on write and delete in the file system. In SFTP mode the account
  is confined to that folder.
- The one request the program makes by itself goes to GitHub at startup, to see
  whether a newer version exists. It carries no information about you or your files.

## What it does not protect

- **FTP is not encrypted.** The password and the files can be read by anyone on
  the network path. Use SFTP whenever the recipient can.
- The password is shown in the window while the server runs, and is kept in
  `%ProgramData%\Wi24rdFTP\state.json` for that time.
- The installer is not code-signed yet. Compare its SHA-256 with
  `SHA256SUMS.txt` on the release page before you run it.
- Opening the server to the internet takes a port forward on your router. That
  is your decision, and the program cannot see or limit who connects.
