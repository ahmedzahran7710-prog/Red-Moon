$ErrorActionPreference = 'Stop'
# Official MariaDB 10.4.32 Windows archive; checksum obtained from the publisher's
# https://archive.mariadb.org/mariadb-10.4.32/winx64-packages/sha256sums.txt
$url = 'https://archive.mariadb.org/mariadb-10.4.32/winx64-packages/mariadb-10.4.32-winx64.zip'
$sha = 'c7239062b1e491c8292ed53f38da633f0642c9e5b78a08673fdb4919ee7194bc'
$archive = Join-Path $env:RUNNER_TEMP 'mariadb-10.4.32.zip'
Invoke-WebRequest -Uri $url -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sha) { throw 'MariaDB publisher checksum differs.' }
$unpack = Join-Path $env:RUNNER_TEMP 'mariadb-official'
Expand-Archive -LiteralPath $archive -DestinationPath $unpack
$home = (Get-ChildItem -LiteralPath $unpack -Directory | Select-Object -First 1).FullName
$data = Join-Path $env:RUNNER_TEMP 'mariadb-ci-data'
$install = Join-Path $home 'bin/mysql_install_db.exe'
& $install "--datadir=$data" --port=13306
if ($LASTEXITCODE -ne 0) { throw 'Disposable MariaDB initialization failed.' }
$exe = Join-Path $home 'bin/mysqld.exe'
$process = Start-Process -FilePath $exe -ArgumentList @('--no-defaults',"--basedir=`"$home`"","--datadir=`"$data`"",'--port=13306','--bind-address=127.0.0.1','--skip-name-resolve','--console') -PassThru -RedirectStandardOutput (Join-Path $env:RUNNER_TEMP 'maria.stdout.log') -RedirectStandardError (Join-Path $env:RUNNER_TEMP 'maria.stderr.log')
$mysql = Join-Path $home 'bin/mysql.exe'
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
