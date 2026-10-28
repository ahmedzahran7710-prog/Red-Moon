$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$stage = 'download'
try {
# Official MariaDB 10.4.32 Windows archive; checksum obtained from the publisher's
# https://archive.mariadb.org/mariadb-10.4.32/winx64-packages/sha256sums.txt
$url = 'https://archive.mariadb.org/mariadb-10.4.32/winx64-packages/mariadb-10.4.32-winx64.zip'
$sha = 'c7239062b1e491c8292ed53f38da633f0642c9e5b78a08673fdb4919ee7194bc'
$archive = Join-Path $env:RUNNER_TEMP 'mariadb-10.4.32.zip'
Invoke-WebRequest -Uri $url -OutFile $archive -MaximumRetryCount 2 -RetryIntervalSec 3
$stage = 'checksum'
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sha) { throw 'MariaDB publisher checksum differs.' }
$unpack = Join-Path $env:RUNNER_TEMP 'mariadb-official'
$stage = 'extract'
Expand-Archive -LiteralPath $archive -DestinationPath $unpack
$mariaHome = (Get-ChildItem -LiteralPath $unpack -Directory | Select-Object -First 1).FullName
$data = Join-Path $env:RUNNER_TEMP 'mariadb-ci-data'
$install = Join-Path $mariaHome 'bin/mysql_install_db.exe'
$stage = 'initialize'
& $install "--datadir=$data" --port=13306
if ($LASTEXITCODE -ne 0) { throw 'Disposable MariaDB initialization failed.' }
$exe = Join-Path $mariaHome 'bin/mysqld.exe'
$stage = 'start'
$process = Start-Process -FilePath $exe -ArgumentList @('--no-defaults',"--basedir=`"$mariaHome`"","--datadir=`"$data`"",'--port=13306','--bind-address=127.0.0.1','--skip-name-resolve','--console') -PassThru -RedirectStandardOutput (Join-Path $env:RUNNER_TEMP 'maria.stdout.log') -RedirectStandardError (Join-Path $env:RUNNER_TEMP 'maria.stderr.log')
$mysql = Join-Path $mariaHome 'bin/mysql.exe'
$stage = 'wait-ready'
$deadline = [DateTime]::UtcNow.AddSeconds(60)
$ready = $false
while ([DateTime]::UtcNow -lt $deadline) {
    $process.Refresh()
    if ($process.HasExited) { throw 'Disposable MariaDB exited before readiness.' }
    & $mysql --host=127.0.0.1 --port=13306 --protocol=TCP --user=root --batch --skip-column-names '--execute=SELECT 1;' 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 1
}
if (!$ready) { throw 'Disposable MariaDB readiness timed out.' }
"REDMOON_MYSQL_EXE=$mysql" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
"REDMOON_MARIADB_PID=$($process.Id)" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
Write-Host 'DISPOSABLE_MARIADB_READY 10.4.32, loopback port 13306; no existing service modified.'

} catch {
    $report = @{ Stage=$stage; Success=$false; ErrorType=$_.Exception.GetType().FullName; Message=$_.Exception.Message }
    $dir = Join-Path $env:RUNNER_TEMP 'fox-evidence'
    [IO.Directory]::CreateDirectory($dir) | Out-Null
    [IO.File]::WriteAllText((Join-Path $dir 'bootstrap-results.json'), ($report | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    Write-Host "::error title=MariaDB bootstrap stage::$stage failed: $($_.Exception.GetType().Name)"
    throw
}
