<#
.SYNOPSIS
    Bygger XDRagon FTP som én selvstændig .exe (ingen .NET-runtime påkrævet hos brugeren)
    og – hvis Inno Setup 6 er installeret – en Setup.exe i .\dist.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -SkipInstaller
#>
[CmdletBinding()]
param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj    = Join-Path $here 'src\XDRagonFTP\XDRagonFTP.csproj'
$publish = Join-Path $here 'publish'
$dist    = Join-Path $here 'dist'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK 8 mangler. Hent den fra https://dotnet.microsoft.com/download/dotnet/8.0 (SDK, ikke kun runtime).'
}

Write-Host '==> dotnet publish (win-x64, self-contained, single file)' -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -o $publish

if ($LASTEXITCODE -ne 0) { throw 'dotnet publish fejlede.' }

if (-not (Test-Path (Join-Path $publish 'Scripts\XDRagonFtp.ps1'))) {
    throw 'Scripts\XDRagonFtp.ps1 blev ikke kopieret med til publish-mappen.'
}
Write-Host "    OK -> $publish\XDRagonFTP.exe" -ForegroundColor Green

if ($SkipInstaller) { return }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning 'Inno Setup 6 ikke fundet – springer installer over. Hent: https://jrsoftware.org/isdl.php (eller: winget install JRSoftware.InnoSetup)'
    return
}

Write-Host '==> Inno Setup' -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $dist | Out-Null
& $iscc (Join-Path $here 'installer\XDRagonFTP.iss')
if ($LASTEXITCODE -ne 0) { throw 'ISCC fejlede.' }

Write-Host "    OK -> $(Get-ChildItem $dist -Filter *.exe | Select-Object -ExpandProperty FullName)" -ForegroundColor Green
