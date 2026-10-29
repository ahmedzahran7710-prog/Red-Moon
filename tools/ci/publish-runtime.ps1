[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Destination, [string]$Rid='win-x64')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$Destination=[IO.Path]::GetFullPath($Destination)
foreach ($relative in @('.local','server/redmoon.stop','server/RedMoon.AccountServer.ini','server/RedMoon.GameServer.ini','server/Login.cfg','server/Shell.ini')) {
    if (Test-Path -LiteralPath (Join-Path $Destination $relative)) { throw 'Publish refuses configured state. Use a NEW staging folder; never overwrite an installed server.' }
}
if ((Test-Path -LiteralPath $Destination) -and @(Get-ChildItem -LiteralPath $Destination -Force | Where-Object { $_.Name -like '.local-migration-*' }).Count -ne 0) { throw 'Preserve interrupted migration state; choose a new publishing destination.' }
$server=Join-Path $Destination 'server'
[IO.Directory]::CreateDirectory($server) | Out-Null
Push-Location (Join-Path $repo 'src')
try {
    foreach ($project in @('RedMoon.AccountServer/RedMoon.AccountServer.csproj','RedMoon.GameServer/RedMoon.GameServer.csproj','RedMoon.Admin/RedMoon.Admin.csproj')) {
        & dotnet publish $project -c Release -r $Rid --self-contained true -o $server --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $project" }
    }
} finally { Pop-Location }
if (Test-Path -LiteralPath (Join-Path $server 'libeay32.dll')) { throw 'Untrusted upstream native library must not be packaged.' }
