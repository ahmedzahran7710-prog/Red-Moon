[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RuntimeRoot, [Parameter(Mandatory=$true)][string]$MysqlExe)
$ErrorActionPreference = 'Stop'
$stage = 'profile-and-dpapi'
try {
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repo 'setup/Common.ps1')
$p = Get-Content -LiteralPath (Join-Path $repo 'setup/owner-profile.json') -Raw | ConvertFrom-Json
$p.GameAddress='127.0.0.1'; $p.DatabasePort=13306; $p.ClientPath=(Join-Path $RuntimeRoot 'test-client'); $p.SourcePath=$RuntimeRoot
$profile = Join-Path $RuntimeRoot 'ci-profile.json'
Write-Utf8 $profile ($p | ConvertTo-Json)
$env:REDMOON_CI_DB_ROOT_PASSWORD=''
$env:REDMOON_CI_GAME_PASSWORD='localfixture123'
# DPAPI must actually work in the same Windows PowerShell used by the owner.
$vaultTest = Join-Path $env:RUNNER_TEMP 'redmoon-dpapi-test'
$secretTest = @{ Value = (New-RandomHex 16) }
Save-LocalSecrets $vaultTest $secretTest $false
$unsealed = Load-LocalSecrets $vaultTest $false
if ($unsealed.Value -ne $secretTest.Value) { throw 'Windows DPAPI round-trip failed.' }
$stage = 'provision'
& (Join-Path $repo 'setup/Setup-RedMoon.ps1') -Root $RuntimeRoot -ProfilePath $profile -MysqlExe $MysqlExe -CI
if (!(Test-Path -LiteralPath (Join-Path $RuntimeRoot '.local/installed.json'))) { throw 'Setup marker was not created.' }
# A re-run must not import/drop anything; it must exit via the existing local marker.
& (Join-Path $repo 'setup/Setup-RedMoon.ps1') -Root $RuntimeRoot -ProfilePath $profile -MysqlExe $MysqlExe -CI
$stage = 'check-configuration'
$secrets = Load-LocalSecrets $RuntimeRoot $true
Export-ServerSecrets $secrets
foreach ($name in @('LoginServer','MsgServer')) {
    & (Join-Path $RuntimeRoot "server/$name.exe") --check-config
    if ($LASTEXITCODE -ne 0) { throw "$name configuration did not validate." }
}
$stage = 'server-start-and-link'
& (Join-Path $repo 'setup/Start-RedMoon.ps1') -Root $RuntimeRoot -CI -TimeoutSeconds 180
# PowerShell functions throw on failure; LASTEXITCODE is meaningful only for native commands.
$stage = 'synthetic-authentication'
$inputJson = @{ Account='ci_player'; Password=$env:REDMOON_CI_GAME_PASSWORD; Port=[int]$p.LoginPort; GamePort=[int]$p.GamePort } | ConvertTo-Json -Compress
$authReport = Invoke-PrivateProcess (Join-Path $RuntimeRoot 'server/RedMoon.Admin.exe') 'probe-login' ($inputJson + "`n")
$auth = $authReport | ConvertFrom-Json
if (!$auth.Authorized -or !$auth.RedirectVerified) { throw 'Synthetic login/redirect did not pass.' }
$stage = 'verify-clean-state'
$finalCount = Invoke-Mysql $MysqlExe $p '' 'SELECT COUNT(*) FROM account;' $p.AccountDatabase
$playerCount = Invoke-Mysql $MysqlExe $p '' 'SELECT COUNT(*) FROM cq_user;' $p.GameDatabase
if ([int]$finalCount -ne 1 -or [int]$playerCount -ne 0) { throw 'Unexpected seed/account state after provisioning.' }
& (Join-Path $repo 'setup/Install-ClientConfig.ps1') -Root $RuntimeRoot -ProfilePath $profile -GenerateOnly -CI
$config = Get-Content -LiteralPath (Join-Path $RuntimeRoot 'client-config/config.json') -Raw | ConvertFrom-Json
if ($config.Servers[0].ServerVersion -ne 5517 -or $config.Servers[0].LoginPort -ne 9958 -or !$config.DisableScreenChanges) { throw 'Generated loader config has wrong values.' }
$stage = 'graceful-stop'
& (Join-Path $repo 'setup/Stop-RedMoon.ps1') -Root $RuntimeRoot -CI
$report = @{
    target='Windows 2022 / Windows PowerShell 5.1'; database_version='MariaDB 10.4.32'; database_port=13306
    source_root=$env:GITHUB_SHA; old_accounts_imported=0; old_players_imported=0; normal_account_created=1
    dpapi_roundtrip=$true; setup_repeat_did_not_reimport=$true; both_config_checks_passed=$true
    synthetic_login_and_redirect=$true; both_servers_ready=$true; inter_server_registered=$true; graceful_shutdown_passed=$true
    loader_json_generated_only=$true; real_client_executed=$false; real_client_login_tested=$false
}
$out = Join-Path $env:RUNNER_TEMP 'fox-evidence/integration-results.json'
Write-Utf8 $out ($report | ConvertTo-Json -Depth 6)
Write-Host 'WINDOWS_SETUP_START_STOP_PASS. Real TQ client remains a local verification step.'

} catch {
    $outDir = Join-Path $env:RUNNER_TEMP 'fox-evidence'
    [IO.Directory]::CreateDirectory($outDir) | Out-Null
    $message = $_.Exception.Message
    foreach ($name in @('REDMOON_CI_GAME_PASSWORD','REDMOON_DB_PASSWORD','REDMOON_AUTH_PASSWORD','REDMOON_TRANSFER_KEY','REDMOON_TRANSFER_SALT')) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($value) { $message = $message.Replace($value, '[redacted]') }
    }
    $failure = @{ Stage=$stage; Success=$false; ErrorType=$_.Exception.GetType().FullName; Message=$message; Line=$_.InvocationInfo.ScriptLineNumber }
    [IO.File]::WriteAllText((Join-Path $outDir 'integration-results.json'), ($failure | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    Write-Host "::error title=Windows installation stage::$stage failed. See sanitized integration report."
    throw
}
