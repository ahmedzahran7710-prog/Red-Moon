$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../setup/Common.ps1')
$fixture = Join-Path $env:RUNNER_TEMP 'owned-records-test.json'
$records = @(
    @{ Name='LoginServer'; Id=11; Executable='C:	estinirst.exe'; StartUtc='2026-01-01T00:00:00Z' },
    @{ Name='MsgServer'; Id=12; Executable='C:	estin
ext.exe'; StartUtc='2026-01-01T00:00:00Z' }
)
Write-Utf8 $fixture ($records | ConvertTo-Json)
$read = @(Read-OwnedProcessRecords $fixture)
if ($read.Count -ne 2 -or $read[0].Name -ne 'LoginServer' -or $read[1].Id -ne 12) { throw 'Two process entries were not enumerated independently.' }
Write-Utf8 $fixture ($records[0] | ConvertTo-Json)
$single = @(Read-OwnedProcessRecords $fixture)
if ($single.Count -ne 1 -or $single[0].Name -ne 'LoginServer') { throw 'Single process entry failed.' }
Write-Utf8 $fixture '[[{"Name":"LoginServer","Id":1}]]'
$rejected = $false
try { Read-OwnedProcessRecords $fixture | Out-Null } catch { $rejected=$true }
if (!$rejected) { throw 'Nested process records must be rejected.' }
Write-Host 'PS51_PROCESS_RECORDS_PASS: independent records, single record, malformed nesting rejected.'
