# Fase A-proeve: bygger og koerer tokens-proeven paa Windows og gemmer resultatet i resultat.txt.
# Koer:  powershell -ExecutionPolicy Bypass -File .\koer.ps1
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

$forventet = ((Get-Content "$here\Tokens\KIT-VERSION.txt") -match '^sha256 ') -replace '^sha256 ', ''
$faktisk = (Get-FileHash "$here\Tokens\Tokens.xaml" -Algorithm SHA256).Hash.ToLower()
$linjer = @()
if ($faktisk -eq $forventet) { $linjer += "OK    00 Kopien af Tokens.xaml er byte for byte som pakken 2.6.0 (sha256 $faktisk)" }
else { $linjer += "FEJL  00 Kopien af Tokens.xaml afviger fra pakken. Ventet $forventet, fandt $faktisk (har git aendret linjeskift?)" }
$linjer | ForEach-Object { Write-Host $_ }

$ErrorActionPreference = 'Continue'
$ud = & dotnet run -c Release --project "$here\Proeve.csproj" 2>&1 | ForEach-Object { "$_" }
$kode = $LASTEXITCODE
$ud | ForEach-Object { Write-Host $_ }
($linjer + $ud + "dotnet afsluttede med kode $kode") | Set-Content -Path "$here\resultat.txt" -Encoding UTF8
Write-Host ""
Write-Host "Gemt i $here\resultat.txt"
exit $kode
