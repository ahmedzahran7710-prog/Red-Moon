#requires -Version 5.1
[CmdletBinding()]
param([string]$ReportPath = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)) '../..'))
$hostExe = Join-Path $PSHOME 'powershell.exe'
if (!(Test-Path -LiteralPath $hostExe)) { throw 'This contract must run in Windows PowerShell 5.1, not only pwsh.' }
if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) { throw 'Expected Windows PowerShell 5.1.' }
if ([string]::IsNullOrWhiteSpace($ReportPath)) { $ReportPath = Join-Path $env:RUNNER_TEMP 'fox-evidence/launcher-results.json' }
$utf8 = New-Object Text.UTF8Encoding($false)
$base = Join-Path $env:RUNNER_TEMP ('redmoon-launchers-' + [Guid]::NewGuid().ToString('N'))
# Deliberately includes spaces, wildcard-looking brackets, ampersand and an exclamation.
$installation = Join-Path $base 'Red Moon [paths] & local!'
$setup = Join-Path $installation 'setup'
$elsewhere = Join-Path $base 'unrelated working directory'
$alternate = Join-Path $base 'explicit alternate root'
foreach ($dir in @($setup, $elsewhere, $alternate)) { [IO.Directory]::CreateDirectory($dir) | Out-Null }
$script:cases = New-Object Collections.Generic.List[object]
$script:locationObservations = New-Object Collections.Generic.List[object]
function Check([string]$Name, [bool]$Passed, [string]$Detail = '') {
    $script:cases.Add(@{ Name=$Name; Passed=$Passed; Detail=$Detail })
    if (!$Passed) { throw "Launcher contract failed: $Name. $Detail" }
}
function Write-Text([string]$Path, [string]$Text) { [IO.File]::WriteAllText($Path, $Text, $utf8) }
function Same-Path([string]$A, [string]$B) {
    return [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($A).TrimEnd('\'), [IO.Path]::GetFullPath($B).TrimEnd('\'))
}
function Invoke-Isolated([string]$Exe, [string]$Arguments, [string]$Capture, [int]$StubExit = 0) {
    if (Test-Path -LiteralPath $Capture) { Remove-Item -LiteralPath $Capture }
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName=$Exe; $info.Arguments=$Arguments; $info.WorkingDirectory=$elsewhere
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardInput=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    $info.EnvironmentVariables['REDMOON_PATH_TEST_CAPTURE']=$Capture
    $info.EnvironmentVariables['REDMOON_PATH_TEST_EXIT']=[string]$StubExit
    $process = New-Object Diagnostics.Process
    $process.StartInfo=$info
    try {
        if (!$process.Start()) { throw 'Failed to start owned regression child.' }
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        # Satisfy the real CMD launcher's pause without an interactive terminal.
        $process.StandardInput.WriteLine(''); $process.StandardInput.Close()
        if (!$process.WaitForExit(20000)) { $process.Kill(); throw 'Owned launcher regression child timed out.' }
        $textOut=$stdout.GetAwaiter().GetResult(); $textErr=$stderr.GetAwaiter().GetResult()
        $captureData=$null
        if (Test-Path -LiteralPath $Capture) { $captureData=Get-Content -LiteralPath $Capture -Raw -Encoding UTF8 | ConvertFrom-Json }
        return [PSCustomObject]@{ ExitCode=$process.ExitCode; Stdout=$textOut; Stderr=$textErr; Capture=$captureData }
    } finally { $process.Dispose() }
}
$boundary = @'
# This test double is the FIRST import boundary in the actual entrypoint body.
# Stop before any DB, credential prompt, file mutation or firewall operation.
$profileValue = $null
$profileVariable = Get-Variable -Name ProfilePath -ErrorAction SilentlyContinue
if ($profileVariable) { $profileValue = [string]$profileVariable.Value }
$captured = @{ Root=[string]$Root; ProfilePath=$profileValue; ScriptDirectory=[string]$scriptDirectory; Version=$PSVersionTable.PSVersion.ToString() }
[IO.File]::WriteAllText($env:REDMOON_PATH_TEST_CAPTURE, ($captured | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
throw 'REDMOON_SAFE_PATH_BOUNDARY'
'@
$stub = @'
#requires -Version 5.1
[CmdletBinding()]
param([string]$Root, [string]$ProfilePath)
$captured = @{ Root=$Root; ProfilePath=$ProfilePath; WorkingDirectory=(Get-Location).ProviderPath; Version=$PSVersionTable.PSVersion.ToString() }
[IO.File]::WriteAllText($env:REDMOON_PATH_TEST_CAPTURE, ($captured | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
exit ([int]$env:REDMOON_PATH_TEST_EXIT)
'@
$names = @('Setup-RedMoon','Start-RedMoon','Stop-RedMoon','Install-ClientConfig','Build-RedMoon','Allow-Hamachi')
$legacy = $null
$success = $false
try {
    # Minimal old param block: capture whether this native host reproduces the
    # reported failure, never run the old setup body against a database.
    $legacyFile=Join-Path $setup 'Legacy-Default-Probe.ps1'
    Write-Text $legacyFile @'
[CmdletBinding()]
param([string]$Root = (Split-Path -Parent $PSScriptRoot), [string]$ProfilePath = (Join-Path $PSScriptRoot 'owner-profile.json'))
[IO.File]::WriteAllText($env:REDMOON_PATH_TEST_CAPTURE, '{"BodyReached":true}')
'@
    $capture=Join-Path $base 'legacy.json'
    $old=Invoke-Isolated $hostExe "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$legacyFile`"" $capture
    $legacy=@{ ExitCode=$old.ExitCode; BodyReached=($null -ne $old.Capture); ReportedEmptyPathReproduced=($old.ExitCode -ne 0 -and ($old.Stderr + $old.Stdout).Contains('empty string')) }

    Write-Text (Join-Path $setup 'Common.ps1') $boundary
    foreach ($name in $names) {
        $original=Join-Path $repo "setup/$name.ps1"
        $target=Join-Path $setup "$name.ps1"
        Copy-Item -LiteralPath $original -Destination $target -Force
        $tokens=$null; $parseErrors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($original,[ref]$tokens,[ref]$parseErrors)
        Check "$name AST parses" ($parseErrors.Count -eq 0)
        foreach ($parameter in $ast.ParamBlock.Parameters) {
            if ($parameter.Name.VariablePath.UserPath -in @('Root','ProfilePath')) {
                Check "$name $($parameter.Name.VariablePath.UserPath) has no path-dependent default" ($parameter.DefaultValue -is [Management.Automation.Language.StringConstantExpressionAst] -and $parameter.DefaultValue.Value -eq '')
            }
        }
        $hasProfile=$name -in @('Setup-RedMoon','Install-ClientConfig')
        $extra=if ($name -eq 'Allow-Hamachi') { ' -AllowedFriendAddress 192.0.2.10' } else { '' }
        $capture=Join-Path $base "$name-default.json"
        $result=Invoke-Isolated $hostExe "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$target`"$extra" $capture
        Check "$name default native -File reaches safe body boundary" ($null -ne $result.Capture -and $result.ExitCode -ne 0 -and $result.Stderr.Contains('REDMOON_SAFE_PATH_BOUNDARY'))
        Check "$name default root follows script, not caller cwd" (Same-Path $result.Capture.Root $installation)
        Check "$name import follows script directory" (Same-Path $result.Capture.ScriptDirectory $setup)
        if ($hasProfile) { Check "$name default profile follows script" (Same-Path $result.Capture.ProfilePath (Join-Path $setup 'owner-profile.json')) }

        $profileOverride=Join-Path $alternate 'custom-profile.json'
        $profileArg=if ($hasProfile) { " -ProfilePath `"$profileOverride`"" } else { '' }
        $capture=Join-Path $base "$name-explicit.json"
        $result=Invoke-Isolated $hostExe "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$target`" -Root `"$alternate`"$profileArg$extra" $capture
        Check "$name explicit root retained" ($null -ne $result.Capture -and (Same-Path $result.Capture.Root $alternate))
        if ($hasProfile) { Check "$name explicit profile retained" (Same-Path $result.Capture.ProfilePath $profileOverride) }
    }

    foreach ($name in $names) {
        if ($name -eq 'Allow-Hamachi') { continue }
        $cmd=Join-Path $installation "$name.cmd"
        Copy-Item -LiteralPath (Join-Path $repo "$name.cmd") -Destination $cmd -Force
        Write-Text (Join-Path $setup "$name.ps1") $stub
        foreach ($exit in @(0, 7)) {
            $capture=Join-Path $base "$name-cmd-$exit.json"
            $result=Invoke-Isolated $env:ComSpec "/d /v:off /c `"`"$cmd`"`"" $capture $exit
            Check "$name real CMD preserves exit $exit" ($result.ExitCode -eq $exit) "Observed exit $($result.ExitCode)"
            Check "$name real CMD passes correct root ($exit)" ($null -ne $result.Capture -and (Same-Path $result.Capture.Root $installation))
            # PowerShell's provider location need not equal the native CMD cwd
            # when the directory contains wildcard-looking brackets. The contract
            # is that explicit Root/ProfilePath remain correct regardless of it.
            $script:locationObservations.Add(@{ Launcher=$name; Exit=$exit; ProviderLocation=$result.Capture.WorkingDirectory; ResolvedRoot=$result.Capture.Root })
            Check "$name real CMD uses Windows PowerShell 5.1 ($exit)" ($result.Capture.Version.StartsWith('5.1.'))
            if ($name -in @('Setup-RedMoon','Install-ClientConfig')) { Check "$name real CMD passes profile ($exit)" (Same-Path $result.Capture.ProfilePath (Join-Path $setup 'owner-profile.json')) }
        }
    }

    # Real launchers + real scripts + real Common.ps1; an unconfigured fixture has
    # no DB/process state, so these must stop at their documented preconditions.
    Copy-Item -LiteralPath (Join-Path $repo 'setup/Common.ps1') -Destination (Join-Path $setup 'Common.ps1') -Force
    foreach ($name in @('Start-RedMoon','Stop-RedMoon')) {
        Copy-Item -LiteralPath (Join-Path $repo "setup/$name.ps1") -Destination (Join-Path $setup "$name.ps1") -Force
        $capture=Join-Path $base "$name-real.json"
        $cmd=Join-Path $installation "$name.cmd"
        $result=Invoke-Isolated $env:ComSpec "/d /v:off /c `"`"$cmd`"`"" $capture
        if ($name -eq 'Start-RedMoon') { Check 'Real Start CMD reaches missing-installation guard' ($result.ExitCode -ne 0 -and $result.Stderr.Contains('Run Setup-RedMoon.cmd once')) }
        else { Check 'Real Stop CMD exits safely without a process record' ($result.ExitCode -eq 0 -and $result.Stdout.Contains('No process record')) }
    }
    Check 'Path regressions did not create installation state' (!(Test-Path -LiteralPath (Join-Path $installation '.local')))
    $success=$true
} finally {
    $report=@{
        schema_version=1; source_head=$env:GITHUB_SHA; powershell_version=$PSVersionTable.PSVersion.ToString()
        engine='Windows PowerShell 5.1 / native powershell.exe -File and cmd.exe /c'
        passed=$success; cases=$script:cases.ToArray(); legacy_default_probe=$legacy
        provider_location_observations=$script:locationObservations.ToArray()
        foreign_working_directory=$true; special_character_path=$true
        mocks='Common import boundary / CMD target only; real Start and Stop guards also exercised'
        real_database_touched=$false; real_client_touched=$false; firewall_touched=$false
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ReportPath))) | Out-Null
    Write-Text $ReportPath ($report | ConvertTo-Json -Depth 8)
}
Write-Host "WINDOWS_LAUNCHER_PATHS_PASS: $($script:cases.Count) assertions. No database/client/firewall actions."
