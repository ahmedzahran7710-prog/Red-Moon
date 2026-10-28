[CmdletBinding()]
param([string]$Root = (Split-Path -Parent $PSScriptRoot), [string]$ProfilePath = (Join-Path $PSScriptRoot 'owner-profile.json'), [switch]$GenerateOnly, [switch]$CI)
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-CiMode ([bool]$CI)
$p = Read-Profile $ProfilePath
$server = [ordered]@{
    ServerName=$p.ServerName; ServerVersion=5517; LoginHost=$p.GameAddress; GameHost=$p.GameAddress
    LoginPort=[int]$p.LoginPort; GamePort=[int]$p.GamePort; ExecutableName='Conquer.exe'
    EnableHostName=$false; Hostname=''; UseDirectX9=$false; ServerNameMemoryAddress=''; ServerIcon='Group2/server1.swf'
    Group=@{ GroupName='RedMoon'; GroupIcon='Group1.swf'; Servers=$null }
}
$config = [ordered]@{
    Servers=@($server); DefaultServer=$server; DebugMode=$false; CloseOnFinish=$false
    HighResolution=$false; FHDResolution=$false; FullScreen=$false; ServernameChange=$false
    DisableAutoFixFlash=$true; Title='Red Moon'; Lang='en'; LicenseKey=''; DisableScreenChanges=$true
    UseCustomDLLs=$false; FPSUnlock=$false
}
$json = $config | ConvertTo-Json -Depth 10
$generated = Join-Path $Root 'client-config/config.json'
Write-Utf8 $generated $json
if ($GenerateOnly) { Write-Host 'Client configuration generated; no client files were changed and no loader was executed.'; return }
$client = $p.ClientPath
if (!(Test-Path -LiteralPath (Join-Path $client 'Conquer.exe'))) { throw 'Conquer.exe is not at the configured client path. No client file was changed.' }
$versionFile = Join-Path $client 'version.dat'
if (!(Test-Path -LiteralPath $versionFile)) { throw 'version.dat is missing; client compatibility cannot be assumed.' }
$versionText = [IO.File]::ReadAllText($versionFile)
if ($versionText.Trim() -ne '5517') { throw 'version.dat does not explicitly contain 5517. Stop and review the actual client, do not overwrite its version.' }
$loader = Join-Path $client 'ConquerLoader.exe'
if (!(Test-Path -LiteralPath $loader)) { throw 'Copy the verified ConquerLoader.exe you obtained from its original publisher into the client folder first. Do not rename MoonQueenLoader to bypass this check.' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($loader)
if ($info.FileVersion -ne '1.0.1.0' -or $info.ProductName -notmatch '^ConquerLoader$') {
    throw 'Loader metadata does not match the reviewed ConquerLoader 1.0.1.0 config schema. No client config was installed. Metadata is not an antivirus/signature check.'
}
if (Test-Path -LiteralPath (Join-Path $client 'config.json.lock')) { throw 'A locked loader configuration exists. It is not deleted or bypassed; review it locally first.' }
$dest = Join-Path $client 'config.json'
if (Test-Path -LiteralPath $dest) {
    $backup = Join-Path $client ('RedMoon-backups/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff') + '/config.json')
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
    Copy-Item -LiteralPath $dest -Destination $backup
}
Write-Utf8 $dest $json
Write-Host 'CLIENT_CONFIG_INSTALLED. Only config.json was written; existing config was backed up.'
Write-Host 'Server.dat, GameServer.ini, Conquer.exe and MoonQueen files were NOT patched. No loader was run; antivirus settings were not changed.'
Write-Host 'Start ConquerLoader yourself, choose RedMoon [5517], and verify an actual login locally.'
