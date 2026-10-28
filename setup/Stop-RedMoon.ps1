[CmdletBinding()]
param([string]$Root = (Split-Path -Parent $PSScriptRoot), [switch]$CI)
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-CiMode ([bool]$CI)
$Root = (Resolve-Path -LiteralPath $Root).ProviderPath
$record = Join-Path $Root '.local/processes.json'
if (!(Test-Path -LiteralPath $record)) { Write-Host 'No process record for this installation.'; return }
$entries = @(Read-OwnedProcessRecords $record)
$owned = @()
$observed = @()
try {
    foreach ($entry in $entries) {
        if ([string]$entry.Name -notin @('LoginServer','MsgServer')) { throw 'Unrecognized process entry; nothing is stopped.' }
        $p = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
        if (!$p) { continue }
        $actualPath = Get-NormalizedProcessPath ([string]$p.MainModule.FileName)
        $recordedPath = Get-NormalizedProcessPath ([string]$entry.Executable)
        $expectedPath = Get-NormalizedProcessPath (Join-Path $Root ("server/" + $entry.Name + '.exe'))
        $observed += @{ Name=$entry.Name; Id=$entry.Id; Actual=$actualPath; Recorded=$recordedPath; Expected=$expectedPath }
        $sameTime = [Math]::Abs(($p.StartTime.ToUniversalTime() - [DateTimeOffset]::Parse($entry.StartUtc).UtcDateTime).TotalSeconds) -le 2
        if ($actualPath -ine $expectedPath -or $recordedPath -ine $expectedPath -or !$sameTime) {
            throw 'A recorded PID/path/start-time does not match this installation. No other process will be touched.'
        }
        # Get-Process attaches to an existing PID. Keep its native handle alive
        # BEFORE exit so Windows PowerShell 5.1 can retrieve a trustworthy ExitCode.
        $null = $p.Handle
        $owned += $p
    }
    # The stop request is installation-local, not a signal to an arbitrary PID.
    Write-Utf8 (Join-Path $Root 'server/redmoon.stop') 'requested safe shutdown'
    foreach ($p in $owned) {
        if (!$p.WaitForExit(20000)) { throw 'Graceful shutdown is still pending. Inspect logs; no force-kill was performed.' }
        $exit = $p.ExitCode
        $observed += @{ Name=$p.ProcessName; Id=$p.Id; ExitCode=$exit }
        if ($null -eq $exit) { throw 'Shutdown completed but an exit code could not be retrieved; not claiming a clean stop.' }
        if ($exit -ne 0) { throw "A server exited with code $exit during shutdown. Inspect local logs." }
    }
    Remove-Item -LiteralPath $record
    Write-Host 'STOPPED. MySQL/XAMPP and unrelated processes were not stopped.'
} catch {
    if ($CI) {
        $diagnostic = @{ Root=$Root; OwnedProcesses=$observed; Stack=$_.ScriptStackTrace; Message=$_.Exception.Message }
        Write-Utf8 (Join-Path $env:RUNNER_TEMP 'fox-evidence/stop-diagnostics.json') ($diagnostic | ConvertTo-Json -Depth 8)
    }
    throw
}
