<#
.SYNOPSIS
    Wi24rd FTP – backend.
    Styrer den indbyggede IIS FTP-server (ftpsvc) og OpenSSH (sshd),
    Windows Firewall og den lokale FTP-konto.

.DESCRIPTION
    Kaldes eleveret (UAC) fra Wi24rd FTP-GUI'en eller fra installeren.
    Resultatet skrives altid til %ProgramData%\Wi24rdFTP\state.json,
    som GUI'en læser bagefter (stdout kan ikke læses fra en eleveret proces).

    Actions:
      Install   - Sikrer IIS FTP-feature, FTP-site, firewall-regler, mapper og konto.
      Start     - Genererer ny adgangskode, starter server, åbner port (21 eller 22).
      Stop      - Lukker port, stopper server, deaktiverer kontoen.
      Uninstall - Fjerner site, regler, konto og sshd-konfiguration.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Install', 'Start', 'Stop', 'Uninstall')]
    [string]$Action,

    [ValidateSet('FTP', 'SFTP')]
    [string]$Mode = 'FTP',

    # Adgangskode valgt i GUI'en (kun A-Z a-z 2-9). Tom = generér selv.
    [ValidatePattern('^[A-Za-z2-9]{0,64}$')]
    [string]$Password = ''
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Konstanter
# ---------------------------------------------------------------------------
$Root       = 'C:\Wi24rdFTP'                      # chroot-rod for SFTP – må IKKE være skrivbar for andre
$Share      = Join-Path $Root 'Share'              # her ligger filerne der deles
$DataDir    = Join-Path $env:ProgramData 'Wi24rdFTP'
$LogDir     = Join-Path $DataDir 'log'
$StateFile  = Join-Path $DataDir 'state.json'
$SiteName   = 'Wi24rdFTP'
$FtpUser    = 'wi24rdftp'
$PasvLow    = 50000
$PasvHigh   = 50100
$RuleFtp    = 'Wi24rd FTP - kontrolkanal (21)'
$RulePasv   = 'Wi24rd FTP - passiv datakanal'
$RuleSftp   = 'Wi24rd FTP - SFTP (22)'
$RuleGroup  = 'Wi24rd FTP'
$SshdConfig = Join-Path $env:ProgramData 'ssh\sshd_config'
$Marker     = '# WI24RD-FTP'

New-Item -ItemType Directory -Force -Path $DataDir, $LogDir | Out-Null
Start-Transcript -Path (Join-Path $LogDir ("backend-{0:yyyyMMdd}.log" -f (Get-Date))) -Append | Out-Null

# ---------------------------------------------------------------------------
# Hjælpefunktioner
# ---------------------------------------------------------------------------
function Write-Log([string]$Message) {
    Write-Host ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $Message)
}

function Get-LocalIPv4 {
    try {
        Get-NetIPAddress -AddressFamily IPv4 -ErrorAction Stop |
            Where-Object {
                $_.IPAddress -notlike '127.*' -and
                $_.IPAddress -notlike '169.254.*' -and
                $_.PrefixOrigin -ne 'WellKnown'
            } |
            ForEach-Object {
                $if = Get-NetAdapter -InterfaceIndex $_.InterfaceIndex -ErrorAction SilentlyContinue
                if ($if -and $if.Status -eq 'Up') { $_.IPAddress }
            } | Sort-Object -Unique
    } catch { @() }
}

function Write-State([hashtable]$Extra = @{}) {
    $ftpSvc = Get-Service -Name ftpsvc -ErrorAction SilentlyContinue
    $sshSvc = Get-Service -Name sshd   -ErrorAction SilentlyContinue
    $state = [ordered]@{
        timestamp     = (Get-Date).ToString('o')
        action        = $Action
        mode          = $Mode
        share         = $Share
        user          = $FtpUser
        ips           = @(Get-LocalIPv4)
        ftpInstalled  = [bool]$ftpSvc
        sftpInstalled = [bool]$sshSvc
        running       = $false
        port          = 0
        password      = ''
        lastError     = ''
    }
    foreach ($k in $Extra.Keys) { $state[$k] = $Extra[$k] }
    $state | ConvertTo-Json -Depth 4 | Set-Content -Path $StateFile -Encoding UTF8
}

function New-RandomPassword {
    # Ingen tvetydige tegn (0/O, 1/l/I) – koden skal kunne læses op i telefonen
    $chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789'
    $bytes = New-Object byte[] 16
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
}

function Ensure-FtpUser([string]$Pw) {
    $sec = ConvertTo-SecureString $Pw -AsPlainText -Force
    $u = Get-LocalUser -Name $FtpUser -ErrorAction SilentlyContinue
    if (-not $u) {
        Write-Log "Opretter lokal konto $FtpUser"
        New-LocalUser -Name $FtpUser -Password $sec -FullName 'Wi24rd FTP' `
            -Description 'Midlertidig konto oprettet af Wi24rd FTP' `
            -PasswordNeverExpires -AccountNeverExpires -UserMayNotChangePassword | Out-Null
    } else {
        Set-LocalUser -Name $FtpUser -Password $sec -PasswordNeverExpires $true
    }
    # Skal være medlem af Users for at kunne logge på (sprog-uafhængigt via SID)
    $usersGroup = Get-LocalGroup -SID 'S-1-5-32-545'
    if (-not (Get-LocalGroupMember -Group $usersGroup -Member $FtpUser -ErrorAction SilentlyContinue)) {
        Add-LocalGroupMember -Group $usersGroup -Member $FtpUser
    }
    # Skjul kontoen på Windows' login-skærm
    $key = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\SpecialAccounts\UserList'
    New-Item -Path $key -Force | Out-Null
    New-ItemProperty -Path $key -Name $FtpUser -Value 0 -PropertyType DWord -Force | Out-Null
}

function Ensure-Folders {
    Write-Log 'Sikrer mapper og rettigheder'
    New-Item -ItemType Directory -Force -Path $Root, $Share, $DataDir, $LogDir | Out-Null

    # Rod: kun SYSTEM/Administrators må skrive (krav for sshd ChrootDirectory)
    & icacls $Root /inheritance:r /grant:r `
        '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' | Out-Null

    # Share: alle lokale brugere må lægge filer ind (GUI'en kører uden admin),
    # men FTP-kontoen må udtrykkeligt IKKE skrive/slette (read-only udefra).
    & icacls $Share /inheritance:r /grant:r `
        '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-11:(OI)(CI)M' | Out-Null
    & icacls $Share /deny "${FtpUser}:(OI)(CI)(DE,WD,AD,WEA,WA,DC,WDAC,WO)" | Out-Null

    # ProgramData: GUI'en skal kunne læse state.json
    & icacls $DataDir /inheritance:r /grant:r `
        '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-11:(OI)(CI)M' | Out-Null
}

function Ensure-FirewallRule([string]$Name, [string]$Port) {
    if (-not (Get-NetFirewallRule -DisplayName $Name -ErrorAction SilentlyContinue)) {
        Write-Log "Opretter firewall-regel '$Name' (TCP $Port) – deaktiveret"
        New-NetFirewallRule -DisplayName $Name -Group $RuleGroup -Direction Inbound `
            -Protocol TCP -LocalPort $Port -Action Allow -Profile Any -Enabled False | Out-Null
    }
}

function Ensure-FirewallRules {
    Ensure-FirewallRule $RuleFtp  '21'
    Ensure-FirewallRule $RulePasv "$PasvLow-$PasvHigh"
    Ensure-FirewallRule $RuleSftp '22'
    & netsh advfirewall set global StatefulFtp enable | Out-Null
}

function Ensure-FtpFeatures {
    $features = 'IIS-WebServerRole', 'IIS-FTPServer', 'IIS-FTPSvc', 'IIS-ManagementScriptingTools'
    $newly = $false
    foreach ($f in $features) {
        $state = (Get-WindowsOptionalFeature -Online -FeatureName $f -ErrorAction SilentlyContinue).State
        if ($state -ne 'Enabled') {
            Write-Log "Aktiverer Windows-feature $f"
            Enable-WindowsOptionalFeature -Online -FeatureName $f -All -NoRestart | Out-Null
            $newly = $true
        }
    }
    if ($newly) {
        # IIS' webserver (port 80) skal ikke stå åben bare fordi vi vil have FTP
        $w3 = Get-Service -Name W3SVC -ErrorAction SilentlyContinue
        if ($w3) { Stop-Service W3SVC -Force -ErrorAction SilentlyContinue; Set-Service W3SVC -StartupType Manual }
    }
    Set-Service -Name ftpsvc -StartupType Manual
}

function Ensure-FtpSite {
    Import-Module WebAdministration -ErrorAction Stop
    if (-not (Test-Path "IIS:\Sites\$SiteName")) {
        Write-Log "Opretter FTP-site $SiteName"
        New-WebFtpSite -Name $SiteName -Port 21 -PhysicalPath $Share -IPAddress '*' -Force | Out-Null
    }
    $p = "IIS:\Sites\$SiteName"
    Set-ItemProperty $p -Name physicalPath -Value $Share
    Set-ItemProperty $p -Name ftpServer.serverAutoStart -Value $false
    Set-ItemProperty $p -Name ftpServer.security.authentication.basicAuthentication.enabled     -Value $true
    Set-ItemProperty $p -Name ftpServer.security.authentication.anonymousAuthentication.enabled -Value $false
    Set-ItemProperty $p -Name ftpServer.security.ssl.controlChannelPolicy -Value 0   # SslAllow
    Set-ItemProperty $p -Name ftpServer.security.ssl.dataChannelPolicy    -Value 0
    Set-ItemProperty $p -Name ftpServer.userIsolation.mode -Value 0                  # None (alle ser Share)

    # Autorisation: kun wi24rdftp, kun læsning
    try { Clear-WebConfiguration -Filter '/system.ftpServer/security/authorization' -PSPath 'IIS:\' -Location $SiteName } catch { }
    Add-WebConfiguration -Filter '/system.ftpServer/security/authorization' -PSPath 'IIS:\' -Location $SiteName `
        -Value @{ accessType = 'Allow'; users = $FtpUser; permissions = 'Read' }

    # Passiv port-område (server-niveau) så firewall-reglen passer
    Set-WebConfigurationProperty -PSPath 'IIS:\' -Filter 'system.ftpServer/firewallSupport' -Name lowDataChannelPort  -Value $PasvLow
    Set-WebConfigurationProperty -PSPath 'IIS:\' -Filter 'system.ftpServer/firewallSupport' -Name highDataChannelPort -Value $PasvHigh
}

function Ensure-OpenSsh {
    $cap = Get-WindowsCapability -Online | Where-Object Name -like 'OpenSSH.Server*' | Select-Object -First 1
    if (-not $cap) { throw 'OpenSSH Server-capability findes ikke på denne Windows-udgave.' }
    if ($cap.State -ne 'Installed') {
        Write-Log "Installerer $($cap.Name) (kræver internet)"
        Add-WindowsCapability -Online -Name $cap.Name | Out-Null
    }
    Set-Service -Name sshd -StartupType Manual
}

function Set-SshdChroot {
    $block = @"
$Marker BEGIN
Match User $FtpUser
    ChrootDirectory $Root
    ForceCommand internal-sftp
    PasswordAuthentication yes
    AllowTcpForwarding no
    X11Forwarding no
$Marker END
"@
    $content = if (Test-Path $SshdConfig) { Get-Content $SshdConfig -Raw } else { '' }
    $pattern = "(?s)\r?\n?$([regex]::Escape($Marker)) BEGIN.*?$([regex]::Escape($Marker)) END\r?\n?"
    $content = [regex]::Replace($content, $pattern, '')
    # Match-blokke skal stå sidst i sshd_config
    Set-Content -Path $SshdConfig -Value ($content.TrimEnd() + "`r`n`r`n" + $block) -Encoding ASCII
}

function Remove-SshdChroot {
    if (-not (Test-Path $SshdConfig)) { return }
    $content = Get-Content $SshdConfig -Raw
    $pattern = "(?s)\r?\n?$([regex]::Escape($Marker)) BEGIN.*?$([regex]::Escape($Marker)) END\r?\n?"
    Set-Content -Path $SshdConfig -Value ([regex]::Replace($content, $pattern, '').TrimEnd() + "`r`n") -Encoding ASCII
}

# ---------------------------------------------------------------------------
# Actions
# ---------------------------------------------------------------------------
function Invoke-Install {
    Ensure-FtpUser (New-RandomPassword)
    Disable-LocalUser -Name $FtpUser
    Ensure-Folders
    Ensure-FtpFeatures
    Ensure-FtpSite
    Ensure-FirewallRules
    Stop-Service ftpsvc -Force -ErrorAction SilentlyContinue
    Write-Log 'Installation færdig'
    Write-State
}

function Invoke-Start {
    Ensure-Folders
    $pw = if ($Password) { $Password } else { New-RandomPassword }
    Ensure-FtpUser $pw
    Enable-LocalUser -Name $FtpUser
    Ensure-FirewallRules

    if ($Mode -eq 'FTP') {
        Ensure-FtpFeatures
        Ensure-FtpSite
        Import-Module WebAdministration
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name ftpServer.serverAutoStart -Value $true
        Write-Log 'Starter ftpsvc'
        Restart-Service ftpsvc -Force
        Enable-NetFirewallRule -DisplayName $RuleFtp, $RulePasv
        $port = 21
    } else {
        Ensure-OpenSsh
        Set-SshdChroot
        Write-Log 'Starter sshd'
        Restart-Service sshd -Force
        Enable-NetFirewallRule -DisplayName $RuleSftp
        $port = 22
    }
    Write-Log "Kører – port $port åben"
    Write-State @{ running = $true; port = $port; password = $pw }
}

function Invoke-Stop {
    Write-Log 'Lukker porte'
    Disable-NetFirewallRule -DisplayName $RuleFtp, $RulePasv, $RuleSftp -ErrorAction SilentlyContinue

    if (Get-Service ftpsvc -ErrorAction SilentlyContinue) {
        try {
            Import-Module WebAdministration -ErrorAction Stop
            if (Test-Path "IIS:\Sites\$SiteName") {
                Set-ItemProperty "IIS:\Sites\$SiteName" -Name ftpServer.serverAutoStart -Value $false
            }
        } catch { }
        Stop-Service ftpsvc -Force -ErrorAction SilentlyContinue
    }
    if (Get-Service sshd -ErrorAction SilentlyContinue) {
        Stop-Service sshd -Force -ErrorAction SilentlyContinue
    }
    # Kontoen lukkes og får ny, ukendt adgangskode
    if (Get-LocalUser -Name $FtpUser -ErrorAction SilentlyContinue) {
        Ensure-FtpUser (New-RandomPassword)
        Disable-LocalUser -Name $FtpUser
    }
    Write-Log 'Stoppet'
    Write-State @{ running = $false }
}

function Invoke-Uninstall {
    Invoke-Stop
    try {
        Import-Module WebAdministration -ErrorAction Stop
        if (Test-Path "IIS:\Sites\$SiteName") { Remove-Item "IIS:\Sites\$SiteName" -Recurse -Force }
    } catch { }
    Get-NetFirewallRule -Group $RuleGroup -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Remove-SshdChroot
    Remove-LocalUser -Name $FtpUser -ErrorAction SilentlyContinue
    Remove-Item -Path $StateFile -Force -ErrorAction SilentlyContinue
    Write-Log 'Afinstalleret (mappen C:\Wi24rdFTP er bevaret)'
}

# ---------------------------------------------------------------------------
try {
    Write-Log "=== $Action ($Mode) ==="
    switch ($Action) {
        'Install'   { Invoke-Install }
        'Start'     { Invoke-Start }
        'Stop'      { Invoke-Stop }
        'Uninstall' { Invoke-Uninstall }
    }
    $exit = 0
} catch {
    $msg = $_.Exception.Message
    Write-Log "FEJL: $msg"
    Write-Log $_.ScriptStackTrace
    try { Write-State @{ lastError = $msg } } catch { }
    $exit = 1
} finally {
    Stop-Transcript | Out-Null
}
exit $exit
