#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$LegacySource,
    [Parameter(Mandatory=$true)][string]$LegacyRoot,
    [Parameter(Mandatory=$true)][string]$RuntimeRoot,
    [Parameter(Mandatory=$true)][string]$MysqlExe
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repo 'setup/Common.ps1')
if ($env:GITHUB_ACTIONS -ne 'true' -or $PSVersionTable.PSVersion.Major -ne 5) { throw 'Disposable Windows PowerShell 5.1 CI only; never run against an owner installation.' }
$legacyHead = 'a5932d55ce7160e8a010bbb9d57b634c51576de3'
$currentHead = $env:GITHUB_SHA
$script:cases = New-Object Collections.Generic.List[object]
$success = $false
$stage = 'legacy-setup'
$failureMessage = $null
$fixtureCreated = $false
$dpapiUsed = $false
$legacyVerified = $false
$migrate = Join-Path $repo 'setup/Migrate-RedMoon.ps1'
function Check([string]$Name, [bool]$Passed) {
    $script:cases.Add(@{ Name=$Name; Passed=$Passed })
    if (!$Passed) { throw "Migration regression failed: $Name" }
}
function Expect-Refusal([string]$Name, [scriptblock]$Action, [string]$Message) {
    $refused = $false
    try { & $Action } catch {
        if (!$_.Exception.Message.Contains($Message)) { throw }
        $refused = $true
    }
    Check $Name $refused
}
function Fingerprint([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Db-Checksums {
    return Invoke-Mysql $MysqlExe $p '' "CHECKSUM TABLE $($p.AccountDatabase).account, $($p.GameDatabase).cq_user EXTENDED;"
}
try {
    # Build/run the actual reviewed OLD source, not renamed copies of new EXEs.
    $sourceMarker = Get-Content -LiteralPath (Join-Path $LegacySource 'legacy-source.json') -Raw | ConvertFrom-Json
    Check 'Reviewed old commit exported, without switching the active branch' ($sourceMarker.source_head -eq $legacyHead)
    $legacyProfile = Join-Path $LegacySource 'setup/owner-profile.json'
    $p = Read-Profile $legacyProfile
    $p.AccountDatabase = 'redmoon_legacy_account'; $p.GameDatabase = 'redmoon_legacy_game'; $p.DatabaseUser = 'redmoon_legacy_runtime'
    Write-Utf8 $legacyProfile ($p | ConvertTo-Json)
    try {
        $env:GITHUB_SHA = $legacyHead
        & (Join-Path $LegacySource 'tools/ci/test-setup.ps1') -RuntimeRoot $LegacyRoot -MysqlExe $MysqlExe
    } finally { $env:GITHUB_SHA = $currentHead }
    $legacyVerified = $true
    Move-Item -LiteralPath (Join-Path $env:RUNNER_TEMP 'fox-evidence/integration-results.json') -Destination (Join-Path $env:RUNNER_TEMP 'fox-evidence/legacy-integration-results.json')
    # The new-install report is saved by the caller before this old regression.
    Copy-Item -LiteralPath (Join-Path $env:RUNNER_TEMP 'fox-evidence/new-install-results.json') -Destination (Join-Path $env:RUNNER_TEMP 'fox-evidence/integration-results.json')
    $p = Read-Profile (Join-Path $LegacyRoot '.local/active-profile.json')
    Check 'Only disposable loopback schemas are used' ($p.DatabasePort -eq 13306 -and $p.GameAddress -eq '127.0.0.1' -and $p.GameDatabase -eq 'redmoon_legacy_game')
    $fixtureSecrets = Load-LocalSecrets $LegacyRoot $true
    $sealed = @{}
    foreach ($property in $fixtureSecrets.PSObject.Properties) { $sealed[$property.Name] = $property.Value }
    Save-LocalSecrets $LegacyRoot $sealed $false
    $dpapiUsed = $true
    $fixtureSecrets = $null; $sealed = $null
    Remove-Item -LiteralPath (Join-Path $LegacyRoot '.local/ci-secrets.json')
    $stage = 'refuse-running-legacy-server'
    & (Join-Path $LegacySource 'setup/Start-RedMoon.ps1') -Root $LegacyRoot -TimeoutSeconds 180
    $oldRecord = Get-Content -LiteralPath (Join-Path $LegacyRoot '.local/processes.json') -Raw
    try {
        Expect-Refusal 'Running old server is refused, never killed' { & $migrate -Root $RuntimeRoot -PreviousRoot $LegacyRoot } 'still running'
        Check 'No destination state created after running-server refusal' (!(Test-Path -LiteralPath (Join-Path $RuntimeRoot '.local')))
    } finally {
        & (Join-Path $LegacySource 'setup/Stop-RedMoon.ps1') -Root $LegacyRoot
    }
    $stage = 'migration-preconditions'
    Expect-Refusal 'Same installation refused' { & $migrate -Root $LegacyRoot -PreviousRoot $LegacyRoot } 'non-overlapping'
    $nested = Join-Path $LegacyRoot 'nested-destination'
    [IO.Directory]::CreateDirectory($nested) | Out-Null
    Expect-Refusal 'Nested installation refused' { & $migrate -Root $nested -PreviousRoot $LegacyRoot } 'non-overlapping'
    [IO.Directory]::Delete($nested)
    $collision = Join-Path $RuntimeRoot 'server/RedMoon.AccountServer.ini'
    Write-Utf8 $collision 'do not overwrite this pre-existing configuration'
    $collisionHash = Fingerprint $collision
    Expect-Refusal 'Existing destination configuration refused' { & $migrate -Root $RuntimeRoot -PreviousRoot $LegacyRoot } 'previous configuration'
    Check 'Existing destination file retained byte-for-byte' ((Fingerprint $collision) -eq $collisionHash)
    Remove-Item -LiteralPath $collision
    $bad = Join-Path $env:RUNNER_TEMP 'redmoon-invalid-vault-fixture'
    [IO.Directory]::CreateDirectory((Join-Path $bad '.local')) | Out-Null
    foreach ($name in @('installed.json','active-profile.json')) { Copy-Item -LiteralPath (Join-Path $LegacyRoot ".local/$name") -Destination (Join-Path $bad ".local/$name") }
    Expect-Refusal 'Missing vault refused without Setup/reset' { & $migrate -Root $RuntimeRoot -PreviousRoot $bad } 'not a completed RedMoon DPAPI installation'
    Write-Utf8 (Join-Path $bad '.local/runtime-secrets.dpapi') 'not-a-dpapi-vault'
    Expect-Refusal 'Unreadable DPAPI vault refused' { & $migrate -Root $RuntimeRoot -PreviousRoot $bad } 'vault could not be opened'
    $processRecord = Join-Path $LegacyRoot '.local/processes.json'
    Write-Utf8 $processRecord '[[{"Name":"LoginServer","Id":1}]]'
    Expect-Refusal 'Malformed legacy PID record refused' { & $migrate -Root $RuntimeRoot -PreviousRoot $LegacyRoot } 'Invalid owned-process record'
    Remove-Item -LiteralPath $processRecord
    $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, [int]$p.LoginPort)
    $listener.Start()
    try { Expect-Refusal 'Occupied port refused without terminating its owner' { & $migrate -Root $RuntimeRoot -PreviousRoot $LegacyRoot } 'still occupied' }
    finally { $listener.Stop() }
    $unsafe = Join-Path $LegacyRoot 'server/ini/never-copy.ps1'
    Write-Utf8 $unsafe '# inert CI fixture; must not be copied or executed'
    try { Expect-Refusal 'Executable content in a data directory refused' { & $migrate -Root $RuntimeRoot -PreviousRoot $LegacyRoot } 'Executable/script content' }
    finally { Remove-Item -LiteralPath $unsafe }
    Check 'Refusals did not create destination local state' (!(Test-Path -LiteralPath (Join-Path $RuntimeRoot '.local')))
    $stage = 'synthetic-existing-character-and-custom-settings'
    $accountId = Invoke-Mysql $MysqlExe $p '' "SELECT id FROM account WHERE name='ci_player';" $p.AccountDatabase
    if ($accountId -notmatch '^\d+$') { throw 'Synthetic account fixture missing.' }
    # This row exists only in isolated CI. No real player seed data is imported.
    Invoke-Mysql $MysqlExe $p '' "INSERT INTO cq_user (id,account_id,name,lookface,profession,level,life,money,emoney) VALUES (1000001,$accountId,'migration_check',1003,10,1,100,12345,79);" $p.GameDatabase | Out-Null
    $fixtureCreated = $true
    [IO.File]::AppendAllText((Join-Path $LegacyRoot 'server/Shell.ini'), "`n; custom-owner-settings-must-survive`n")
    $custom = Join-Path $LegacyRoot 'server/ini/RedMoon-local-override.ini'
    Write-Utf8 $custom "[Custom]`nPreserveThisFile=yes`n"
    # A stale record and old stop/log files must remain in the OLD folder only.
    Write-Utf8 $processRecord $oldRecord
    Write-Utf8 (Join-Path $LegacyRoot '.local/logs/do-not-copy.log') 'old diagnostic fixture'
    $filesBefore = @{}
    foreach ($relative in @('.local/installed.json','.local/active-profile.json','.local/runtime-secrets.dpapi','.local/processes.json','server/Login.cfg','server/Shell.ini','server/GameMap.dat','server/ini/RedMoon-local-override.ini','server/redmoon.stop','.local/logs/do-not-copy.log')) {
        $filesBefore[$relative] = Fingerprint (Join-Path $LegacyRoot $relative)
    }
    $dbBefore = Db-Checksums
    $stage = 'real-dpapi-migration'
    & $migrate -Root $RuntimeRoot -PreviousRoot $LegacyRoot
    Check 'Existing account and entire character table checksums unchanged by migration' ((Db-Checksums) -ceq $dbBefore)
    foreach ($relative in $filesBefore.Keys) {
        Check "Old input unchanged: $relative" ((Fingerprint (Join-Path $LegacyRoot $relative)) -eq $filesBefore[$relative])
    }
    Check 'DPAPI ciphertext copied exactly' ((Fingerprint (Join-Path $RuntimeRoot '.local/runtime-secrets.dpapi')) -eq $filesBefore['.local/runtime-secrets.dpapi'])
    Check 'Install marker retained, not re-provisioned' ((Fingerprint (Join-Path $RuntimeRoot '.local/installed.json')) -eq $filesBefore['.local/installed.json'])
    Check 'Custom game config retained under its new name' ((Fingerprint (Join-Path $RuntimeRoot 'server/RedMoon.GameServer.ini')) -eq $filesBefore['server/Shell.ini'])
    Check 'Account config retained under its new name' ((Fingerprint (Join-Path $RuntimeRoot 'server/RedMoon.AccountServer.ini')) -eq $filesBefore['server/Login.cfg'])
    Check 'Runtime override file retained' ((Fingerprint (Join-Path $RuntimeRoot 'server/ini/RedMoon-local-override.ini')) -eq $filesBefore['server/ini/RedMoon-local-override.ini'])
    Check 'GameMap data retained' ((Fingerprint (Join-Path $RuntimeRoot 'server/GameMap.dat')) -eq $filesBefore['server/GameMap.dat'])
    $migratedProfile = Read-Profile (Join-Path $RuntimeRoot '.local/active-profile.json')
    foreach ($property in $p.PSObject.Properties) {
        if ($property.Name -eq 'SourcePath') { continue }
        Check ('Profile value retained: ' + $property.Name) ($migratedProfile.($property.Name) -ceq $property.Value)
    }
    Check 'New profile points to new source folder' ($migratedProfile.SourcePath -ieq [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\'))
    foreach ($relative in @('.local/processes.json','.local/ci-secrets.json','.local/logs/do-not-copy.log','server/redmoon.stop','server/Login.cfg','server/Shell.ini','server/LoginServer.exe','server/MsgServer.exe')) {
        Check "Stale/private/legacy output not copied: $relative" (!(Test-Path -LiteralPath (Join-Path $RuntimeRoot $relative)))
    }
    Expect-Refusal 'Repeat migration refuses an installed destination' { & $migrate -Root $RuntimeRoot -PreviousRoot $LegacyRoot } 'already has local state'
    & (Join-Path $repo 'setup/Setup-RedMoon.ps1') -Root $RuntimeRoot
    Check 'Setup after migration exits through existing marker without SQL import' ((Db-Checksums) -ceq $dbBefore)
    $stage = 'start-renamed-runtime-with-existing-credentials'
    $secrets = Load-LocalSecrets $RuntimeRoot $false
    Export-ServerSecrets $secrets
    foreach ($name in @('RedMoon.AccountServer','RedMoon.GameServer')) {
        & (Join-Path $RuntimeRoot "server/$name.exe") --check-config
        if ($LASTEXITCODE -ne 0) { throw 'Migrated runtime configuration failed.' }
    }
    $secrets = $null
    & (Join-Path $repo 'setup/Start-RedMoon.ps1') -Root $RuntimeRoot -TimeoutSeconds 180
    $records = @(Read-OwnedProcessRecords (Join-Path $RuntimeRoot '.local/processes.json'))
    Check 'Both process records use RedMoon identities' ($records.Count -eq 2 -and $records[0].Name -eq 'RedMoon.AccountServer' -and $records[1].Name -eq 'RedMoon.GameServer')
    $inputJson = @{ Account='ci_player'; Password=$env:REDMOON_CI_GAME_PASSWORD; Port=[int]$p.LoginPort; GamePort=[int]$p.GamePort } | ConvertTo-Json -Compress
    $auth = (Invoke-PrivateProcess (Join-Path $RuntimeRoot 'server/RedMoon.Admin.exe') 'probe-login' ($inputJson + "`n")) | ConvertFrom-Json
    Check 'Existing password authenticates after migration' ($auth.Authorized -and $auth.RedirectVerified)
    $handoff = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $deadline) {
        $log = Join-Path $RuntimeRoot '.local/logs/RedMoon.GameServer.stdout.log'
        $stream = [IO.File]::Open($log, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8, $true)
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($text.Contains('Packet body rejected:')) { throw 'Renamed game runtime rejected login metadata.' }
        if ($text.Contains('REDMOON_AUTH_TOKEN_QUEUED')) { $handoff = $true; break }
        Start-Sleep -Milliseconds 200
    }
    Check 'Token handoff completed before shutdown' $handoff
    & (Join-Path $repo 'setup/Stop-RedMoon.ps1') -Root $RuntimeRoot
    $gameLog = [IO.File]::ReadAllText((Join-Path $RuntimeRoot '.local/logs/RedMoon.GameServer.stdout.log'))
    Check 'Renamed game server received the existing account login token' ($gameLog.Contains('REDMOON_AUTH_TOKEN_QUEUED') -and !$gameLog.Contains('Packet body rejected:'))
    $character = Invoke-Mysql $MysqlExe $p '' "SELECT CONCAT_WS(',',name,money,emoney,account_id) FROM cq_user WHERE id=1000001;" $p.GameDatabase
    Check 'Existing character identity and currency sentinel preserved after startup' ($character -ceq "migration_check,12345,79,$accountId")
    $accounts = Invoke-Mysql $MysqlExe $p '' 'SELECT COUNT(*) FROM account;' $p.AccountDatabase
    $characters = Invoke-Mysql $MysqlExe $p '' 'SELECT COUNT(*) FROM cq_user;' $p.GameDatabase
    Check 'No account/character duplicate or reset' ([int]$accounts -eq 1 -and [int]$characters -eq 1)
    Check 'Graceful stop cleared only the new installation process record' (!(Test-Path -LiteralPath (Join-Path $RuntimeRoot '.local/processes.json')) -and (Test-Path -LiteralPath $processRecord))
    $success = $true
} catch {
    $failureMessage = $_.Exception.Message
    foreach ($name in @('REDMOON_CI_GAME_PASSWORD','REDMOON_DB_PASSWORD','REDMOON_AUTH_PASSWORD','REDMOON_TRANSFER_KEY','REDMOON_TRANSFER_SALT')) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($value) { $failureMessage = $failureMessage.Replace($value, '[redacted]') }
    }
    throw
} finally {
    $env:GITHUB_SHA = $currentHead
    $report = @{
        source_head=$currentHead; legacy_source_head=$legacyHead; passed=$success; stage=$stage; message=$failureMessage
        target='Windows PowerShell 5.1 / MariaDB 10.4.32 / disposable loopback schemas'
        cases=$script:cases.ToArray(); dpapi_used=$dpapiUsed; legacy_executables_built_from_actual_old_source=$legacyVerified
        fixture_character_created_in_isolated_ci=$fixtureCreated; real_owner_database_touched=$false
        real_client_executed=$false; real_client_login_tested=$false
    }
    Write-Utf8 (Join-Path $env:RUNNER_TEMP 'fox-evidence/migration-results.json') ($report | ConvertTo-Json -Depth 8)
}
Write-Host 'WINDOWS_MIGRATION_PASS. Old/new real servers, DPAPI and synthetic account/character preservation checked; no real client executed.'
