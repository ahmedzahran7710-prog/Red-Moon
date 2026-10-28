[CmdletBinding()]
param([string]$Root = (Split-Path -Parent $PSScriptRoot), [switch]$CI, [int]$TimeoutSeconds = 180)
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-CiMode ([bool]$CI)
$Root = [IO.Path]::GetFullPath($Root)
if (!(Test-Path -LiteralPath (Join-Path $Root '.local/installed.json'))) { throw 'Run Setup-RedMoon.cmd once before starting.' }
$profile = Read-Profile (Join-Path $Root '.local/active-profile.json')
$addresses = @([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | ForEach-Object { $_.GetIPProperties().UnicastAddresses } | ForEach-Object { $_.Address.ToString() })
if ($addresses -notcontains $profile.GameAddress) { throw 'The configured Hamachi address is not active on this PC. Start Hamachi first or review the local profile.' }
$ports = @([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() | ForEach-Object { $_.Port })
foreach ($port in @($profile.LoginPort,$profile.GamePort,$profile.InternalPort)) {
    if ($ports -contains [int]$port) { throw "Port $port is already occupied. No existing process will be stopped. Use Stop-RedMoon.cmd for this installation, or review the other server." }
}
$secrets = Load-LocalSecrets $Root ([bool]$CI)
Export-ServerSecrets $secrets
$server = Join-Path $Root 'server'
$logs = Join-Path $Root '.local/logs'
[IO.Directory]::CreateDirectory($logs) | Out-Null
$stop = Join-Path $server 'redmoon.stop'
if (Test-Path -LiteralPath $stop) { Remove-Item -LiteralPath $stop }
$started = New-Object Collections.Generic.List[object]
function Start-OwnedServer([string]$Name) {
    $exe = Join-Path $server ($Name + $(if ($script:OnWindows) { '.exe' } else { '' }))
    if (!(Test-Path -LiteralPath $exe)) { throw "Missing published executable: $Name" }
    # Logs contain local runtime output; they are not uploaded by these scripts.
    $startArguments = @{ FilePath=$exe; ArgumentList='--headless'; WorkingDirectory=$server; PassThru=$true; RedirectStandardOutput=(Join-Path $logs "$Name.stdout.log"); RedirectStandardError=(Join-Path $logs "$Name.stderr.log") }
    if ($script:OnWindows) { $startArguments.WindowStyle = 'Hidden' }
    $process = Start-Process @startArguments
    $started.Add(@{ Name=$Name; Id=$process.Id; Executable=[IO.Path]::GetFullPath($exe); StartUtc=$process.StartTime.ToUniversalTime().ToString('o') })
    Write-Utf8 (Join-Path $Root '.local/processes.json') (@($started.ToArray()) | ConvertTo-Json)
    return $process
}
function Wait-Ready($Process, [string]$File, [string]$Marker) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $Process.Refresh()
        if ($Process.HasExited) { throw "Server exited before readiness. See local logs in $logs (do not share secrets)." }
        if ((Test-Path -LiteralPath $File) -and [IO.File]::ReadAllText($File).Contains($Marker)) { return }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Marker. Inspect local logs before retrying."
}
try {
    Write-Host 'Starting LoginServer...'
    $login = Start-OwnedServer 'LoginServer'
    Wait-Ready $login (Join-Path $logs 'LoginServer.stdout.log') 'REDMOON_LOGIN_READY'
    Write-Host 'Starting MsgServer and loading maps...'
    $game = Start-OwnedServer 'MsgServer'
    Wait-Ready $game (Join-Path $logs 'MsgServer.stdout.log') 'REDMOON_GAME_READY'
    Wait-Ready $login (Join-Path $logs 'LoginServer.stdout.log') 'REDMOON_GAME_REGISTERED'
    Write-Host "SERVERS_READY: $($profile.GameAddress), login $($profile.LoginPort), game $($profile.GamePort)."
    Write-Host 'This confirms server startup/link only, not a successful real client login. Use Stop-RedMoon.cmd to save and stop.'
} catch {
    # Ask only our newly started processes to shut down via the local marker.
    Write-Utf8 $stop 'startup failed; stop this installation'
    throw
} finally {
    foreach ($name in @('REDMOON_DB_PASSWORD','REDMOON_AUTH_PASSWORD','REDMOON_TRANSFER_KEY','REDMOON_TRANSFER_SALT')) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
}
