# Changelog

What each version of Wi24rd FTP holds.

## Unreleased

- A downloads badge in the README, counted by Wi24rd Mimir.
- Security policy, contributing guide, support file and issue forms.
- The workflows use actions locked to a commit, ask for the permissions they
  need and nothing more, and have time limits. The release is made as a draft
  and published when all three files are attached.

## 1.0.1 (2026-10-30)

- The installer starts the program as the user who ran it, not as administrator.
- The check for an elevated window uses the token's elevation type.
- The console is visible while setup runs.

## 1.0.0 (2026-10-30)

The first release: temporary file sharing on the FTP and SFTP servers that are
part of Windows, with a one-time password, read-only access for the recipient,
an installer, English and Danish, and a check for newer versions.
