param(
    [ValidateSet("SetupAll", "ServerLauncher", "Check", "Offline", "Hamachi", "StartAll", "StopAll", "StaffBadge")]
    [string]$Mode = "SetupAll",
    [string]$HamachiIP = "25.21.212.130"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# ============================================================================
# MoonQueen Unified Setup & Server Launcher (V6192 Pure 1.0x)
# Brand: MoonQueen | Developer: ahmedzahran (احمد زهران)
# Protected Developer Rights Seal — Do Not Modify
# ============================================================================
$MQ_BRAND    = "MoonQueen"
$MQ_DEV_EN   = "ahmedzahran"
$MQ_DEV_AR   = "احمد زهران"
$MQ_SEAL     = "f00c83c7789c6b18a7ce5a5cd5b905c85fd41e20cd2e88806c3a92a7a96348b9"
$MQ_REALM_ID = "e418a91a-9574-4962-842b-4729238d3965"

function Assert-DeveloperSeal {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes("$MQ_BRAND|$MQ_DEV_EN|$MQ_DEV_AR")
        $actual = -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") })
    } finally {
        $sha.Dispose()
    }
    if ($MQ_BRAND -ne "MoonQueen" -or $MQ_DEV_EN -ne "ahmedzahran" -or $actual -ne $MQ_SEAL) {
        Write-Host "[FATAL] Tamper detected: MoonQueen developer seal (ahmedzahran) is immutable." -ForegroundColor Red
        exit 99
    }
}

Assert-DeveloperSeal

$ScriptDir      = $PSScriptRoot
$RepoRoot       = Split-Path -Parent $ScriptDir
$LocalStateFile = Join-Path $ScriptDir "launcher.local.json"

function Write-Banner([string]$Subtitle) {
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor DarkRed
    Write-Host "  MoonQueen V6192 - Developed by ahmedzahran" -ForegroundColor Yellow
    Write-Host "  $Subtitle" -ForegroundColor Cyan
    Write-Host "======================================================================" -ForegroundColor DarkRed
    Write-Host ""
}

function Write-StepHeader([string]$StepCode, [string]$Title) {
    Write-Host ""
    Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkCyan
    Write-Host "  $StepCode $Title" -ForegroundColor Cyan
    Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkCyan
}

function Load-LauncherState {
    if (Test-Path -LiteralPath $LocalStateFile) {
        try {
            return Get-Content -LiteralPath $LocalStateFile -Raw -Encoding UTF8 | ConvertFrom-Json
        } catch {
            return [pscustomobject]@{}
        }
    }
    return [pscustomobject]@{}
}

function Save-LauncherState([hashtable]$Values) {
    $current = @{}
    $loaded = Load-LauncherState
    if ($null -ne $loaded) {
        foreach ($prop in $loaded.PSObject.Properties) {
            $current[$prop.Name] = $prop.Value
        }
    }
    foreach ($k in $Values.Keys) {
        $current[$k] = $Values[$k]
    }
    $json = $current | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($LocalStateFile, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function New-CryptoRandomString([int]$Length) {
    $chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"
    $bytes = New-Object byte[] $Length
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    } finally {
        $rng.Dispose()
    }
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $Length; $i++) {
        [void]$sb.Append($chars[$bytes[$i] % $chars.Length])
    }
    return $sb.ToString()
}

function Read-PlainFromSecurePrompt([string]$PromptText) {
    $sec = Read-Host $PromptText -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
    try {
        return [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    } finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Resolve-BinDirectory([bool]$CreateIfMissing = $false) {
    $candidates = @(
        (Join-Path $RepoRoot "src\Bin\net7.0"),
        (Join-Path $RepoRoot "src\Bin\Release\net7.0"),
        (Join-Path $RepoRoot "src\Bin\Debug\net7.0")
    )
    foreach ($dir in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $dir "MoonQueen.Game.exe")) {
            return $dir
        }
    }
    foreach ($dir in $candidates) {
        if (Test-Path -LiteralPath $dir) {
            return $dir
        }
    }
    if ($CreateIfMissing) {
        New-Item -ItemType Directory -Path $candidates[0] -Force | Out-Null
    }
    return $candidates[0]
}

function Find-MySqlExe([bool]$PromptIfMissing = $false) {
    $state = Load-LauncherState
    $paths = @()
    if ($null -ne $state -and -not [string]::IsNullOrWhiteSpace([string]$state.MySqlExePath)) {
        $paths += [string]$state.MySqlExePath
    }
    $paths += @(
        "C:\xampp\mysql\bin\mysql.exe",
        "D:\xampp\mysql\bin\mysql.exe",
        "E:\xampp\mysql\bin\mysql.exe",
        "F:\xampp\mysql\bin\mysql.exe"
    )
    $mariaDirs = Get-ChildItem -Path "C:\Program Files\MariaDB*" -Directory -ErrorAction SilentlyContinue
    foreach ($md in $mariaDirs) {
        $paths += (Join-Path $md.FullName "bin\mysql.exe")
    }
    foreach ($p in $paths) {
        if (-not [string]::IsNullOrWhiteSpace($p) -and (Test-Path -LiteralPath $p)) {
            return $p
        }
    }
    $cmd = Get-Command mysql -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { return $cmd.Source }
    if ($PromptIfMissing) {
        Write-Host "[?] mysql.exe was not found automatically." -ForegroundColor Yellow
        $entered = (Read-Host "Enter the full path to mysql.exe (example: C:\xampp\mysql\bin\mysql.exe)").Trim('"').Trim()
        if (-not [string]::IsNullOrWhiteSpace($entered) -and (Test-Path -LiteralPath $entered)) {
            Save-LauncherState @{ MySqlExePath = $entered }
            return $entered
        }
    }
    return $null
}

function Find-PhpExe {
    $paths = @(
        "C:\xampp\php\php.exe",
        "D:\xampp\php\php.exe",
        "E:\xampp\php\php.exe",
        "F:\xampp\php\php.exe"
    )
    foreach ($p in $paths) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    $cmd = Get-Command php -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { return $cmd.Source }
    return $null
}

function Test-TcpPort([string]$HostAddr, [int]$Port, [int]$TimeoutMs = 900) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $iar = $client.BeginConnect($HostAddr, $Port, $null, $null)
        if (-not $iar.AsyncWaitHandle.WaitOne($TimeoutMs, $false)) {
            return $false
        }
        $client.EndConnect($iar)
        return $true
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

function Wait-TcpPort([string]$HostAddr, [int]$Port, [int]$MaxSeconds = 12) {
    for ($i = 0; $i -lt ($MaxSeconds * 2); $i++) {
        if (Test-TcpPort $HostAddr $Port 450) {
            return $true
        }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Read-JsonConfigSafe([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Write-JsonUtf8NoBom([string]$Path, $Object, [int]$Depth = 8) {
    $json = $Object | ConvertTo-Json -Depth $Depth
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Invoke-MySqlQuery([string]$MySqlExe, [string]$HostAddr, [int]$Port, [string]$User, [string]$Password, [string]$Database, [string]$Sql) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $MySqlExe
    $dbArg = ""
    if (-not [string]::IsNullOrWhiteSpace($Database)) {
        $dbArg = ' -D "' + $Database + '"'
    }
    $psi.Arguments = '-h "' + $HostAddr + '" -P ' + $Port + ' -u "' + $User + '" --default-character-set=utf8mb4 -N -B' + $dbArg + ' -e "' + $Sql + '"'
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    if (-not [string]::IsNullOrEmpty($Password)) {
        $psi.EnvironmentVariables["MYSQL_PWD"] = $Password
    }
    $proc = [System.Diagnostics.Process]::Start($psi)
    $outTask = $proc.StandardOutput.ReadToEndAsync()
    $errTask = $proc.StandardError.ReadToEndAsync()
    $proc.WaitForExit()
    $stdout = $outTask.GetAwaiter().GetResult()
    $stderr = $errTask.GetAwaiter().GetResult()
    return [pscustomobject]@{
        ExitCode = $proc.ExitCode
        Output   = $stdout.Trim()
        Error    = $stderr.Trim()
    }
}

function Invoke-MySqlImportFile([string]$MySqlExe, [string]$HostAddr, [int]$Port, [string]$User, [string]$Password, [string]$SqlFilePath) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $MySqlExe
    $psi.Arguments = '-h "' + $HostAddr + '" -P ' + $Port + ' -u "' + $User + '" --default-character-set=utf8mb4 --binary-mode=1'
    $psi.RedirectStandardInput  = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    if (-not [string]::IsNullOrEmpty($Password)) {
        $psi.EnvironmentVariables["MYSQL_PWD"] = $Password
    }
    $proc = [System.Diagnostics.Process]::Start($psi)
    $outTask = $proc.StandardOutput.ReadToEndAsync()
    $errTask = $proc.StandardError.ReadToEndAsync()
    $fs = [System.IO.File]::OpenRead($SqlFilePath)
    try {
        $fs.CopyTo($proc.StandardInput.BaseStream)
    } finally {
        $fs.Dispose()
        $proc.StandardInput.Close()
    }
    $proc.WaitForExit()
    $stdout = $outTask.GetAwaiter().GetResult()
    $stderr = $errTask.GetAwaiter().GetResult()
    return [pscustomobject]@{
        ExitCode = $proc.ExitCode
        Output   = $stdout.Trim()
        Error    = $stderr.Trim()
    }
}

function Invoke-MySqlStdinScript([string]$MySqlExe, [string]$HostAddr, [int]$Port, [string]$User, [string]$Password, [string]$Database, [string]$SqlScript) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $MySqlExe
    $dbArg = ""
    if (-not [string]::IsNullOrWhiteSpace($Database)) {
        $dbArg = ' -D "' + $Database + '"'
    }
    $psi.Arguments = '-h "' + $HostAddr + '" -P ' + $Port + ' -u "' + $User + '" --default-character-set=utf8mb4' + $dbArg
    $psi.RedirectStandardInput  = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    if (-not [string]::IsNullOrEmpty($Password)) {
        $psi.EnvironmentVariables["MYSQL_PWD"] = $Password
    }
    $proc = [System.Diagnostics.Process]::Start($psi)
    $outTask = $proc.StandardOutput.ReadToEndAsync()
    $errTask = $proc.StandardError.ReadToEndAsync()
    $utf8Bytes = [System.Text.Encoding]::UTF8.GetBytes($SqlScript)
    try {
        $proc.StandardInput.BaseStream.Write($utf8Bytes, 0, $utf8Bytes.Length)
    } finally {
        $proc.StandardInput.Close()
    }
    $proc.WaitForExit()
    $stdout = $outTask.GetAwaiter().GetResult()
    $stderr = $errTask.GetAwaiter().GetResult()
    return [pscustomobject]@{
        ExitCode = $proc.ExitCode
        Output   = $stdout.Trim()
        Error    = $stderr.Trim()
    }
}

function Protect-RealmCredential([string]$PlainText) {
    $keyHex = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("OTEzMTFhMjFlMDI4NWQ0NjY3N2FhNzVkNDRjNzI3YWM="))
    $key = New-Object byte[] ($keyHex.Length / 2)
    for ($i = 0; $i -lt $keyHex.Length; $i += 2) {
        $key[$i / 2] = [System.Convert]::ToByte($keyHex.Substring($i, 2), 16)
    }
    $aes = [System.Security.Cryptography.Aes]::Create()
    try {
        $aes.Key = $key
        $aes.IV  = New-Object byte[] 16
        $ms = New-Object System.IO.MemoryStream
        $cs = New-Object System.Security.Cryptography.CryptoStream($ms, $aes.CreateEncryptor(), [System.Security.Cryptography.CryptoStreamMode]::Write)
        $sw = New-Object System.IO.StreamWriter($cs)
        $sw.Write($PlainText)
        $sw.Dispose()
        $cs.Dispose()
        return [System.Convert]::ToBase64String($ms.ToArray())
    } finally {
        $aes.Dispose()
    }
}

function Get-AccountPasswordHash([string]$PlainPassword, [string]$Salt) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha.ComputeHash([System.Text.Encoding]::ASCII.GetBytes($PlainPassword + $Salt))
        return -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
    } finally {
        $sha.Dispose()
    }
}

function Resolve-ClientDirectory([bool]$PromptIfMissing = $false, [bool]$AllowChange = $false) {
    $state = Load-LauncherState
    $savedDir = $null
    if ($null -ne $state -and -not [string]::IsNullOrWhiteSpace([string]$state.ClientDirectory)) {
        $savedDir = [string]$state.ClientDirectory
    }

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($savedDir)) { $candidates += $savedDir }
    $parentOfRepo = Split-Path -Parent $RepoRoot
    if (-not [string]::IsNullOrWhiteSpace($parentOfRepo)) {
        $candidates += (Join-Path $parentOfRepo "MoonQueen-Client")
    }
    $candidates += "F:\AhmedZahran\MoonQueen-Client"

    $detected = $null
    foreach ($c in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($c) -and (Test-Path -LiteralPath $c)) {
            $detected = $c
            break
        }
    }

    if ($AllowChange -and $null -ne $detected) {
        $inp = (Read-Host "Client folder [$detected] (Enter to confirm, or enter a different path)").Trim('"').Trim()
        if (-not [string]::IsNullOrWhiteSpace($inp)) {
            if (Test-Path -LiteralPath $inp) {
                Save-LauncherState @{ ClientDirectory = $inp }
                return $inp
            } else {
                Write-Host "[!] Path not found; using [$detected]." -ForegroundColor Yellow
            }
        }
        Save-LauncherState @{ ClientDirectory = $detected }
        return $detected
    }

    if ($null -ne $detected) {
        return $detected
    }

    if ($PromptIfMissing) {
        Write-Host "[?] Enter your Conquer 6192 client folder (example: F:\AhmedZahran\MoonQueen-Client)" -ForegroundColor Cyan
        $entered = (Read-Host "Client folder (or press Enter to skip)").Trim('"').Trim()
        if (-not [string]::IsNullOrWhiteSpace($entered)) {
            if (Test-Path -LiteralPath $entered) {
                Save-LauncherState @{ ClientDirectory = $entered }
                return $entered
            }
            Write-Host "[!] Folder not found: $entered. Continuing without copying client assets." -ForegroundColor Yellow
        }
    }
    return $null
}

function Ensure-LuaAssets([string]$BinDir) {
    $srcLua   = Join-Path $RepoRoot "RequiredAssetsForRun\lua"
    $srcIni   = Join-Path $srcLua "lua.ini"
    $dstLua   = Join-Path $BinDir "lua"
    $dstIni   = Join-Path $dstLua "lua.ini"
    $dstBasic = Join-Path $dstLua "BasicData"
    if (Test-Path -LiteralPath $srcIni) {
        $needsSync = (-not (Test-Path -LiteralPath $dstIni)) -or (-not (Test-Path -LiteralPath $dstBasic))
        if (-not $needsSync) {
            $rawIni = Get-Content -LiteralPath $dstIni -Raw -ErrorAction SilentlyContinue
            if ($rawIni -match "todo\.lua" -or $rawIni -match "V100\\") {
                $needsSync = $true
            }
        }
        if ($needsSync) {
            New-Item -ItemType Directory -Path $dstLua -Force | Out-Null
            Copy-Item -Path (Join-Path $srcLua "*") -Destination $dstLua -Recurse -Force
            Write-Host "[OK] Copied RequiredAssetsForRun\lua scripts to $dstLua." -ForegroundColor Green
        } else {
            Write-Host "[OK] Server Lua scripts are present." -ForegroundColor Green
        }
    }
}

function Sync-ClientAndServerAssets([string]$BinDir, [string]$ClientDir, [string]$TargetIP) {
    Ensure-LuaAssets $BinDir

    if ([string]::IsNullOrWhiteSpace($ClientDir) -or -not (Test-Path -LiteralPath $ClientDir)) {
        Write-Host "[i] No client folder selected; only Lua assets were prepared." -ForegroundColor Gray
        return
    }

    # 1. Copy ini & map from V6192 client to Server BinDir if missing
    $srcIniDir  = Join-Path $ClientDir "ini"
    $dstIniDir  = Join-Path $BinDir "ini"
    $dstGameMap = Join-Path $dstIniDir "GameMap.dat"

    if ((Test-Path -LiteralPath $srcIniDir) -and -not (Test-Path -LiteralPath $dstGameMap)) {
        Write-Host "[+] Copying client ini files to $dstIniDir..." -ForegroundColor Cyan
        New-Item -ItemType Directory -Path $dstIniDir -Force | Out-Null
        Copy-Item -Path (Join-Path $srcIniDir "*") -Destination $dstIniDir -Recurse -Force
        Write-Host "[OK] Copied client ini files." -ForegroundColor Green
    } elseif (Test-Path -LiteralPath $dstGameMap) {
        Write-Host "[OK] ini\GameMap.dat exists in the server folder." -ForegroundColor Green
    }

    $srcMapDir = Join-Path $ClientDir "map"
    $dstMapDir = Join-Path $BinDir "map"
    $hasDmaps  = (Test-Path -LiteralPath $dstMapDir) -and ($null -ne (Get-ChildItem -Path $dstMapDir -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1))
    if ((Test-Path -LiteralPath $srcMapDir) -and -not $hasDmaps) {
        Write-Host "[+] Copying client map folder to $dstMapDir..." -ForegroundColor Cyan
        New-Item -ItemType Directory -Path $dstMapDir -Force | Out-Null
        Copy-Item -Path (Join-Path $srcMapDir "*") -Destination $dstMapDir -Recurse -Force
        Write-Host "[OK] Copied client map folder." -ForegroundColor Green
    } elseif ($hasDmaps) {
        Write-Host "[OK] Server map folder contains files." -ForegroundColor Green
    }

    # 2. Copy built MoonQueen.exe & ConquerCipherHook.dll from لورد الكلينت if available
    $loaderRoot    = Join-Path $RepoRoot "لورد الكلينت"
    $loaderRelDir1 = Join-Path $loaderRoot "ConquerLoader\Release"
    $loaderRelDir2 = Join-Path $loaderRoot "ConquerLoader\ConquerLoader\bin\Release"

    foreach ($candExe in @((Join-Path $loaderRelDir1 "MoonQueen.exe"), (Join-Path $loaderRelDir2 "MoonQueen.exe"))) {
        if (Test-Path -LiteralPath $candExe) {
            $exeSrcDir = Split-Path -Parent $candExe
            Copy-Item -LiteralPath $candExe -Destination (Join-Path $ClientDir "MoonQueen.exe") -Force
            foreach ($dllName in @("CLCore.dll", "CLServer.dll", "COHook.dll", "OpenConquerHook.dll", "MetroFramework.dll", "MetroFramework.Design.dll", "MetroFramework.Fonts.dll", "Newtonsoft.Json.dll", "SimpleInjector.dll")) {
                $dllSrc = Join-Path $exeSrcDir $dllName
                if (Test-Path -LiteralPath $dllSrc) {
                    Copy-Item -LiteralPath $dllSrc -Destination (Join-Path $ClientDir $dllName) -Force
                }
            }
            Write-Host "[OK] Copied the built MoonQueen launcher and available libraries to the client." -ForegroundColor Green
            break
        }
    }

    foreach ($candHook in @((Join-Path $loaderRelDir2 "ConquerCipherHook.dll"), (Join-Path $loaderRelDir1 "ConquerCipherHook.dll"))) {
        if (Test-Path -LiteralPath $candHook) {
            Copy-Item -LiteralPath $candHook -Destination (Join-Path $ClientDir "ConquerCipherHook.dll") -Force
            Write-Host "[OK] Copied ConquerCipherHook.dll to the client." -ForegroundColor Green
            break
        }
    }

    # 3. Sync config.json in Client folder (LoginHost + GameHost in Servers[*] & DefaultServer) and remove stale config.json.lock
    $templateCfgPath = Join-Path $loaderRoot "config.json"
    $clientCfgPath   = Join-Path $ClientDir "config.json"
    $clientLockPath  = Join-Path $ClientDir "config.json.lock"

    if (Test-Path -LiteralPath $clientLockPath) {
        Remove-Item -LiteralPath $clientLockPath -Force
        Write-Host "[OK] Removed the old config.json.lock from the client." -ForegroundColor Green
    }

    $cfgObj = Read-JsonConfigSafe $clientCfgPath
    if ($null -eq $cfgObj -or $null -eq $cfgObj.Servers -or $null -eq $cfgObj.DefaultServer) {
        $cfgObj = Read-JsonConfigSafe $templateCfgPath
    }

    if ($null -ne $cfgObj) {
        if ($null -ne $cfgObj.Servers) {
            foreach ($srv in $cfgObj.Servers) {
                $srv.ServerName = "MoonQueen"
                $srv.LoginHost  = $TargetIP
                $srv.GameHost   = $TargetIP
                $srv.LoginPort  = 9958
                $srv.GamePort   = 5816
            }
        }
        if ($null -ne $cfgObj.DefaultServer) {
            $cfgObj.DefaultServer.ServerName = "MoonQueen"
            $cfgObj.DefaultServer.LoginHost  = $TargetIP
            $cfgObj.DefaultServer.GameHost   = $TargetIP
            $cfgObj.DefaultServer.LoginPort  = 9958
            $cfgObj.DefaultServer.GamePort   = 5816
        }
        $cfgObj.Title = "[MoonQueen] Official Client Loader - Developed by ahmedzahran"
        Write-JsonUtf8NoBom $clientCfgPath $cfgObj 8
        Write-Host "[OK] Updated client config.json (LoginHost and GameHost = $TargetIP | Ports = 9958 / 5816)." -ForegroundColor Green
    }
}

function Get-ClientConfiguredIp([string]$ClientDir) {
    if ([string]::IsNullOrWhiteSpace($ClientDir)) { return $null }
    $cfg = Read-JsonConfigSafe (Join-Path $ClientDir "config.json")
    if ($null -eq $cfg) { return $null }
    if ($null -ne $cfg.DefaultServer -and -not [string]::IsNullOrWhiteSpace([string]$cfg.DefaultServer.LoginHost)) {
        return [string]$cfg.DefaultServer.LoginHost
    }
    if ($null -ne $cfg.Servers -and $cfg.Servers.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$cfg.Servers[0].LoginHost)) {
        return [string]$cfg.Servers[0].LoginHost
    }
    return $null
}

function Resolve-VerifiedMySqlCredentials([string]$MySqlExe, [string]$BinDir) {
    $loginCfg = Read-JsonConfigSafe (Join-Path $BinDir "MoonQueen.Login.Config.local.json")
    $dbHost = if ($null -ne $loginCfg -and $null -ne $loginCfg.Database -and $loginCfg.Database.Hostname) { [string]$loginCfg.Database.Hostname } else { "127.0.0.1" }
    $dbPort = if ($null -ne $loginCfg -and $null -ne $loginCfg.Database -and $loginCfg.Database.Port) { [int]$loginCfg.Database.Port } else { 3306 }
    $dbUser = if ($null -ne $loginCfg -and $null -ne $loginCfg.Database -and $loginCfg.Database.Username) { [string]$loginCfg.Database.Username } else { "root" }
    $dbPass = if ($null -ne $loginCfg -and $null -ne $loginCfg.Database -and $null -ne $loginCfg.Database.Password) { [string]$loginCfg.Database.Password } else { "" }

    if ($dbPass -match "اكتب") { $dbPass = "" }

    # Try saved/empty password first without bothering the owner if it already works!
    if ($null -ne $loginCfg -and $null -ne $loginCfg.Database) {
        $trySaved = Invoke-MySqlQuery $MySqlExe $dbHost $dbPort $dbUser $dbPass "" "SELECT VERSION();"
        if ($trySaved.ExitCode -eq 0) {
            Write-Host "[OK] Connected to MariaDB / MySQL using saved local settings ($($trySaved.Output))." -ForegroundColor Green
            return [pscustomobject]@{
                Host     = $dbHost
                Port     = $dbPort
                User     = $dbUser
                Password = $dbPass
            }
        }
    } else {
        # Also test empty root password common on fresh XAMPP before prompting
        $tryEmpty = Invoke-MySqlQuery $MySqlExe $dbHost $dbPort $dbUser "" "" "SELECT VERSION();"
        if ($tryEmpty.ExitCode -eq 0) {
            $useEmpty = (Read-Host "MySQL user $dbUser works without a password. Press Enter to use it, or enter a password").Trim()
            if ([string]::IsNullOrEmpty($useEmpty)) {
                Write-Host "[OK] Connected to MariaDB / MySQL ($($tryEmpty.Output))." -ForegroundColor Green
                return [pscustomobject]@{
                    Host     = $dbHost
                    Port     = $dbPort
                    User     = $dbUser
                    Password = ""
                }
            }
            $dbPass = $useEmpty
        }
    }

    while ($true) {
        if ([string]::IsNullOrEmpty($dbPass)) {
            $dbPass = Read-PlainFromSecurePrompt "Enter the MySQL password for $dbUser at ${dbHost}:${dbPort}, or press Enter for none"
        }
        $ping = Invoke-MySqlQuery $MySqlExe $dbHost $dbPort $dbUser $dbPass "" "SELECT VERSION();"
        if ($ping.ExitCode -eq 0) {
            Write-Host "[OK] Connected to MariaDB / MySQL version: $($ping.Output)" -ForegroundColor Green
            return [pscustomobject]@{
                Host     = $dbHost
                Port     = $dbPort
                User     = $dbUser
                Password = $dbPass
            }
        }
        Write-Host "[!!] MySQL connection failed: $($ping.Error)" -ForegroundColor Red
        $dbPass = ""
        $uInput = (Read-Host "Press Enter to retry the password for $dbUser, or enter a different MySQL user").Trim()
        if (-not [string]::IsNullOrWhiteSpace($uInput)) {
            $dbUser = $uInput
        }
    }
}

function Ensure-ServerLocalConfigs([string]$BinDir, $DbCreds) {
    $loginPath = Join-Path $BinDir "MoonQueen.Login.Config.local.json"
    $gamePath  = Join-Path $BinDir "MoonQueen.Game.Config.local.json"
    $aiPath    = Join-Path $BinDir "MoonQueen.Ai.Config.local.json"

    $loginCfg = Read-JsonConfigSafe $loginPath
    $gameCfg  = Read-JsonConfigSafe $gamePath
    $aiCfg    = Read-JsonConfigSafe $aiPath

    $sharedKey = if ($null -ne $gameCfg -and $null -ne $gameCfg.Auth -and ([string]$gameCfg.Auth.SharedKey).Length -eq 32) {
        [string]$gameCfg.Auth.SharedKey
    } elseif ($null -ne $loginCfg -and $null -ne $loginCfg.Auth -and ([string]$loginCfg.Auth.SharedKey).Length -eq 32) {
        [string]$loginCfg.Auth.SharedKey
    } else {
        New-CryptoRandomString 32
    }

    $sharedIV = if ($null -ne $gameCfg -and $null -ne $gameCfg.Auth -and ([string]$gameCfg.Auth.SharedIV).Length -eq 16) {
        [string]$gameCfg.Auth.SharedIV
    } elseif ($null -ne $loginCfg -and $null -ne $loginCfg.Auth -and ([string]$loginCfg.Auth.SharedIV).Length -eq 16) {
        [string]$loginCfg.Auth.SharedIV
    } else {
        New-CryptoRandomString 16
    }

    $realmId = if ($null -ne $gameCfg -and $null -ne $gameCfg.Realm -and -not [string]::IsNullOrWhiteSpace([string]$gameCfg.Realm.ServerId)) {
        [string]$gameCfg.Realm.ServerId
    } else {
        $MQ_REALM_ID
    }

    $realmUser = if ($null -ne $gameCfg -and $null -ne $gameCfg.Realm -and -not [string]::IsNullOrWhiteSpace([string]$gameCfg.Realm.Username) -and [string]$gameCfg.Realm.Username -ne "yD3Ni6tMW1NNU1QH") {
        [string]$gameCfg.Realm.Username
    } else {
        "MoonQueenRealm_" + (New-CryptoRandomString 6)
    }

    $realmPass = if ($null -ne $gameCfg -and $null -ne $gameCfg.Realm -and -not [string]::IsNullOrWhiteSpace([string]$gameCfg.Realm.Password) -and [string]$gameCfg.Realm.Password -ne "jETqqIKi9LuFvOgu" -and [string]$gameCfg.Realm.Password -notmatch "اكتب") {
        [string]$gameCfg.Realm.Password
    } else {
        New-CryptoRandomString 24
    }

    $aiUser = if ($null -ne $gameCfg -and $null -ne $gameCfg.Ai -and -not [string]::IsNullOrWhiteSpace([string]$gameCfg.Ai.Username) -and [string]$gameCfg.Ai.Username -ne "yD3Ni6tMW1NNU1QH") {
        [string]$gameCfg.Ai.Username
    } else {
        New-CryptoRandomString 16
    }

    $aiPass = if ($null -ne $gameCfg -and $null -ne $gameCfg.Ai -and -not [string]::IsNullOrWhiteSpace([string]$gameCfg.Ai.Password) -and [string]$gameCfg.Ai.Password -ne "jETqqIKi9LuFvOgu" -and [string]$gameCfg.Ai.Password -notmatch "اكتب") {
        [string]$gameCfg.Ai.Password
    } else {
        New-CryptoRandomString 16
    }

    # The AI login packet has three fixed 16-byte CP1252 fields. Never truncate
    # credentials: regenerate invalid old setup-generated values on both sides.
    foreach ($field in @('aiUser', 'aiPass')) {
        $value = Get-Variable -Name $field -ValueOnly
        if ($value -notmatch '^[\x21-\x7E]{1,16}$') {
            Set-Variable -Name $field -Value (New-CryptoRandomString 16)
            Write-Host "[WARNING] Incompatible AI credentials were regenerated and synchronized. Restart Game and AI together." -ForegroundColor Yellow
        }
    }

    $maxOnline = if ($null -ne $gameCfg -and $null -ne $gameCfg.Realm -and [int]$gameCfg.Realm.MaxOnlinePlayers -gt 0) {
        [int]$gameCfg.Realm.MaxOnlinePlayers
    } else {
        500
    }

    $processors = if ($null -ne $gameCfg -and $null -ne $gameCfg.Realm -and [int]$gameCfg.Realm.Processors -gt 0) {
        [int]$gameCfg.Realm.Processors
    } else {
        1
    }

    $loginOut = [ordered]@{
        Auth = [ordered]@{
            SharedKey = $sharedKey
            SharedIV  = $sharedIV
        }
        Network = [ordered]@{
            IPAddress = "0.0.0.0"
            Port      = 9958
            MaxConn   = 500
        }
        RealmNetwork = [ordered]@{
            IPAddress = "127.0.0.1"
            Port      = 9865
            MaxConn   = 10
        }
        Authentication = [ordered]@{ Url = "http://127.0.0.1" }
        Realm          = [ordered]@{ Url = "http://127.0.0.1" }
        Account        = [ordered]@{ Url = "http://127.0.0.1" }
        Database = [ordered]@{
            Hostname = $DbCreds.Host
            Schema   = "moonqueen_account"
            Username = $DbCreds.User
            Password = $DbCreds.Password
            Port     = $DbCreds.Port
        }
    }

    $gameOut = [ordered]@{
        Auth = [ordered]@{
            SharedKey = $sharedKey
            SharedIV  = $sharedIV
        }
        Database = [ordered]@{
            Hostname = $DbCreds.Host
            Schema   = "moonqueen_game"
            Username = $DbCreds.User
            Password = $DbCreds.Password
            Port     = $DbCreds.Port
        }
        Realm = [ordered]@{
            ServerId         = $realmId
            Name             = "MoonQueen"
            IPAddress        = "0.0.0.0"
            Port             = 5816
            MaxOnlinePlayers = $maxOnline
            Username         = $realmUser
            Password         = $realmPass
            ReleaseDate      = "2026-01-01T00:00:00"
            Processors       = $processors
        }
        Login = [ordered]@{
            IPAddress = "127.0.0.1"
            Port      = 9865
        }
        Ai = [ordered]@{
            IPAddress = "127.0.0.1"
            Port      = 9866
            Username  = $aiUser
            Password  = $aiPass
        }
    }

    if ($null -ne $gameCfg -and $null -ne $gameCfg.Rates) {
        $gameOut["Rates"] = $gameCfg.Rates
    }

    $aiOut = [ordered]@{
        Database = [ordered]@{
            Hostname = $DbCreds.Host
            Schema   = "moonqueen_game"
            Username = $DbCreds.User
            Password = $DbCreds.Password
            Port     = $DbCreds.Port
        }
        Ai = [ordered]@{
            ServerId  = $realmId
            Name      = "MoonQueen"
            IPAddress = "127.0.0.1"
            Port      = 9866
            Username  = $aiUser
            Password  = $aiPass
        }
    }

    Write-JsonUtf8NoBom $loginPath $loginOut 8
    Write-JsonUtf8NoBom $gamePath  $gameOut  8
    Write-JsonUtf8NoBom $aiPath    $aiOut    8

    Write-Host "[OK] Generated and synchronized three local configuration files in ${BinDir}:" -ForegroundColor Green
    Write-Host "     - SharedKey (32 characters) and SharedIV (16 characters) match between Login and Game." -ForegroundColor Gray
    Write-Host "     - Realm (port 9865) and AI (port 9866) are bound to 127.0.0.1." -ForegroundColor Gray

    return [pscustomobject]@{
        RealmId   = $realmId
        RealmUser = $realmUser
        RealmPass = $realmPass
        SharedKey = $sharedKey
        SharedIV  = $sharedIV
    }
}

function Ensure-CleanDatabases([string]$MySqlExe, $DbCreds) {
    $dbDir   = Join-Path $RepoRoot "قاعدة البيانات"
    $accFile = Join-Path $dbDir "1-moonqueen_account.sql"
    $gamFile = Join-Path $dbDir "2-moonqueen_game.sql"

    if (-not (Test-Path -LiteralPath $accFile) -or -not (Test-Path -LiteralPath $gamFile)) {
        throw "Database seed files are missing from: $dbDir"
    }

    $accTablesRes = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "" "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='moonqueen_account';"
    $gamTablesRes = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "" "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='moonqueen_game';"
    $gamInstRes   = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "" "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='moonqueen_game' AND table_name='cq_instance_enter_condition';"

    $accCount = 0; [void][int]::TryParse($accTablesRes.Output, [ref]$accCount)
    $gamCount = 0; [void][int]::TryParse($gamTablesRes.Output, [ref]$gamCount)
    $hasV6192 = ($gamInstRes.ExitCode -eq 0 -and $gamInstRes.Output -eq "1")

    $shouldImport = $false
    if ($accCount -lt 5 -or $gamCount -lt 302 -or (-not $hasV6192)) {
        Write-Host "[+] Installing clean moonqueen_account and moonqueen_game databases for the first time..." -ForegroundColor Cyan
        $shouldImport = $true
    } else {
        $accRows = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_account" "SELECT COUNT(*) FROM conquer_account;"
        $chrRows = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_game" "SELECT COUNT(*) FROM cq_user;"
        Write-Host "[OK] MoonQueen databases already exist (account tables: $accCount, accounts: $($accRows.Output); game tables: $gamCount, characters: $($chrRows.Output))." -ForegroundColor Green
        $resetInput = (Read-Host "Press Enter to keep existing data, or type RESET to reinstall both databases from scratch").Trim()
        if ($resetInput -ceq "RESET") {
            $shouldImport = $true
        }
    }

    if ($shouldImport) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        Write-Host "[1/2] Importing 1-moonqueen_account.sql..." -ForegroundColor Cyan
        $impAcc = Invoke-MySqlImportFile $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password $accFile
        if ($impAcc.ExitCode -ne 0) {
            throw "Failed to import 1-moonqueen_account.sql: $($impAcc.Error)"
        }
        Write-Host "[OK] Imported moonqueen_account in $([math]::Round($sw.Elapsed.TotalSeconds, 2)) seconds." -ForegroundColor Green

        $sw.Restart()
        Write-Host "[2/2] Importing 2-moonqueen_game.sql..." -ForegroundColor Cyan
        $impGam = Invoke-MySqlImportFile $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password $gamFile
        $sw.Stop()
        if ($impGam.ExitCode -ne 0) {
            throw "Failed to import 2-moonqueen_game.sql: $($impGam.Error)"
        }
        Write-Host "[OK] Imported moonqueen_game in $([math]::Round($sw.Elapsed.TotalSeconds, 2)) seconds." -ForegroundColor Green
    }
}

function Sync-RealmAndAdminAccount([string]$MySqlExe, $DbCreds, $RealmSecrets, [string]$TargetIP) {
    $encUser = Protect-RealmCredential $RealmSecrets.RealmUser
    $encPass = Protect-RealmCredential $RealmSecrets.RealmPass
    $safeRealmId = $RealmSecrets.RealmId.Replace("'", "''")
    $safeTargetIp = $TargetIP.Replace("'", "''")
    $safeEncUser = $encUser.Replace("'", "''")
    $safeEncPass = $encPass.Replace("'", "''")

    $realmSql = @'
DELETE FROM `realm` WHERE `Name` = 'MoonQueen' AND `RealmID` <> '__REALM_ID__';

INSERT INTO `realm` (
  `RealmID`, `Name`, `GameIPAddress`, `RpcIPAddress`,
  `GamePort`, `RpcPort`, `Status`, `Username`, `Password`,
  `DatabaseHost`, `DatabaseUser`, `DatabasePass`, `DatabaseSchema`, `DatabasePort`,
  `Active`, `ProductionRealm`
) VALUES (
  '__REALM_ID__', 'MoonQueen', '__TARGET_IP__', '127.0.0.1',
  5816, 9865, 1, '__ENC_USER__', '__ENC_PASS__',
  '', '', '', 'moonqueen_game', '3306',
  1, 1
) ON DUPLICATE KEY UPDATE
  `Name` = 'MoonQueen',
  `GameIPAddress` = '__TARGET_IP__',
  `RpcIPAddress` = '127.0.0.1',
  `GamePort` = 5816,
  `RpcPort` = 9865,
  `Username` = '__ENC_USER__',
  `Password` = '__ENC_PASS__',
  `DatabaseSchema` = 'moonqueen_game',
  `DatabasePort` = '3306',
  `Active` = 1,
  `ProductionRealm` = 1;
'@

    $realmSqlFinal = $realmSql.
        Replace("__REALM_ID__", $safeRealmId).
        Replace("__TARGET_IP__", $safeTargetIp).
        Replace("__ENC_USER__", $safeEncUser).
        Replace("__ENC_PASS__", $safeEncPass)

    $rRes = Invoke-MySqlStdinScript $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_account" $realmSqlFinal
    if ($rRes.ExitCode -ne 0) {
        throw "Failed to update realm table: $($rRes.Error)"
    }
    Write-Host "[OK] Updated the MoonQueen realm entry with IP: $TargetIP" -ForegroundColor Green

    # Check existing PM account (AuthorityId >= 8)
    $pmRes = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_account" "SELECT UserName FROM conquer_account WHERE AuthorityId >= 8 ORDER BY Id ASC LIMIT 1;"
    $existingPm = if ($pmRes.ExitCode -eq 0) { $pmRes.Output.Trim() } else { "" }

    $needAdminSetup = [string]::IsNullOrWhiteSpace($existingPm)
    $adminUser = "ahmedzahran"

    if (-not $needAdminSetup) {
        Write-Host "[OK] Existing PM account: $existingPm (AuthorityId = 8)." -ForegroundColor Green
        $updChoice = (Read-Host "Press Enter to keep this account, or type PASS to update its password or create a PM account").Trim()
        if ($updChoice -ceq "PASS") {
            $adminUser = $existingPm
            $needAdminSetup = $true
        }
    }

    if ($needAdminSetup) {
        $enteredUser = (Read-Host "PM account name [Enter for $adminUser]").Trim()
        if (-not [string]::IsNullOrWhiteSpace($enteredUser)) {
            $adminUser = $enteredUser
        }
        $adminPass = ""
        while ([string]::IsNullOrWhiteSpace($adminPass)) {
            $adminPass = Read-PlainFromSecurePrompt "Enter the game password for account $adminUser"
            if ([string]::IsNullOrWhiteSpace($adminPass)) {
                Write-Host "[!] Game account password cannot be empty." -ForegroundColor Yellow
            }
        }
        $salt = "MQ" + (New-CryptoRandomString 34)
        $hash = Get-AccountPasswordHash $adminPass $salt
        $safeAdmin = $adminUser.Replace("'", "''")

        $chkUser = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_account" "SELECT Id FROM conquer_account WHERE UserName='$safeAdmin' LIMIT 1;"
        if ($chkUser.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($chkUser.Output)) {
            $accId = [int]$chkUser.Output.Trim()
            $accSql = "UPDATE conquer_account SET Password='$hash', Salt='$salt', AuthorityId=8, Flag=1 WHERE Id=$accId LIMIT 1;"
        } else {
            $accSql = "INSERT INTO conquer_account (UserName, Password, Salt, AuthorityId, Flag, IpAddress, MacAddress, ParentId, Created) VALUES ('$safeAdmin', '$hash', '$salt', 8, 1, '127.0.0.1', '000000000000', UUID(), NOW());"
        }
        $aRes = Invoke-MySqlStdinScript $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_account" $accSql
        if ($aRes.ExitCode -ne 0) {
            throw "Failed to save admin account: $($aRes.Error)"
        }
        Write-Host "[OK] Saved PM account $adminUser (AuthorityId = 8)." -ForegroundColor Green
    }

    # Optional [PM]/[GM] character badge if characters exist in cq_user
    $charsRes = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_game" "SELECT name FROM cq_user ORDER BY id ASC LIMIT 10;"
    if ($charsRes.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($charsRes.Output)) {
        $charLines = $charsRes.Output -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        Write-Host "[i] Existing characters: $($charLines -join ', ')" -ForegroundColor Cyan
        $wantBadge = (Read-Host "Enter a character name to add the PM badge now, or press Enter to skip").Trim()
        if (-not [string]::IsNullOrWhiteSpace($wantBadge)) {
            Set-CharacterStaffBadge $MySqlExe $DbCreds $wantBadge "[PM]"
        }
    } else {
        Write-Host "[i] After creating a character (4-11 characters), run setup again to add the PM badge." -ForegroundColor Gray
    }
}

function Set-CharacterStaffBadge([string]$MySqlExe, $DbCreds, [string]$CharName, [string]$Badge = "[PM]") {
    $runningGame = Get-Process -Name "MoonQueen.Game" -ErrorAction SilentlyContinue
    if ($null -ne $runningGame) {
        Write-Host "[!] Make sure the character is offline before changing its name." -ForegroundColor Yellow
    }

    $baseName = $CharName -replace '\[(PM|GM)\]$', ''
    $targetName = $baseName + $Badge
    if ($targetName.Length -gt 15) {
        Write-Host "[!!] Final name $targetName has $($targetName.Length) characters; choose a name of at most 11 characters." -ForegroundColor Red
        return
    }

    $safeOld = $CharName.Replace("'", "''")
    $safeNew = $targetName.Replace("'", "''")
    $existsOld = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_game" "SELECT id FROM cq_user WHERE name='$safeOld' LIMIT 1;"
    if ($existsOld.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($existsOld.Output)) {
        Write-Host "[!!] Character '$CharName' not found in moonqueen_game." -ForegroundColor Red
        return
    }
    if ($CharName -ceq $targetName) {
        Write-Host "[OK] Character already has the badge: $targetName" -ForegroundColor Green
        return
    }
    $existsNew = Invoke-MySqlQuery $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_game" "SELECT COUNT(*) FROM cq_user WHERE name='$safeNew';"
    if ($existsNew.ExitCode -eq 0 -and $existsNew.Output -ne "0") {
        Write-Host "[!!] Character '$targetName' already exists!" -ForegroundColor Red
        return
    }
    $updRes = Invoke-MySqlStdinScript $MySqlExe $DbCreds.Host $DbCreds.Port $DbCreds.User $DbCreds.Password "moonqueen_game" "UPDATE cq_user SET name='$safeNew' WHERE id=$($existsOld.Output.Trim()) LIMIT 1;"
    if ($updRes.ExitCode -eq 0) {
        Write-Host "[OK] Badge applied: $CharName -> $targetName" -ForegroundColor Green
    } else {
        Write-Host "[!!] Failed to update character name: $($updRes.Error)" -ForegroundColor Red
    }
}

function Invoke-ReadinessCheck([bool]$QuietHeader = $false) {
    if (-not $QuietHeader) {
        Write-Banner "Server, database and client pre-flight check"
    }
    $okCount   = 0
    $warnCount = 0
    $failCount = 0

    # 1. .NET 7 check
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $dotnetCmd) {
        $runtimes = & dotnet --list-runtimes 2>$null
        if ($runtimes -match "Microsoft\.NETCore\.App 7\.0\.") {
            Write-Host "[OK] .NET 7.0 runtime is installed." -ForegroundColor Green
            $okCount++
        } else {
            Write-Host "[!!] .NET 7.0 runtime not found. Install .NET 7.0 SDK (x64)." -ForegroundColor Red
            $failCount++
        }
    } else {
        Write-Host "[!!] dotnet not found. Install .NET 7.0 SDK (x64)." -ForegroundColor Red
        $failCount++
    }

    # 2. Binaries check
    $binDir = Resolve-BinDirectory
    Write-Host "[i] Server directory: $binDir" -ForegroundColor Gray

    foreach ($exe in @("MoonQueen.Login.exe", "MoonQueen.Game.exe", "MoonQueen.Ai.exe")) {
        if (Test-Path -LiteralPath (Join-Path $binDir $exe)) {
            Write-Host "[OK] Server executable found: $exe" -ForegroundColor Green
            $okCount++
        } else {
            Write-Host "[!] $exe is not built. Build MoonQueen.sln in Release mode." -ForegroundColor Yellow
            $warnCount++
        }
    }

    # 3. Local Configs + AES key/IV + Ports check
    $loginCfg = Read-JsonConfigSafe (Join-Path $binDir "MoonQueen.Login.Config.local.json")
    $gameCfg  = Read-JsonConfigSafe (Join-Path $binDir "MoonQueen.Game.Config.local.json")
    $aiCfg    = Read-JsonConfigSafe (Join-Path $binDir "MoonQueen.Ai.Config.local.json")

    if ($null -ne $loginCfg -and $null -ne $gameCfg -and $null -ne $aiCfg) {
        Write-Host "[OK] Three local configuration files are present." -ForegroundColor Green
        $okCount++
        $lk = [string]$loginCfg.Auth.SharedKey
        $gk = [string]$gameCfg.Auth.SharedKey
        $li = [string]$loginCfg.Auth.SharedIV
        $gi = [string]$gameCfg.Auth.SharedIV
        if ($lk.Length -eq 32 -and $lk -eq $gk -and $li.Length -eq 16 -and $li -eq $gi) {
            Write-Host "[OK] Login and Game shared encryption keys match." -ForegroundColor Green
            $okCount++
        } else {
            Write-Host "[!!] SharedKey/SharedIV mismatch between Login and Game!" -ForegroundColor Red
            $failCount++
        }
        if ($gameCfg.Realm.Name -eq "MoonQueen" -and $aiCfg.Ai.Name -eq "MoonQueen" -and [int]$gameCfg.Ai.Port -eq [int]$aiCfg.Ai.Port) {
            Write-Host "[OK] Game and AI realm name and port match ($($aiCfg.Ai.Port))." -ForegroundColor Green
            $okCount++
        } else {
            Write-Host "[!!] Game and AI realm name or port mismatch!" -ForegroundColor Red
            $failCount++
        }
    } else {
        Write-Host "[!!] Local configuration files are incomplete in $binDir. Run 1-Setup-MoonQueen.bat." -ForegroundColor Red
        $failCount++
    }

    # 4. Assets inside BinDir
    if (Test-Path -LiteralPath (Join-Path $binDir "lua\lua.ini")) {
        Write-Host "[OK] Server Lua scripts found: lua\lua.ini" -ForegroundColor Green
        $okCount++
    } else {
        Write-Host "[!] Missing server lua folder." -ForegroundColor Yellow
        $warnCount++
    }

    if (Test-Path -LiteralPath (Join-Path $binDir "ini\GameMap.dat")) {
        Write-Host "[OK] Map index found: ini\GameMap.dat" -ForegroundColor Green
        $okCount++
    } else {
        Write-Host "[!] Missing client 6192 ini folder at $binDir\ini" -ForegroundColor Yellow
        $warnCount++
    }

    if (Test-Path -LiteralPath (Join-Path $binDir "map")) {
        Write-Host "[OK] Map folder exists." -ForegroundColor Green
        $okCount++
    } else {
        Write-Host "[!] Missing client 6192 map folder at $binDir\map" -ForegroundColor Yellow
        $warnCount++
    }

    # 5. MariaDB & Schema check
    $dbHost = if ($null -ne $loginCfg -and $loginCfg.Database.Hostname) { [string]$loginCfg.Database.Hostname } else { "127.0.0.1" }
    $dbPort = if ($null -ne $loginCfg -and $loginCfg.Database.Port) { [int]$loginCfg.Database.Port } else { 3306 }
    $realmIpInDb = $null

    if (Test-TcpPort $dbHost $dbPort) {
        Write-Host "[OK] MariaDB / MySQL is listening on ${dbHost}:${dbPort}" -ForegroundColor Green
        $okCount++

        $mysqlExe = Find-MySqlExe
        if ($null -ne $mysqlExe -and $null -ne $loginCfg -and $null -ne $loginCfg.Database) {
            $accSchema  = [string]$loginCfg.Database.Schema
            $gameSchema = if ($null -ne $gameCfg -and $gameCfg.Database.Schema) { [string]$gameCfg.Database.Schema } else { "moonqueen_game" }
            $res = Invoke-MySqlQuery $mysqlExe $dbHost $dbPort ([string]$loginCfg.Database.Username) ([string]$loginCfg.Database.Password) $accSchema "SELECT CONCAT(Name, '|', GameIPAddress, '|', GamePort, '|', Username) FROM realm WHERE Name='MoonQueen' LIMIT 1;"
            if ($res.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($res.Output)) {
                $parts = $res.Output.Split('|')
                $realmIpInDb = $parts[1]
                $expectedEncUser = if ($null -ne $gameCfg -and $null -ne $gameCfg.Realm) { Protect-RealmCredential ([string]$gameCfg.Realm.Username) } else { "" }
                $modeLabel = if ($realmIpInDb -eq "127.0.0.1") { "local offline" } else { "network / Hamachi" }
                Write-Host "[OK] Realm: $($parts[0]) | IP: $realmIpInDb ($modeLabel)" -ForegroundColor Green
                $okCount++
                if (-not [string]::IsNullOrEmpty($expectedEncUser) -and $parts[3] -eq $expectedEncUser) {
                    Write-Host "[OK] Realm credentials match the configuration." -ForegroundColor Green
                    $okCount++
                } else {
                    Write-Host "[!!] Realm credentials differ from configuration. Run 1-Setup-MoonQueen.bat." -ForegroundColor Red
                    $failCount++
                }
            } else {
                Write-Host "[!!] Realm table in $accSchema is not configured. Run setup." -ForegroundColor Red
                $failCount++
            }

            $resGame = Invoke-MySqlQuery $mysqlExe $dbHost $dbPort ([string]$loginCfg.Database.Username) ([string]$loginCfg.Database.Password) $gameSchema "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$gameSchema';"
            $resProc = Invoke-MySqlQuery $mysqlExe $dbHost $dbPort ([string]$loginCfg.Database.Username) ([string]$loginCfg.Database.Password) $gameSchema "SELECT COUNT(*) FROM information_schema.routines WHERE routine_schema = '$gameSchema' AND routine_type = 'PROCEDURE';"
            if ($resGame.ExitCode -eq 0 -and [int]$resGame.Output -ge 302) {
                Write-Host "[OK] Database ${gameSchema}: $($resGame.Output) tables, $($resProc.Output) procedures." -ForegroundColor Green
                $okCount++
            } else {
                Write-Host "[!!] Database $gameSchema has only $($resGame.Output) tables. Run setup." -ForegroundColor Red
                $failCount++
            }
        }
    } else {
        Write-Host "[!!] MySQL is not listening on ${dbHost}:${dbPort}. Start MySQL in XAMPP." -ForegroundColor Red
        $failCount++
    }

    # 6. Client check (Servers[*] & DefaultServer LoginHost/GameHost)
    $clientDir = Resolve-ClientDirectory -PromptIfMissing:$false
    if ($null -ne $clientDir) {
        $cIp = Get-ClientConfiguredIp $clientDir
        $lockExists = Test-Path -LiteralPath (Join-Path $clientDir "config.json.lock")
        if ($lockExists) {
            Write-Host "[!] config.json.lock exists in $clientDir. Run setup to update the IP." -ForegroundColor Yellow
            $warnCount++
        }
        if (-not [string]::IsNullOrWhiteSpace($cIp)) {
            Write-Host "[OK] Client config.json points to $cIp" -ForegroundColor Green
            $okCount++
            if (-not [string]::IsNullOrWhiteSpace($realmIpInDb) -and $cIp -ne $realmIpInDb) {
                Write-Host "[!!] IP mismatch: database $realmIpInDb, client $cIp. Run setup." -ForegroundColor Red
                $failCount++
            }
        } else {
            Write-Host "[!] Client config.json needs synchronization. Run setup." -ForegroundColor Yellow
            $warnCount++
        }
        if (Test-Path -LiteralPath (Join-Path $clientDir "MoonQueen.exe")) {
            Write-Host "[OK] MoonQueen.exe exists in the client folder." -ForegroundColor Green
            $okCount++
        } else {
            Write-Host "[!] MoonQueen.exe not found in $clientDir." -ForegroundColor Yellow
            $warnCount++
        }
    }

    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor DarkGray
    Write-Host "  Pre-flight results: OK ($okCount) | warnings ($warnCount) | errors ($failCount)" -ForegroundColor Cyan
    Write-Host "======================================================================" -ForegroundColor DarkGray

    return [pscustomobject]@{
        OkCount   = $okCount
        WarnCount = $warnCount
        FailCount = $failCount
    }
}

function Invoke-FullSequentialSetup {
    Write-Banner "[1] MoonQueen all-in-one setup"
    Write-Host "This setup prepares and checks the server, database and client in sequence." -ForegroundColor Gray

    # ------------------------------------------------------------------------
    # [1/6] البيئة + وضع الشبكة + مسار الكلينت
    # ------------------------------------------------------------------------
    Write-StepHeader "[1/6]" "Select mode and check environment (MySQL, .NET 7 and client)"

    $state = Load-LauncherState
    $savedIp = if ($null -ne $state -and -not [string]::IsNullOrWhiteSpace([string]$state.PreferredIP)) { [string]$state.PreferredIP } else { "127.0.0.1" }
    $defaultChoice = if ($savedIp -eq "127.0.0.1") { "1" } else { "2" }

    Write-Host "  [1] Local offline mode (127.0.0.1)" -ForegroundColor Green
    Write-Host "  [2] Hamachi network mode ($HamachiIP)" -ForegroundColor Cyan
    $netChoice = (Read-Host "Select mode [default: $defaultChoice]").Trim()
    if ([string]::IsNullOrWhiteSpace($netChoice)) { $netChoice = $defaultChoice }

    $targetIp = "127.0.0.1"
    if ($netChoice -eq "2") {
        $customIp = (Read-Host "Hamachi IP [$HamachiIP] (press Enter to confirm or enter another IP)").Trim()
        $targetIp = if ([string]::IsNullOrWhiteSpace($customIp)) { $HamachiIP } else { $customIp }
    }
    Save-LauncherState @{ PreferredIP = $targetIp }
    Write-Host "[OK] Selected server and client IP: $targetIp" -ForegroundColor Green

    if (-not (Test-TcpPort "127.0.0.1" 3306)) {
        Write-Host ""
        Write-Host "[!!] MySQL / MariaDB is not listening on 127.0.0.1:3306!" -ForegroundColor Red
        Write-Host "     Start MySQL in XAMPP Control Panel, then press Enter to continue..." -ForegroundColor Yellow
        Read-Host | Out-Null
        if (-not (Test-TcpPort "127.0.0.1" 3306)) {
            Write-Host "[!!] MySQL is still stopped. Start it in XAMPP and rerun setup." -ForegroundColor Red
            return
        }
    }

    $mysqlExe = Find-MySqlExe -PromptIfMissing:$true
    if ($null -eq $mysqlExe) {
        Write-Host "[!!] mysql.exe was not found. Check your XAMPP installation." -ForegroundColor Red
        return
    }

    $clientDir = Resolve-ClientDirectory -PromptIfMissing:$true -AllowChange:$true

    # ------------------------------------------------------------------------
    # [2/6] تجهيز ملفات السيرفر والـ Lua والكلينت واللودر وconfig.json
    # ------------------------------------------------------------------------
    Write-StepHeader "[2/6]" "Copy and synchronize server Lua and client assets"
    $binDir = Resolve-BinDirectory -CreateIfMissing:$true
    Sync-ClientAndServerAssets -BinDir $binDir -ClientDir $clientDir -TargetIP $targetIp

    # ------------------------------------------------------------------------
    # [3/6] التحقق من باسورد MySQL وتوليد ملفات *.Config.local.json تلقائيًا
    # ------------------------------------------------------------------------
    Write-StepHeader "[3/6]" "Generate and synchronize three local configuration files"
    $dbCreds = Resolve-VerifiedMySqlCredentials -MySqlExe $mysqlExe -BinDir $binDir
    $realmSecrets = Ensure-ServerLocalConfigs -BinDir $binDir -DbCreds $dbCreds

    # ------------------------------------------------------------------------
    # [4/6] تثبيت أو مراجعة قاعدتَي moonqueen_account و moonqueen_game
    # ------------------------------------------------------------------------
    Write-StepHeader "[4/6]" "Install or review the two MoonQueen databases"
    Ensure-CleanDatabases -MySqlExe $mysqlExe -DbCreds $dbCreds

    # ------------------------------------------------------------------------
    # [5/6] ربط وتشفير سطر Realm + تجهيز حساب الأدمن [PM] وشارة الإدارة
    # ------------------------------------------------------------------------
    Write-StepHeader "[5/6]" "Update realm IP ($targetIp) and prepare the PM account"
    Sync-RealmAndAdminAccount -MySqlExe $mysqlExe -DbCreds $dbCreds -RealmSecrets $realmSecrets -TargetIP $targetIp

    # ------------------------------------------------------------------------
    # [6/6] بناء اختياري (لو لسه متبناش) + الفحص النهائي الشامل
    # ------------------------------------------------------------------------
    Write-StepHeader "[6/6]" "Final readiness verification"
    $gameExePath = Join-Path $binDir "MoonQueen.Game.exe"
    if (-not (Test-Path -LiteralPath $gameExePath)) {
        $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
        $slnPath = Join-Path $RepoRoot "MoonQueen.sln"
        if ($null -ne $dotnetCmd -and (Test-Path -LiteralPath $slnPath)) {
            $wantBuild = (Read-Host "Server executables are missing. Build MoonQueen.sln in Release now? [Enter = yes; n = build manually]").Trim()
            if ($wantBuild -ne "n" -and $wantBuild -ne "N") {
                Write-Host "[+] Building MoonQueen.sln in Release..." -ForegroundColor Cyan
                & dotnet build $slnPath -c Release
                $binDir = Resolve-BinDirectory -CreateIfMissing:$true
                Sync-ClientAndServerAssets -BinDir $binDir -ClientDir $clientDir -TargetIP $targetIp
                [void](Ensure-ServerLocalConfigs -BinDir $binDir -DbCreds $dbCreds)
            }
        }
    }

    $summary = Invoke-ReadinessCheck -QuietHeader:$true

    $runningLogin = Get-Process -Name "MoonQueen.Login" -ErrorAction SilentlyContinue
    if ($null -ne $runningLogin) {
        Write-Host ""
        Write-Host "[WARNING] Server is running. Restart all services to apply changes." -ForegroundColor Yellow
    } elseif ($summary.FailCount -eq 0) {
        Write-Host ""
        Write-Host "[OK] Setup finished with no readiness errors." -ForegroundColor Green
        Write-Host "Start the server using 2-Start-MoonQueen-Server.bat." -ForegroundColor Yellow
    }
}

function Start-AllServices {
    Write-Banner "[2] Start MoonQueen services and Control Studio"
    $binDir = Resolve-BinDirectory
    Ensure-LuaAssets $binDir

    $loginCfgPath = Join-Path $binDir "MoonQueen.Login.Config.local.json"
    $gameCfgPath  = Join-Path $binDir "MoonQueen.Game.Config.local.json"
    $aiCfgPath    = Join-Path $binDir "MoonQueen.Ai.Config.local.json"

    if (-not (Test-Path -LiteralPath $loginCfgPath) -or -not (Test-Path -LiteralPath $gameCfgPath) -or -not (Test-Path -LiteralPath $aiCfgPath)) {
        Write-Host "[!!] Local configuration files are missing!" -ForegroundColor Red
        Write-Host "     Run 1-Setup-MoonQueen.bat first." -ForegroundColor Yellow
        return
    }

    # Check without exposing credentials or editing configs while services may be running.
    $gameAiCheck = Read-JsonConfigSafe $gameCfgPath
    $aiCheck = Read-JsonConfigSafe $aiCfgPath
    if ($null -eq $gameAiCheck -or $null -eq $gameAiCheck.Ai -or $null -eq $aiCheck -or $null -eq $aiCheck.Ai -or
        [string]$aiCheck.Ai.Name -notmatch '^[\x21-\x7E]{1,16}$' -or
        [string]$gameAiCheck.Ai.Username -notmatch '^[\x21-\x7E]{1,16}$' -or
        [string]$gameAiCheck.Ai.Password -notmatch '^[\x21-\x7E]{1,16}$' -or
        [string]$gameAiCheck.Ai.Username -cne [string]$aiCheck.Ai.Username -or
        [string]$gameAiCheck.Ai.Password -cne [string]$aiCheck.Ai.Password) {
        Write-Host "[!!] AI credentials are incompatible or mismatched. Stop services, run setup, then restart." -ForegroundColor Red
        return
    }

    if (-not (Test-TcpPort "127.0.0.1" 3306)) {
        Write-Host "[!!] MySQL is not listening on 127.0.0.1:3306. Start MySQL in XAMPP." -ForegroundColor Red
        return
    }

    foreach ($exe in @("MoonQueen.Login.exe", "MoonQueen.Game.exe", "MoonQueen.Ai.exe")) {
        if (-not (Test-Path -LiteralPath (Join-Path $binDir $exe))) {
            Write-Host "[!!] $exe is missing from $binDir. Build MoonQueen.sln in Release or run setup." -ForegroundColor Red
            return
        }
    }

    # 1. Start Login Server & wait for RPC port 9865
    if ($null -ne (Get-Process -Name "MoonQueen.Login" -ErrorAction SilentlyContinue)) {
        Write-Host "[=] MoonQueen.Login is already running." -ForegroundColor Yellow
    } else {
        Write-Host "[1/3] Starting MoonQueen.Login.exe..." -ForegroundColor Green
        Start-Process -FilePath (Join-Path $binDir "MoonQueen.Login.exe") -WorkingDirectory $binDir
        [void](Wait-TcpPort "127.0.0.1" 9865 10)
    }

    # 2. Start Game Server & wait for AI RPC port 9866
    if ($null -ne (Get-Process -Name "MoonQueen.Game" -ErrorAction SilentlyContinue)) {
        Write-Host "[=] MoonQueen.Game is already running." -ForegroundColor Yellow
    } else {
        Write-Host "[2/3] Starting MoonQueen.Game.exe..." -ForegroundColor Green
        Start-Process -FilePath (Join-Path $binDir "MoonQueen.Game.exe") -WorkingDirectory $binDir
        [void](Wait-TcpPort "127.0.0.1" 9866 15)
    }

    # 3. Start AI Server
    if ($null -ne (Get-Process -Name "MoonQueen.Ai" -ErrorAction SilentlyContinue)) {
        Write-Host "[=] MoonQueen.Ai is already running." -ForegroundColor Yellow
    } else {
        Write-Host "[3/3] Starting MoonQueen.Ai.exe..." -ForegroundColor Green
        Start-Process -FilePath (Join-Path $binDir "MoonQueen.Ai.exe") -WorkingDirectory $binDir
        Start-Sleep -Seconds 2
    }

    # 4. Start MoonQueen Control Studio on 127.0.0.1:8099
    $studioDir = Join-Path $RepoRoot "لوحة تحكم السيرفر"
    $phpExe = Find-PhpExe
    if ((Test-Path -LiteralPath $studioDir) -and $null -ne $phpExe) {
        if (-not (Test-TcpPort "127.0.0.1" 8099)) {
            Write-Host "[+] Starting Control Studio at http://127.0.0.1:8099 ..." -ForegroundColor Green
            Start-Process -FilePath $phpExe -ArgumentList '-S 127.0.0.1:8099 router.php' -WorkingDirectory $studioDir -WindowStyle Minimized
            Start-Sleep -Seconds 1
        }
        Start-Process "http://127.0.0.1:8099"
    }

    Write-Host ""
    Write-Host "[i] Start commands issued. Check each server log for successful startup and AI authentication." -ForegroundColor Cyan
}

function Stop-AllServices {
    Write-Banner "Stopping MoonQueen services (AI -> Game -> Login)"
    foreach ($name in @("MoonQueen.Ai", "MoonQueen.Game", "MoonQueen.Login")) {
        $procs = Get-Process -Name $name -ErrorAction SilentlyContinue
        if ($null -ne $procs) {
            foreach ($p in $procs) {
                Write-Host "[-] Stopping $name (PID: $($p.Id))..." -ForegroundColor Yellow
                Stop-Process -Id $p.Id -Force
            }
            Start-Sleep -Seconds 1
        } else {
            Write-Host "[=] $name is already stopped." -ForegroundColor DarkGray
        }
    }
    Write-Host ""
    Write-Host "[OK] Stop commands completed." -ForegroundColor Green
}

function Invoke-ServerLauncher {
    $loginRunning = $null -ne (Get-Process -Name "MoonQueen.Login" -ErrorAction SilentlyContinue)
    $gameRunning  = $null -ne (Get-Process -Name "MoonQueen.Game"  -ErrorAction SilentlyContinue)
    $aiRunning    = $null -ne (Get-Process -Name "MoonQueen.Ai"    -ErrorAction SilentlyContinue)
    $studioUp     = Test-TcpPort "127.0.0.1" 8099 500

    # If none of the 3 servers are running, start everything directly!
    if ((-not $loginRunning) -and (-not $gameRunning) -and (-not $aiRunning)) {
        Start-AllServices
        return
    }

    Write-Banner "[2] MoonQueen server manager"
    Write-Host ("  - MoonQueen.Login : " + $(if ($loginRunning) { "ONLINE" } else { "OFFLINE" })) -ForegroundColor $(if ($loginRunning) { "Green" } else { "Yellow" })
    Write-Host ("  - MoonQueen.Game  : " + $(if ($gameRunning)  { "ONLINE" } else { "OFFLINE" })) -ForegroundColor $(if ($gameRunning)  { "Green" } else { "Yellow" })
    Write-Host ("  - MoonQueen.Ai    : " + $(if ($aiRunning)    { "ONLINE" } else { "OFFLINE" })) -ForegroundColor $(if ($aiRunning)    { "Green" } else { "Yellow" })
    Write-Host ("  - Control Studio  : " + $(if ($studioUp)     { "ONLINE at http://127.0.0.1:8099" } else { "OFFLINE" })) -ForegroundColor $(if ($studioUp) { "Cyan" } else { "DarkGray" })
    Write-Host ""

    if ((-not $loginRunning) -or (-not $gameRunning) -or (-not $aiRunning)) {
        Write-Host "  [1] Start stopped services" -ForegroundColor Green
        Write-Host "  [2] Restart all server services" -ForegroundColor Yellow
        Write-Host "  [3] Stop all server services" -ForegroundColor Red
        Write-Host "  [4] Open Control Studio (http://127.0.0.1:8099)" -ForegroundColor Cyan
        Write-Host "  [0] Exit" -ForegroundColor DarkGray
        $c = (Read-Host "Select an option [default: 1]").Trim()
        if ([string]::IsNullOrWhiteSpace($c)) { $c = "1" }
        switch ($c) {
            "1" { Start-AllServices }
            "2" { Stop-AllServices; Start-Sleep -Seconds 2; Start-AllServices }
            "3" { Stop-AllServices }
            "4" { Start-Process "http://127.0.0.1:8099" }
        }
    } else {
        Write-Host "  [1] Restart all server services" -ForegroundColor Yellow
        Write-Host "  [2] Stop all server services" -ForegroundColor Red
        Write-Host "  [3] Open Control Studio (http://127.0.0.1:8099)" -ForegroundColor Cyan
        Write-Host "  [0] Exit (leave services running)" -ForegroundColor DarkGray
        $c = (Read-Host "Select an option [default: 0]").Trim()
        switch ($c) {
            "1" { Stop-AllServices; Start-Sleep -Seconds 2; Start-AllServices }
            "2" { Stop-AllServices }
            "3" { Start-Process "http://127.0.0.1:8099" }
        }
    }
}

function Invoke-QuickIpSwitch([string]$TargetIP, [string]$Label) {
    Write-Banner "Switch server and client IP to $Label ($TargetIP)"
    $binDir   = Resolve-BinDirectory -CreateIfMissing:$true
    $mysqlExe = Find-MySqlExe -PromptIfMissing:$true
    if ($null -eq $mysqlExe) { return }
    $dbCreds      = Resolve-VerifiedMySqlCredentials -MySqlExe $mysqlExe -BinDir $binDir
    $realmSecrets = Ensure-ServerLocalConfigs -BinDir $binDir -DbCreds $dbCreds
    $clientDir    = Resolve-ClientDirectory -PromptIfMissing:$true
    Sync-ClientAndServerAssets -BinDir $binDir -ClientDir $clientDir -TargetIP $TargetIP
    Sync-RealmAndAdminAccount -MySqlExe $mysqlExe -DbCreds $dbCreds -RealmSecrets $realmSecrets -TargetIP $TargetIP
    Save-LauncherState @{ PreferredIP = $TargetIP }
}

switch ($Mode) {
    "SetupAll"       { Invoke-FullSequentialSetup }
    "ServerLauncher" { Invoke-ServerLauncher }
    "Check"          { [void](Invoke-ReadinessCheck) }
    "Offline"        { Invoke-QuickIpSwitch -TargetIP "127.0.0.1" -Label "local offline" }
    "Hamachi"        { Invoke-QuickIpSwitch -TargetIP $HamachiIP  -Label "Hamachi network" }
    "StartAll"       { Start-AllServices }
    "StopAll"        { Stop-AllServices }
    "StaffBadge"     {
        $binDir   = Resolve-BinDirectory
        $mysqlExe = Find-MySqlExe -PromptIfMissing:$true
        if ($null -ne $mysqlExe) {
            $dbCreds = Resolve-VerifiedMySqlCredentials -MySqlExe $mysqlExe -BinDir $binDir
            $name = (Read-Host "Enter the current character name").Trim()
            if (-not [string]::IsNullOrWhiteSpace($name)) {
                Set-CharacterStaffBadge $mysqlExe $dbCreds $name "[PM]"
            }
        }
    }
}
