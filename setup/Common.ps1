Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:OnWindows = $env:OS -eq 'Windows_NT'

function Read-Profile([string]$Path) {
    $p = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($key in @('AccountDatabase', 'GameDatabase', 'DatabaseUser')) {
        if ($p.$key -notmatch '^[a-z][a-z0-9_]{2,47}$') { throw "Invalid local identifier: $key" }
    }
    if ($p.AccountDatabase -eq $p.GameDatabase) { throw 'Account and game schemas must be different.' }
    if ($p.DatabaseHost -ne '127.0.0.1' -or $p.InternalAddress -ne '127.0.0.1') { throw 'Database and internal server link must stay on loopback.' }
    if ($p.ServerName -notmatch '^[A-Za-z0-9_-]{1,16}$') { throw 'ServerName must fit the legacy 16-byte field.' }
    foreach ($key in @('LoginPort', 'GamePort', 'InternalPort', 'DatabasePort')) {
        if ([int]$p.$key -lt 1 -or [int]$p.$key -gt 65535) { throw "Invalid port: $key" }
    }
    $ip = [System.Net.IPAddress]::Parse($p.GameAddress)
    if ($ip.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) { throw 'GameAddress must be IPv4.' }
    if ([int]$p.ClientVersion -ne 5517) { throw 'This bundle targets client 5517, not the old loader profile 5187.' }
    return $p
}
function Assert-CiMode([bool]$CI) {
    if ($CI -and $env:GITHUB_ACTIONS -ne 'true') { throw 'Disposable test mode is restricted to GitHub CI.' }
    if (!$CI -and !$script:OnWindows) { throw 'The local installation scripts require Windows; no unencrypted non-Windows secret store is created.' }
}
function ConvertTo-Plain([Security.SecureString]$Secure) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}
function New-RandomHex([int]$Bytes) {
    $b = New-Object byte[] $Bytes
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($b); return ([BitConverter]::ToString($b)).Replace('-', '').ToLowerInvariant() }
    finally { $rng.Dispose() }
}
function Write-Utf8([string]$Path, [string]$Text) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path))) | Out-Null
    [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding($false)))
}
function Find-Mysql([string]$Requested) {
    if ($Requested) {
        if (!(Test-Path -LiteralPath $Requested -PathType Leaf)) { throw 'Specified mysql executable was not found.' }
        return [IO.Path]::GetFullPath($Requested)
    }
    foreach ($name in @('mysql.exe','mariadb.exe','mysql','mariadb')) {
        $cmd = Get-Command $name -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    foreach ($drive in @('C','D','E','F','G')) {
        foreach ($suffix in @('xampp\mysql\bin\mysql.exe','Program Files\MariaDB 10.4\bin\mysql.exe')) {
            $candidate = "${drive}:\$suffix"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    throw 'mysql.exe was not found. Re-run Setup-RedMoon.ps1 -MysqlExe "full path to XAMPP mysql\bin\mysql.exe". HeidiSQL is not the command-line client.'
}
function Invoke-PrivateProcess([string]$Exe, [string]$Arguments, [string]$InputText, [hashtable]$Variables = @{}, [int]$TimeoutSeconds = 120) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $Exe; $info.Arguments = $Arguments
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    if ($info.PSObject.Properties.Name -contains 'StandardInputEncoding') {
        $info.StandardInputEncoding = New-Object Text.UTF8Encoding($false)
    } else {
        [Console]::InputEncoding = New-Object Text.UTF8Encoding($false)
    }
    $info.StandardOutputEncoding = New-Object Text.UTF8Encoding($false)
    $info.StandardErrorEncoding = New-Object Text.UTF8Encoding($false)
    foreach ($key in $Variables.Keys) { $info.EnvironmentVariables[$key] = [string]$Variables[$key] }
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    try {
        if (!$process.Start()) { throw 'Could not start local setup helper.' }
        $outTask = $process.StandardOutput.ReadToEndAsync(); $errTask = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write($InputText); $process.StandardInput.Close()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill() # only the exact child created by this function, never an existing service
            throw 'Local setup helper timed out. No existing MySQL service was stopped.'
        }
        $stdout = $outTask.GetAwaiter().GetResult(); $stderr = $errTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            $match = [regex]::Match($stderr, 'ERROR (\d+)')
            $code = if ($match.Success) { $match.Groups[1].Value } else { [string]$process.ExitCode }
            $reason = ''
            if ($code -in @('2002','2003','1045')) {
                $safeLine = @($stderr -split "`n" | Where-Object { $_ -match '^ERROR (2002|2003|1045)' } | Select-Object -First 1)
                if ($safeLine.Count -gt 0) { $reason = ' ' + $safeLine[0].Trim() }
            }
            throw "Local helper failed (code $code).$reason Raw SQL/credential-bearing errors are deliberately not printed."
        }
        return $stdout.Trim()
    } finally { $process.Dispose() }
}
function Invoke-Mysql([string]$Exe, $Profile, [string]$Password, [string]$Sql, [string]$Database = '') {
    $argsText = "--host=127.0.0.1 --port=$($Profile.DatabasePort) --protocol=TCP --user=root --batch --skip-column-names --default-character-set=utf8mb4"
    if ($Database) {
        if ($Database -notmatch '^[a-z][a-z0-9_]{2,47}$') { throw 'Unsafe database identifier.' }
        $argsText += " $Database"
    }
    return Invoke-PrivateProcess $Exe $argsText $Sql @{ MYSQL_PWD = $Password } 180
}
function Save-LocalSecrets([string]$Root, [hashtable]$Secrets, [bool]$CI) {
    $json = $Secrets | ConvertTo-Json -Compress
    if ($CI) { Write-Utf8 (Join-Path $Root '.local/ci-secrets.json') $json; return }
    $secure = ConvertTo-SecureString $json -AsPlainText -Force
    Write-Utf8 (Join-Path $Root '.local/runtime-secrets.dpapi') (ConvertFrom-SecureString $secure)
}
function Load-LocalSecrets([string]$Root, [bool]$CI) {
    if ($CI) { return (Get-Content -LiteralPath (Join-Path $Root '.local/ci-secrets.json') -Raw | ConvertFrom-Json) }
    $cipher = Get-Content -LiteralPath (Join-Path $Root '.local/runtime-secrets.dpapi') -Raw
    try { return (ConvertTo-Plain (ConvertTo-SecureString $cipher) | ConvertFrom-Json) }
    catch { throw 'The local secret vault could not be opened. Use the same Windows account that ran setup; do not upload the vault.' }
}
function Export-ServerSecrets($Secrets) {
    foreach ($name in @('REDMOON_DB_PASSWORD','REDMOON_AUTH_PASSWORD','REDMOON_TRANSFER_KEY','REDMOON_TRANSFER_SALT')) {
        [Environment]::SetEnvironmentVariable($name, [string]$Secrets.$name, 'Process')
    }
}
function Write-ServerConfiguration([string]$Root, $Profile) {
    $server = Join-Path $Root 'server'
    $common = @"
[MySQL]
Hostname=127.0.0.1
Port=$($Profile.DatabasePort)
Username=$($Profile.DatabaseUser)
Password=env:REDMOON_DB_PASSWORD
"@
    $crypto = @"
[TransferKey]
Key=env:REDMOON_TRANSFER_KEY
Salt=env:REDMOON_TRANSFER_SALT
[Blowfish]
Key=BC234xs45nme7HU9
"@
    $login = @"
$common
Database=$($Profile.AccountDatabase)
[ServerConfig]
LISTEN_PORT=$($Profile.LoginPort)
[Network]
ServerName=$($Profile.ServerName)
LoginBindAddress=$($Profile.GameAddress)
InternalBindAddress=127.0.0.1
AdvertisedAddress=$($Profile.GameAddress)
GamePort=$($Profile.GamePort)
[AccountServer]
ACCOUNT_PORT=$($Profile.InternalPort)
LOGINNAME=redmoon
PASSWORD=env:REDMOON_AUTH_PASSWORD
$crypto
"@
    $game = @"
$common
Database=$($Profile.GameDatabase)
[AccountServer]
ACCOUNT_IP=127.0.0.1
ACCOUNT_PORT=$($Profile.InternalPort)
SERVERNAME=$($Profile.ServerName)
LOGINNAME=redmoon
PASSWORD=env:REDMOON_AUTH_PASSWORD
MAXLOGINTABLESIZE=500
[Network]
GameBindAddress=$($Profile.GameAddress)
GamePort=$($Profile.GamePort)
$crypto
"@
    Write-Utf8 (Join-Path $server 'Login.cfg') $login
    Write-Utf8 (Join-Path $server 'Shell.ini') $game
}
