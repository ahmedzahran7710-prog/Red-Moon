#requires -Version 5.1
[CmdletBinding()]
param([string]$Root = '')
# Resolve defaults only after parameter binding. Windows PowerShell 5.1 may
# not populate script automatic variables while evaluating param() defaults.
$scriptFile = $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptFile)) {
    throw 'Run this saved script file, not its pasted contents; its directory is required.'
}
$scriptDirectory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($scriptFile))
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = [IO.Directory]::GetParent($scriptDirectory).FullName
}
. (Join-Path $scriptDirectory 'Common.ps1')
if (!$script:OnWindows) { throw 'This local build entry point creates a Windows x64 bundle.' }
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET SDK 10.0.403, or use the self-contained Windows bundle instead.' }
if (!(Get-Command python -ErrorAction SilentlyContinue)) { throw 'Building from source needs Python 3.9+. The ready-to-run bundle needs neither Python nor an installed .NET SDK.' }
if (Test-Path -LiteralPath (Join-Path $Root '.local/installed.json')) { throw 'Do not rebuild over an installed/running server. Build into a fresh source checkout and preserve the local vault/data.' }
Push-Location (Join-Path $Root 'src')
try {
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdk -notlike '10.0.*') { throw 'The pinned .NET 10 SDK was not selected.' }
    foreach ($project in @('FoxConquer5517/LoginServer/LoginServer.csproj','FoxConquer5517/MsgServer/MsgServer.csproj','RedMoon.Admin/RedMoon.Admin.csproj')) {
        & dotnet publish $project -c Release -r win-x64 --self-contained true -o (Join-Path $Root 'server')
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed; do not use partial outputs.' }
    }
} finally { Pop-Location }
Push-Location $Root
try {
    & python tools/fox_baseline.py materialize
    if ($LASTEXITCODE -ne 0) { throw 'Upstream integrity check failed.' }
    $data = Join-Path $Root ('.cache/local-data-' + [Guid]::NewGuid().ToString('N'))
    & python tools/prepare_runtime_data.py --source .cache/fox5517/original --dest $data
    if ($LASTEXITCODE -ne 0) { throw 'Clean data preparation failed.' }
    Copy-Item -Path (Join-Path $data 'runtime-data/*') -Destination (Join-Path $Root 'server') -Recurse
    Copy-Item -LiteralPath (Join-Path $data 'database') -Destination (Join-Path $Root 'database') -Recurse
    Copy-Item -LiteralPath (Join-Path $data 'data-provenance.json') -Destination (Join-Path $Root 'data-provenance.json')
} finally { Pop-Location }
Write-Host 'BUILD_READY. Run Setup-RedMoon.cmd once; then Start-RedMoon.cmd.'
