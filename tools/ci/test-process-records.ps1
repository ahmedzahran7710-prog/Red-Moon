$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../setup/Common.ps1')
$fixture = Join-Path $env:RUNNER_TEMP 'owned-records-test.json'
$records = @(
    @{ Name='RedMoon.AccountServer'; Id=11; Executable='C:	estinirst.exe'; StartUtc='2026-01-01T00:00:00Z' },
    @{ Name='RedMoon.GameServer'; Id=12; Executable='C:	estin
ext.exe'; StartUtc='2026-01-01T00:00:00Z' }
)
Write-Utf8 $fixture ($records | ConvertTo-Json)
$read = @(Read-OwnedProcessRecords $fixture)
if ($read.Count -ne 2 -or $read[0].Name -ne 'RedMoon.AccountServer' -or $read[1].Id -ne 12) { throw 'Two process entries were not enumerated independently.' }
Write-Utf8 $fixture ($records[0] | ConvertTo-Json)
$single = @(Read-OwnedProcessRecords $fixture)
if ($single.Count -ne 1 -or $single[0].Name -ne 'RedMoon.AccountServer') { throw 'Single process entry failed.' }
Write-Utf8 $fixture '[[{"Name":"RedMoon.AccountServer","Id":1}]]'
$rejected = $false
try { Read-OwnedProcessRecords $fixture | Out-Null } catch { $rejected=$true }
if (!$rejected) { throw 'Nested process records must be rejected.' }
Write-Host 'PS51_PROCESS_RECORDS_PASS: independent records, single record, malformed nesting rejected.'

# Windows PowerShell must retain a query/synchronization handle to an attached
# process before it exits; a null ExitCode is not interchangeable with nonzero.
foreach ($wanted in @(0, 7)) {
    $child = Start-Process -FilePath 'powershell.exe' -ArgumentList "-NoProfile -NonInteractive -Command `"Start-Sleep -Milliseconds 700; exit $wanted`"" -PassThru -WindowStyle Hidden
    $attached = Get-Process -Id $child.Id
    $null = $attached.Handle
    if (!$attached.WaitForExit(10000)) { $child.Kill(); throw 'Owned test child did not exit.' }
    if ($null -eq $attached.ExitCode -or $attached.ExitCode -ne $wanted) { throw "Attached process exit-code test failed: expected $wanted." }
    $attached.Dispose(); $child.Dispose()
}
Write-Host 'PS51_ATTACHED_EXIT_CODE_PASS: exit 0 and exit 7 distinguished.'
