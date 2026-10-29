#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RuntimeRoot, [Parameter(Mandatory=$true)][string]$LegacyRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $RuntimeRoot 'setup/Common.ps1')
if ($env:GITHUB_ACTIONS -ne 'true' -or $PSVersionTable.PSVersion.Major -ne 5) { throw 'Disposable Windows PowerShell 5.1 CI only.' }
$passed = $false
$stage = 'migrate-using-extracted-scripts'
try {
    # These scripts, binaries and assets are from the exact delivered ZIP, not the checkout.
    & (Join-Path $RuntimeRoot 'setup/Migrate-RedMoon.ps1') -Root $RuntimeRoot -PreviousRoot $LegacyRoot
    $profile = Read-Profile (Join-Path $RuntimeRoot '.local/active-profile.json')
    if ($profile.GameAddress -ne '127.0.0.1' -or $profile.DatabasePort -ne 13306 -or $profile.GameDatabase -ne 'redmoon_legacy_game') { throw 'Expected isolated migration fixture only.' }
    $stage = 'start-extracted-runtime'
    & (Join-Path $RuntimeRoot 'setup/Start-RedMoon.ps1') -Root $RuntimeRoot -TimeoutSeconds 180
    $stage = 'authenticate-with-existing-fixture'
    $inputJson = @{ Account='ci_player'; Password=$env:REDMOON_CI_GAME_PASSWORD; Port=[int]$profile.LoginPort; GamePort=[int]$profile.GamePort } | ConvertTo-Json -Compress
    $auth = (Invoke-PrivateProcess (Join-Path $RuntimeRoot 'server/RedMoon.Admin.exe') 'probe-login' ($inputJson + "`n")) | ConvertFrom-Json
    if (!$auth.Authorized -or !$auth.RedirectVerified) { throw 'Extracted package authentication/redirect failed.' }
    $handoff = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    $log = Join-Path $RuntimeRoot '.local/logs/RedMoon.GameServer.stdout.log'
    while ([DateTime]::UtcNow -lt $deadline) {
        $stream = [IO.File]::Open($log, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8, $true)
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($text.Contains('Packet body rejected:')) { throw 'Extracted game runtime rejected login metadata.' }
        if ($text.Contains('REDMOON_AUTH_TOKEN_QUEUED')) { $handoff = $true; break }
        Start-Sleep -Milliseconds 200
    }
    if (!$handoff) { throw 'Extracted package did not queue the account token.' }
    $stage = 'stop-extracted-runtime'
    & (Join-Path $RuntimeRoot 'setup/Stop-RedMoon.ps1') -Root $RuntimeRoot
    $passed = $true
} finally {
    Write-Utf8 (Join-Path $env:RUNNER_TEMP 'fox-evidence/package-runtime-results.json') (@{
        source_head=$env:GITHUB_SHA; passed=$passed; stage=$stage
        exact_zip_extracted=$true; scripts_and_executables_from_extracted_bundle=$true
        dpapi_migration=$passed; existing_fixture_authenticated=$passed; token_handoff=$passed; graceful_stop=$passed
        real_client_executed=$false; real_owner_database_touched=$false
    } | ConvertTo-Json)
}
Write-Host 'EXACT_WINDOWS_PACKAGE_RUNTIME_PASS. Downloaded-byte inventory and extracted runtime checked; no real client executed.'
