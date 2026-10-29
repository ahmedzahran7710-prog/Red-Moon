#requires -Version 5.1
[CmdletBinding()]
param([string]$Root = '', [string]$PreviousRoot = '')
# Keep path-dependent defaults out of param(): required by Windows PowerShell 5.1.
$scriptFile = $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptFile)) {
    throw 'Run this saved script file, not its pasted contents; its directory is required.'
}
$scriptDirectory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($scriptFile))
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = [IO.Directory]::GetParent($scriptDirectory).FullName
}
. (Join-Path $scriptDirectory 'Common.ps1')
Assert-CiMode $false # Migration always uses Windows DPAPI, never the CI plaintext store.
if ([string]::IsNullOrWhiteSpace($PreviousRoot)) {
    Write-Host 'Keep the OLD installation. Stop it with its own Stop-RedMoon.cmd first.'
    Write-Host 'Back up both existing databases in HeidiSQL before switching. Do NOT reimport SQL.'
    $PreviousRoot = (Read-Host 'Full path to the OLD installed folder (containing .local)').Trim().Trim('"')
}
if ([string]::IsNullOrWhiteSpace($PreviousRoot)) { throw 'An old installation path is required; nothing was copied.' }
$Root = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Root).ProviderPath).TrimEnd('\')
$PreviousRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $PreviousRoot).ProviderPath).TrimEnd('\')
if ($Root.Length -le 3 -or $PreviousRoot.Length -le 3) { throw 'Choose installation folders, not drive roots.' }
if ($Root -ieq $PreviousRoot -or $Root.StartsWith($PreviousRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or $PreviousRoot.StartsWith($Root + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Old and new installations must be separate, non-overlapping folders.'
}
function Assert-PlainPath([string]$Path) {
    # Do not follow junctions/symlinks into another installation or outside its data.
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Migration refuses junctions/symlinks; use ordinary separate installation folders.' }
        }
        $parent = [IO.Directory]::GetParent($cursor)
        $cursor = if ($parent) { $parent.FullName } else { $null }
    }
}
Assert-PlainPath $Root
Assert-PlainPath $PreviousRoot
if (Test-Path -LiteralPath (Join-Path $Root '.local')) { throw 'Destination already has local state. Nothing will be overwritten; extract a fresh full bundle into a new folder.' }
if (@(Get-ChildItem -LiteralPath $Root -Force | Where-Object { $_.Name -like '.local-migration-*' }).Count -ne 0) {
    throw 'An interrupted migration staging folder exists here. Preserve it and review the previous error before retrying.'
}
foreach ($name in @('RedMoon.AccountServer.exe','RedMoon.GameServer.exe','RedMoon.Admin.exe','RedMoon.Core.dll','RedMoon.Data.dll')) {
    $file = Join-Path $Root "server/$name"
    Assert-PlainPath $file
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw 'Destination is not a complete new RedMoon Windows bundle. Download/extract the FULL package first.' }
}
foreach ($name in @('RedMoon.AccountServer.ini','RedMoon.GameServer.ini','Login.cfg','Shell.ini','LoginServer.exe','MsgServer.exe','redmoon.stop')) {
    if (Test-Path -LiteralPath (Join-Path $Root "server/$name")) { throw 'Destination contains previous configuration/runtime state. Use a fresh separate bundle; no in-place overwrite is supported.' }
}
foreach ($name in @('installed.json','active-profile.json','runtime-secrets.dpapi')) {
    $file = Join-Path $PreviousRoot ".local/$name"
    Assert-PlainPath $file
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw 'The old folder is not a completed RedMoon DPAPI installation. Do not run Setup against its existing databases; review the missing state first.' }
}
$profile = Read-Profile (Join-Path $PreviousRoot '.local/active-profile.json')
$state = Get-Content -LiteralPath (Join-Path $PreviousRoot '.local/installed.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($state.SchemaVersion -ne 1 -or $state.AccountDatabase -cne $profile.AccountDatabase -or $state.GameDatabase -cne $profile.GameDatabase) {
    throw 'Installed marker and active profile do not describe the same supported databases. Nothing was copied.'
}
# Opening the existing vault proves the current Windows identity can use it.
# Do not generate/re-encrypt/print credentials, and do not contact MySQL here.
$secrets = Load-LocalSecrets $PreviousRoot $false
try {
    foreach ($key in @('REDMOON_DB_PASSWORD','REDMOON_AUTH_PASSWORD','REDMOON_TRANSFER_KEY','REDMOON_TRANSFER_SALT')) {
        if ([string]::IsNullOrWhiteSpace([string]$secrets.$key)) { throw 'The old vault is incomplete. No credentials or databases will be reset.' }
    }
} finally { $secrets = $null }
function Assert-PreviousStopped {
    $record = Join-Path $PreviousRoot '.local/processes.json'
    if (Test-Path -LiteralPath $record) {
        Assert-PlainPath $record
        foreach ($entry in @(Read-OwnedProcessRecords $record -AllowLegacy)) {
            $recorded = Get-NormalizedProcessPath ([string]$entry.Executable)
            $expected = Get-NormalizedProcessPath (Join-Path $PreviousRoot ("server/" + $entry.Name + '.exe'))
            $started = [DateTimeOffset]::Parse($entry.StartUtc).UtcDateTime
            if ($recorded -ine $expected) { throw 'Old process record belongs to a different path. Review it; no process will be stopped by migration.' }
            $process = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
            if ($process) {
                $actual = Get-NormalizedProcessPath ([string]$process.MainModule.FileName)
                if ($actual -ieq $expected -and [Math]::Abs(($process.StartTime.ToUniversalTime() - $started).TotalSeconds) -le 2) {
                    throw 'The old installation is still running. Use its own Stop-RedMoon.cmd and wait for STOPPED before migrating.'
                }
            }
        }
    }
    # Also detect an old server launched without a process record.
    foreach ($process in @(Get-Process -Name 'LoginServer','MsgServer','RedMoon.AccountServer','RedMoon.GameServer' -ErrorAction SilentlyContinue)) {
        $actual = Get-NormalizedProcessPath ([string]$process.MainModule.FileName)
        if ($actual.StartsWith($PreviousRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'A server from the old folder is still running. Stop it gracefully first; migration never kills processes.'
        }
    }
    $ports = @([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() | ForEach-Object { $_.Port })
    foreach ($port in @($profile.LoginPort,$profile.GamePort,$profile.InternalPort)) {
        if ($ports -contains [int]$port) { throw 'A game/account/link port is still occupied. Stop the old installation and review any conflicting process; nothing was copied.' }
    }
}
Assert-PreviousStopped
$legacy = (Test-Path -LiteralPath (Join-Path $PreviousRoot 'server/LoginServer.exe')) -and (Test-Path -LiteralPath (Join-Path $PreviousRoot 'server/MsgServer.exe'))
$modern = (Test-Path -LiteralPath (Join-Path $PreviousRoot 'server/RedMoon.AccountServer.exe')) -and (Test-Path -LiteralPath (Join-Path $PreviousRoot 'server/RedMoon.GameServer.exe'))
if ($legacy -eq $modern) { throw 'Old runtime layout is incomplete or ambiguous. Expected one matching pair of RedMoon or legacy server executables.' }
$accountConfig = if ($legacy) { 'Login.cfg' } else { 'RedMoon.AccountServer.ini' }
$gameConfig = if ($legacy) { 'Shell.ini' } else { 'RedMoon.GameServer.ini' }
$plan = New-Object Collections.Generic.List[object]
function Add-Copy([string]$Source, [string]$Relative) {
    Assert-PlainPath $Source
    if (!(Test-Path -LiteralPath $Source -PathType Leaf)) { throw "A required old runtime file is missing: $Relative. Nothing was installed." }
    $target = Join-Path $Root $Relative
    Assert-PlainPath $target
    if ((Test-Path -LiteralPath $target) -and !(Test-Path -LiteralPath $target -PathType Leaf)) { throw 'A destination directory conflicts with a runtime file; use a fresh bundle.' }
    $plan.Add(@{ Source=$Source; Relative=$Relative })
}
Add-Copy (Join-Path $PreviousRoot "server/$accountConfig") 'server/RedMoon.AccountServer.ini'
Add-Copy (Join-Path $PreviousRoot "server/$gameConfig") 'server/RedMoon.GameServer.ini'
Add-Copy (Join-Path $PreviousRoot 'server/GameMap.dat') 'server/GameMap.dat'
foreach ($folder in @('ini','Maps','DMaps','Scene')) {
    $from = Join-Path $PreviousRoot "server/$folder"
    if (!(Test-Path -LiteralPath $from)) { continue }
    Assert-PlainPath $from
    $directories = New-Object Collections.Generic.Queue[string]
    $directories.Enqueue($from)
    while ($directories.Count -gt 0) {
        foreach ($item in @(Get-ChildItem -LiteralPath ($directories.Dequeue()) -Force)) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'A runtime data junction/symlink was found. No files will be followed outside the old installation.' }
            if ($item.PSIsContainer) { $directories.Enqueue($item.FullName); continue }
            if ($item.Extension.ToLowerInvariant() -in @('.exe','.dll','.bat','.cmd','.ps1','.vbs','.js','.msi','.com','.lnk','.url')) { throw 'Executable/script content was found in an old data directory. Review it manually; migration copies data, not legacy programs.' }
            Add-Copy $item.FullName ($item.FullName.Substring($PreviousRoot.Length + 1))
        }
    }
}
$stage = Join-Path $Root ('.local-migration-' + [Guid]::NewGuid().ToString('N'))
$changes = New-Object Collections.Generic.List[object]
$committed = $false
$cleanStage = $true
try {
    [IO.Directory]::CreateDirectory((Join-Path $stage '.local')) | Out-Null
    foreach ($file in @('installed.json','runtime-secrets.dpapi')) {
        [IO.File]::Copy((Join-Path $PreviousRoot ".local/$file"), (Join-Path $stage ".local/$file"), $false)
    }
    # Only this informational path changes; connection/game/client settings stay as installed.
    $profile | Add-Member -NotePropertyName SourcePath -NotePropertyValue $Root -Force
    Write-Utf8 (Join-Path $stage '.local/active-profile.json') ($profile | ConvertTo-Json -Depth 8)
    foreach ($entry in $plan) {
        $target = Join-Path $stage $entry.Relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        [IO.File]::Copy($entry.Source, $target, $false)
    }
    Write-Utf8 (Join-Path $stage '.local/migration.json') (@{
        SchemaVersion=1; PreviousRoot=$PreviousRoot; CurrentRoot=$Root; MigratedUtc=[DateTime]::UtcNow.ToString('o')
        SourceLayout=$(if ($legacy) { 'legacy-names' } else { 'redmoon-names' }); RuntimeFilesCopied=$plan.Count
        DatabaseContacted=$false; CredentialsRotated=$false; OldInstallationModified=$false; ProcessRecordsCopied=$false
    } | ConvertTo-Json)
    Assert-PreviousStopped
    foreach ($entry in $plan) {
        $target = Join-Path $Root $entry.Relative
        $backup = Join-Path $stage ('rollback/' + $entry.Relative)
        $existed = Test-Path -LiteralPath $target
        if ($existed) {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
            [IO.File]::Copy($target, $backup, $false)
        }
        $changes.Add(@{ Target=$target; Backup=$backup; Existed=$existed })
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        [IO.File]::Copy((Join-Path $stage $entry.Relative), $target, $true)
    }
    # Publish the complete local state last, by a same-volume directory rename.
    # Start never sees an installed marker paired with incomplete credentials/configs.
    [IO.Directory]::Move((Join-Path $stage '.local'), (Join-Path $Root '.local'))
    $committed = $true
} catch {
    $originalFailure = $_
    if (!$committed) {
        try {
            for ($i = $changes.Count - 1; $i -ge 0; $i--) {
                $change = $changes[$i]
                if ($change.Existed) { [IO.File]::Copy($change.Backup, $change.Target, $true) }
                elseif (Test-Path -LiteralPath $change.Target -PathType Leaf) { [IO.File]::Delete($change.Target) }
            }
        } catch {
            $cleanStage = $false
            throw "Migration failed and destination rollback needs review. Preserve $stage and the OLD installation; do not run Setup or delete databases."
        }
    }
    throw $originalFailure
} finally {
    if ($cleanStage -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
Write-Host 'MIGRATION_READY. Existing DPAPI vault, settings and runtime data copied; no SQL was executed.'
Write-Host 'No old executable, process record, stop marker, client file or log was copied or modified.'
Write-Host 'Use Start-RedMoon.cmd HERE, then verify the same account and character in your existing client.'
Write-Host 'KEEP the old folder and database backups until your local verification succeeds. Do not start both installations together.'
