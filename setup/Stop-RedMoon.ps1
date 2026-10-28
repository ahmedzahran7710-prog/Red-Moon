[CmdletBinding()]
param([string]$Root = (Split-Path -Parent $PSScriptRoot), [switch]$CI)
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-CiMode ([bool]$CI)
$Root = [IO.Path]::GetFullPath($Root)
$record = Join-Path $Root '.local/processes.json'
if (!(Test-Path -LiteralPath $record)) { Write-Host 'No process record for this installation.'; exit 0 }
$entries = @(Get-Content -LiteralPath $record -Raw | ConvertFrom-Json)
$owned = @()
foreach ($entry in $entries) {
    $p = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
    if (!$p) { continue }
    if ([IO.Path]::GetFullPath($p.Path) -ne [IO.Path]::GetFullPath($entry.Executable) -or [Math]::Abs(($p.StartTime.ToUniversalTime() - [DateTime]::Parse($entry.StartUtc).ToUniversalTime()).TotalSeconds) -gt 2) {
        throw 'A recorded PID now belongs to a different process. It will not be touched.'
    }
    $owned += $p
}
Write-Utf8 (Join-Path $Root 'server/redmoon.stop') 'requested safe shutdown'
foreach ($p in $owned) {
    if (!$p.WaitForExit(20000)) { throw 'Graceful shutdown is still pending. Inspect logs; no force-kill was performed.' }
}
Remove-Item -LiteralPath $record
Write-Host 'STOPPED. MySQL/XAMPP and unrelated processes were not stopped.'
