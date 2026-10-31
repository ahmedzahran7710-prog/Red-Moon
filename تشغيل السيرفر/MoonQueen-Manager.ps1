param(
    [ValidateSet("Menu", "Check", "Offline", "Hamachi", "StartAll", "StopAll", "CryptoHelper")]
    [string]$Mode = "Menu",
    [string]$HamachiIP = "25.21.212.130"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# ============================================================================
# MoonQueen Launch & Readiness Manager (V6192)
# Brand: MoonQueen | Developer: ahmedzahran (احمد زهران)
# Protected Developer Rights Seal — Do Not Modify
# ============================================================================
$MQ_BRAND  = "MoonQueen"
$MQ_DEV_EN = "ahmedzahran"
$MQ_DEV_AR = "احمد زهران"
$MQ_SEAL   = "f00c83c7789c6b18a7ce5a5cd5b905c85fd41e20cd2e88806c3a92a7a96348b9"

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

$ScriptDir = $PSScriptRoot
$RepoRoot  = Split-Path -Parent $ScriptDir
$LocalStateFile = Join-Path $ScriptDir "launcher.local.json"

function Write-Banner([string]$Subtitle) {
    Write-Host ""
    Write-Host "==================================================================" -ForegroundColor DarkRed
    Write-Host "  MoonQueen Server Manager (V6192) — Developed by ahmedzahran" -ForegroundColor Yellow
    Write-Host "  $Subtitle" -ForegroundColor Cyan
    Write-Host "==================================================================" -ForegroundColor DarkRed
    Write-Host ""
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
    $json = $current | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($LocalStateFile, $json, [System.Text.Encoding]::UTF8)
}

function Resolve-BinDirectory {
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
    return $candidates[0]
}

function Find-MySqlExe {
    $paths = @(
        "C:\xampp\mysql\bin\mysql.exe",
        "D:\xampp\mysql\bin\mysql.exe",
        "E:\xampp\mysql\bin\mysql.exe",
        "F:\xampp\mysql\bin\mysql.exe"
    )
    foreach ($p in $paths) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    $cmd = Get-Command mysql -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { return $cmd.Source }
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

function Read-JsonConfigSafe([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        return $null
    }
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
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()
    return [pscustomobject]@{
        ExitCode = $proc.ExitCode
        Output   = $stdout.Trim()
        Error    = $stderr.Trim()
    }
}

function Get-AccountDbCredentials([string]$BinDir, [bool]$PromptIfMissing = $true) {
    $loginCfgPath = Join-Path $BinDir "MoonQueen.Login.Config.local.json"
    $cfg = Read-JsonConfigSafe $loginCfgPath
    if ($null -ne $cfg -and $null -ne $cfg.Database) {
        return [pscustomobject]@{
            Host     = if ($cfg.Database.Hostname) { [string]$cfg.Database.Hostname } else { "127.0.0.1" }
            Port     = if ($cfg.Database.Port) { [int]$cfg.Database.Port } else { 3306 }
            User     = if ($cfg.Database.Username) { [string]$cfg.Database.Username } else { "root" }
            Password = if ($null -ne $cfg.Database.Password) { [string]$cfg.Database.Password } else { "" }
            Schema   = if ($cfg.Database.Schema) { [string]$cfg.Database.Schema } else { "moonqueen_account" }
            FromFile = $true
        }
    }
    if (-not $PromptIfMissing) {
        return $null
    }
    Write-Host "[!] ملف MoonQueen.Login.Config.local.json غير موجود بعد داخل $BinDir" -ForegroundColor Yellow
    $sec = Read-Host "اكتب باسورد MySQL (root) المحلي لهذه العملية فقط (لن يُحفظ على القرص)" -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
    try {
        $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    } finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
    return [pscustomobject]@{
        Host     = "127.0.0.1"
        Port     = 3306
        User     = "root"
        Password = $plain
        Schema   = "moonqueen_account"
        FromFile = $false
    }
}

function Resolve-ClientDirectory([bool]$PromptIfMissing = $false) {
    $state = Load-LauncherState
    $candidates = @()
    if ($null -ne $state -and -not [string]::IsNullOrWhiteSpace($state.ClientDirectory)) {
        $candidates += [string]$state.ClientDirectory
    }
    $parentOfRepo = Split-Path -Parent $RepoRoot
    if (-not [string]::IsNullOrWhiteSpace($parentOfRepo)) {
        $candidates += (Join-Path $parentOfRepo "MoonQueen-Client")
    }
    $candidates += "F:\AhmedZahran\MoonQueen-Client"

    foreach ($c in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($c) -and (Test-Path -LiteralPath $c)) {
            return $c
        }
    }

    if ($PromptIfMissing) {
        Write-Host ""
        Write-Host "[?] اكتب مسار فولدر كلينت 6192 على جهازك (مثال: F:\AhmedZahran\MoonQueen-Client)" -ForegroundColor Cyan
        $entered = (Read-Host "مسار فولدر الكلينت (أو اضغط Enter للتخطي)").Trim('"').Trim()
        if (-not [string]::IsNullOrWhiteSpace($entered) -and (Test-Path -LiteralPath $entered)) {
            Save-LauncherState @{ ClientDirectory = $entered }
            return $entered
        }
    }
    return $null
}

function Ensure-LuaAssets([string]$BinDir) {
    $srcLua = Join-Path $RepoRoot "RequiredAssetsForRun\lua"
    $dstLua = Join-Path $BinDir "lua"
    $dstIni = Join-Path $dstLua "lua.ini"
    if (-not (Test-Path -LiteralPath $dstIni) -and (Test-Path -LiteralPath (Join-Path $srcLua "lua.ini"))) {
        if (Test-Path -LiteralPath $BinDir) {
            New-Item -ItemType Directory -Path $dstLua -Force | Out-Null
            Copy-Item -Path (Join-Path $srcLua "*") -Destination $dstLua -Recurse -Force
            Write-Host "[+] تم نسخ ملفات RequiredAssetsForRun\lua تلقائيًا إلى $dstLua" -ForegroundColor Green
        }
    }
}

function Invoke-ReadinessCheck {
    Write-Banner "فحص الجاهزية الشامل قبل التشغيل (Pre-Flight Check)"
    $okCount = 0
    $warnCount = 0
    $failCount = 0

    # 1. .NET 7 check
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $dotnetCmd) {
        $runtimes = & dotnet --list-runtimes 2>$null
        if ($runtimes -match "Microsoft\.NETCore\.App 7\.0\.") {
            Write-Host "[OK] حزمة .NET 7.0 Runtime مثبتة وجاهزة." -ForegroundColor Green
            $okCount++
        } else {
            Write-Host "[!!] أمر dotnet موجود لكن لم يظهر .NET 7.0 Runtime — ثبّت .NET 7.0 SDK (x64)." -ForegroundColor Red
            $failCount++
        }
    } else {
        Write-Host "[!!] لم يتم العثور على أمر dotnet — ثبّت .NET 7.0 SDK (x64)." -ForegroundColor Red
        $failCount++
    }

    # 2. Binaries check
    $binDir = Resolve-BinDirectory
    Write-Host "[i] مسار تشغيل السيرفر الفعلي: $binDir" -ForegroundColor Gray
    Ensure-LuaAssets $binDir

    foreach ($exe in @("MoonQueen.Login.exe", "MoonQueen.Game.exe", "MoonQueen.Ai.exe")) {
        $exePath = Join-Path $binDir $exe
        if (Test-Path -LiteralPath $exePath) {
            Write-Host "[OK] ملف التشغيل موجود: $exe" -ForegroundColor Green
            $okCount++
        } else {
            Write-Host "[!!] ملف التشغيل غير موجود بعد: $exe (افتح MoonQueen.sln في Visual Studio على وضع Release واعمل Build Solution)." -ForegroundColor Yellow
            $warnCount++
        }
    }

    # 3. Local Configs + SharedKey/SharedIV check
    $loginCfg = Read-JsonConfigSafe (Join-Path $binDir "MoonQueen.Login.Config.local.json")
    $gameCfg  = Read-JsonConfigSafe (Join-Path $binDir "MoonQueen.Game.Config.local.json")
    $aiCfg    = Read-JsonConfigSafe (Join-Path $binDir "MoonQueen.Ai.Config.local.json")

    if ($null -ne $loginCfg -and $null -ne $gameCfg -and $null -ne $aiCfg) {
        Write-Host "[OK] ملفات الإعداد المحلية الثلاثة (*.Config.local.json) موجودة وسليمة بصيغة JSON." -ForegroundColor Green
        $okCount++
        $lk = [string]$loginCfg.Auth.SharedKey
        $gk = [string]$gameCfg.Auth.SharedKey
        $li = [string]$loginCfg.Auth.SharedIV
        $gi = [string]$gameCfg.Auth.SharedIV
        if ($lk.Length -eq 32 -and $lk -eq $gk -and $li.Length -eq 16 -and $li -eq $gi) {
            Write-Host "[OK] مفاتيح التشفير المشتركة SharedKey (32) وSharedIV (16) متطابقة بين Login وGame." -ForegroundColor Green
            $okCount++
        } else {
            Write-Host "[!!] خطأ في SharedKey/SharedIV: لازم SharedKey يكون 32 حرف وSharedIV يكون 16 حرف ومتطابقين في Login وGame." -ForegroundColor Red
            $failCount++
        }
        if ($gameCfg.Realm.Name -eq "MoonQueen" -and $aiCfg.Ai.Name -eq "MoonQueen") {
            Write-Host "[OK] اسم العالم موحّد على MoonQueen في Game وAi." -ForegroundColor Green
            $okCount++
        } else {
            Write-Host "[!!] اسم السيرفر في Game أو Ai مختلف عن MoonQueen!" -ForegroundColor Red
            $failCount++
        }
    } else {
        Write-Host "[!!] ملف أو أكثر من (*.Config.local.json) غير موجود في $binDir (راجع الخطوة 4 في متطلبات التشغيل.md)." -ForegroundColor Yellow
        $warnCount++
    }

    # 4. Client assets copied to server Bin
    $gameMapDat = Join-Path $binDir "ini\GameMap.dat"
    $mapDir     = Join-Path $binDir "map"
    $luaIni     = Join-Path $binDir "lua\lua.ini"

    if (Test-Path -LiteralPath $gameMapDat) {
        Write-Host "[OK] ملف الخرائط الأساسي موجود: ini\GameMap.dat" -ForegroundColor Green
        $okCount++
    } else {
        Write-Host "[!!] ناقص: انسخ فولدر ini من كلينت 6192 إلى $binDir\ini" -ForegroundColor Yellow
        $warnCount++
    }

    if (Test-Path -LiteralPath $mapDir) {
        Write-Host "[OK] فولدر الخرائط map موجود داخل مسار السيرفر." -ForegroundColor Green
        $okCount++
    } else {
        Write-Host "[!!] ناقص: انسخ فولدر map من كلينت 6192 إلى $binDir\map" -ForegroundColor Yellow
        $warnCount++
    }

    if (Test-Path -LiteralPath $luaIni) {
        Write-Host "[OK] ملف سكربتات السيرفر موجود: lua\lua.ini" -ForegroundColor Green
        $okCount++
    } else {
        Write-Host "[!!] ناقص: انسخ RequiredAssetsForRun\lua إلى $binDir\lua" -ForegroundColor Yellow
        $warnCount++
    }

    # 5. MariaDB / MySQL check
    $dbHost = if ($null -ne $loginCfg -and $loginCfg.Database.Hostname) { [string]$loginCfg.Database.Hostname } else { "127.0.0.1" }
    $dbPort = if ($null -ne $loginCfg -and $loginCfg.Database.Port) { [int]$loginCfg.Database.Port } else { 3306 }
    $realmIpInDb = $null

    if (Test-TcpPort $dbHost $dbPort) {
        Write-Host "[OK] خدمة MySQL / MariaDB شغالة ومفتوحة على ${dbHost}:${dbPort}" -ForegroundColor Green
        $okCount++

        $mysqlExe = Find-MySqlExe
        if ($null -ne $mysqlExe -and $null -ne $loginCfg -and $null -ne $loginCfg.Database) {
            $accSchema  = [string]$loginCfg.Database.Schema
            $gameSchema = if ($null -ne $gameCfg -and $gameCfg.Database.Schema) { [string]$gameCfg.Database.Schema } else { "moonqueen_game" }
            $res = Invoke-MySqlQuery $mysqlExe $dbHost $dbPort ([string]$loginCfg.Database.Username) ([string]$loginCfg.Database.Password) $accSchema "SELECT CONCAT(Name, '|', GameIPAddress, '|', GamePort) FROM realm LIMIT 1;"
            if ($res.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($res.Output)) {
                $parts = $res.Output.Split('|')
                $realmName = $parts[0]
                $realmIpInDb = $parts[1]
                $modeLabel = if ($realmIpInDb -eq "127.0.0.1") { "وضع الأوفلاين المحلي (بدون نت)" } else { "وضع الشبكة / الهماشي" }
                Write-Host "[OK] قاعدة $accSchema جاهزة — العالم: $realmName | الآي بي الحالي: $realmIpInDb ($modeLabel)" -ForegroundColor Green
                $okCount++
            } else {
                Write-Host "[!!] تعذر قراءة جدول realm من قاعدة $accSchema — تأكد من استيراد SQL وتنفيذ استعلام التجهيز." -ForegroundColor Yellow
                $warnCount++
            }

            $resGame = Invoke-MySqlQuery $mysqlExe $dbHost $dbPort ([string]$loginCfg.Database.Username) ([string]$loginCfg.Database.Password) $gameSchema "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$gameSchema';"
            if ($resGame.ExitCode -eq 0 -and [int]$resGame.Output -ge 50) {
                Write-Host "[OK] قاعدة $gameSchema جاهزة وتحتوي على $($resGame.Output) جدول." -ForegroundColor Green
                $okCount++
            } else {
                Write-Host "[!!] قاعدة $gameSchema غير مستوردة بالكامل بعد." -ForegroundColor Yellow
                $warnCount++
            }
        }
    } else {
        Write-Host "[!!] خدمة MySQL متوقفة حاليًا على ${dbHost}:${dbPort} — افتح XAMPP واضغط Start قدام MySQL." -ForegroundColor Red
        $failCount++
    }

    # 6. Client directory & IP sync check
    $clientDir = Resolve-ClientDirectory -PromptIfMissing:$false
    if ($null -ne $clientDir) {
        $clientCfgPath  = Join-Path $clientDir "config.json"
        $clientLockPath = Join-Path $clientDir "config.json.lock"
        $clientExePath  = Join-Path $clientDir "MoonQueen.exe"
        if (Test-Path -LiteralPath $clientLockPath) {
            Write-Host "[!!] تنبيه: يوجد ملف config.json.lock داخل الكلينت ($clientDir) — شغّل أداة التحويل (أوفلاين/هماشي) لمسحه تلقائيًا." -ForegroundColor Yellow
            $warnCount++
        }
        $clientCfg = Read-JsonConfigSafe $clientCfgPath
        if ($null -ne $clientCfg) {
            $cIp = [string]$clientCfg.Hosts[0].IPAddress
            $cName = [string]$clientCfg.Hosts[0].ServerName
            Write-Host "[OK] ملف config.json في الكلينت موجود — السيرفر: $cName | الآي بي: $cIp" -ForegroundColor Green
            $okCount++
            if (-not [string]::IsNullOrWhiteSpace($realmIpInDb) -and $cIp -ne $realmIpInDb) {
                Write-Host "[!!] عدم تطابق الآي بي! الداتابيز على ($realmIpInDb) بينما الكلينت على ($cIp) — شغّل ملف التحويل 2 أو 3 لتوحيدهم فورًا." -ForegroundColor Red
                $failCount++
            }
        }
        if (-not (Test-Path -LiteralPath $clientExePath)) {
            Write-Host "[!!] ملف اللودر MoonQueen.exe غير منسوخ بعد داخل الكلينت ($clientDir)." -ForegroundColor Yellow
            $warnCount++
        }
    } else {
        Write-Host "[i] لم يتم تحديد فولدر الكلينت بعد (سيتم سؤالك عنه مرة واحدة عند استخدام أداة تبديل الأوفلاين/الهماشي)." -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "------------------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host "  ملخص الفحص: سليم ($okCount) | تنبيهات تجهيز ($warnCount) | أخطاء تحتاج حل ($failCount)" -ForegroundColor Cyan
    Write-Host "------------------------------------------------------------------" -ForegroundColor DarkGray
}

function Switch-NetworkMode([string]$TargetIP, [string]$ModeTitle) {
    Write-Banner "التحويل الفوري إلى $ModeTitle ($TargetIP)"
    $binDir = Resolve-BinDirectory
    $mysqlExe = Find-MySqlExe
    if ($null -eq $mysqlExe) {
        Write-Host "[!!] لم يتم العثور على mysql.exe الخاص بـ XAMPP." -ForegroundColor Red
        return
    }

    $creds = Get-AccountDbCredentials $binDir -PromptIfMissing:$true
    if (-not (Test-TcpPort $creds.Host $creds.Port)) {
        Write-Host "[!!] خدمة MySQL غير شغالة على $($creds.Host):$($creds.Port) — شغّل MySQL من XAMPP أولًا." -ForegroundColor Red
        return
    }

    $sql = "UPDATE realm SET GameIPAddress = '$TargetIP', RpcIPAddress = '127.0.0.1' WHERE Name = 'MoonQueen';"
    $res = Invoke-MySqlQuery $mysqlExe $creds.Host $creds.Port $creds.User $creds.Password $creds.Schema $sql
    if ($res.ExitCode -ne 0) {
        Write-Host "[!!] فشل تحديث جدول realm في $($creds.Schema): $($res.Error)" -ForegroundColor Red
        return
    }
    Write-Host "[OK] تم تحديث جدول realm في قاعدة $($creds.Schema) إلى الآي بي: $TargetIP" -ForegroundColor Green

    $clientDir = Resolve-ClientDirectory -PromptIfMissing:$true
    if ($null -ne $clientDir) {
        $templateCfg = Join-Path $RepoRoot "لورد الكلينت\config.json"
        $clientCfgPath = Join-Path $clientDir "config.json"
        $clientLockPath = Join-Path $clientDir "config.json.lock"

        if (Test-Path -LiteralPath $clientLockPath) {
            Remove-Item -LiteralPath $clientLockPath -Force
            Write-Host "[OK] تم حذف ملف القفل القديم config.json.lock من فولدر الكلينت." -ForegroundColor Green
        }

        $sourceToRead = if (Test-Path -LiteralPath $clientCfgPath) { $clientCfgPath } else { $templateCfg }
        if (Test-Path -LiteralPath $sourceToRead) {
            $raw = Get-Content -LiteralPath $sourceToRead -Raw -Encoding UTF8
            $replacement = '${1}"' + $TargetIP + '"'
            $updatedRaw = [regex]::Replace($raw, '("IPAddress"\s*:\s*)"[^"]*"', $replacement)
            [System.IO.File]::WriteAllText($clientCfgPath, $updatedRaw, (New-Object System.Text.UTF8Encoding($false)))
            Write-Host "[OK] تم ضبط ملف الكلينت ($clientCfgPath) على الآي بي: $TargetIP" -ForegroundColor Green
        }
    } else {
        Write-Host "[!] تم تحديث قاعدة البيانات بنجاح؛ تذكّر تعديل IPAddress في config.json داخل فولدر الكلينت إلى $TargetIP." -ForegroundColor Yellow
    }

    $runningLogin = Get-Process -Name "MoonQueen.Login" -ErrorAction SilentlyContinue
    if ($null -ne $runningLogin) {
        Write-Host ""
        Write-Host "[تنبيه مهم] سيرفر MoonQueen.Login شغال حاليًا — أعد تشغيل السيرفر (ملف 5 ثم ملف 4) عشان يلقط الآي بي الجديد ($TargetIP)." -ForegroundColor Yellow
    } else {
        Write-Host ""
        Write-Host "[تم بنجاح] السيرفر والكلينت جاهزان الآن للعمل على $ModeTitle ($TargetIP)!" -ForegroundColor Cyan
    }
}

function Start-AllServices {
    Write-Banner "تشغيل سيرفرات MoonQueen الثلاثة + استوديو التحكم الفوري"
    $binDir = Resolve-BinDirectory
    Ensure-LuaAssets $binDir

    if (-not (Test-TcpPort "127.0.0.1" 3306)) {
        Write-Host "[!!] خدمة MySQL متوقفة على 127.0.0.1:3306 — افتح XAMPP وشغّل MySQL الأول." -ForegroundColor Red
        return
    }

    $ordered = @(
        @{ Proc = "MoonQueen.Login"; Exe = "MoonQueen.Login.exe"; Delay = 3; Label = "سيرفر تسجيل الدخول (Login)" },
        @{ Proc = "MoonQueen.Game";  Exe = "MoonQueen.Game.exe";  Delay = 5; Label = "سيرفر عالم اللعبة (Game)" },
        @{ Proc = "MoonQueen.Ai";    Exe = "MoonQueen.Ai.exe";    Delay = 2; Label = "سيرفر الوحوش والـ Spawns (Ai)" }
    )

    foreach ($item in $ordered) {
        $exePath = Join-Path $binDir $item.Exe
        if (-not (Test-Path -LiteralPath $exePath)) {
            Write-Host "[!!] ملف $($item.Exe) غير موجود في $binDir — ابنِ السورس على وضع Release أولًا." -ForegroundColor Red
            return
        }
        $existing = Get-Process -Name $item.Proc -ErrorAction SilentlyContinue
        if ($null -ne $existing) {
            Write-Host "[=] $($item.Label) شغال بالفعل." -ForegroundColor Yellow
        } else {
            Write-Host "[+] جاري تشغيل $($item.Label)..." -ForegroundColor Green
            Start-Process -FilePath $exePath -WorkingDirectory $binDir
            Start-Sleep -Seconds $item.Delay
        }
    }

    # Start MoonQueen Control Studio on 127.0.0.1:8099 if PHP is available
    $studioDir = Join-Path $RepoRoot "لوحة تحكم السيرفر"
    $phpExe = Find-PhpExe
    if ((Test-Path -LiteralPath $studioDir) -and $null -ne $phpExe) {
        if (-not (Test-TcpPort "127.0.0.1" 8099)) {
            Write-Host "[+] جاري تشغيل استوديو تحكم MoonQueen على http://127.0.0.1:8099 ..." -ForegroundColor Green
            Start-Process -FilePath $phpExe -ArgumentList '-S 127.0.0.1:8099 router.php' -WorkingDirectory $studioDir -WindowStyle Minimized
            Start-Sleep -Seconds 1
        }
        Start-Process "http://127.0.0.1:8099"
    }

    Write-Host ""
    Write-Host "[تم] كل خدمات MoonQueen شغالة الآن! تقدر تفتح MoonQueen.exe من فولدر الكلينت وتدخل اللعبة." -ForegroundColor Cyan
}

function Stop-AllServices {
    Write-Banner "إيقاف سيرفرات MoonQueen بأمان (Ai -> Game -> Login)"
    foreach ($name in @("MoonQueen.Ai", "MoonQueen.Game", "MoonQueen.Login")) {
        $procs = Get-Process -Name $name -ErrorAction SilentlyContinue
        if ($null -ne $procs) {
            foreach ($p in $procs) {
                Write-Host "[-] جاري إيقاف $name (PID: $($p.Id))..." -ForegroundColor Yellow
                Stop-Process -Id $p.Id -Force
            }
            Start-Sleep -Seconds 1
        } else {
            Write-Host "[=] $name متوقف بالفعل." -ForegroundColor DarkGray
        }
    }
    Write-Host ""
    Write-Host "[تم] تم إيقاف سيرفرات MoonQueen الثلاثة بنجاح." -ForegroundColor Green
}

function Invoke-CryptoHelper {
    Write-Banner "مولّد تشفير الربط الداخلي (Realm RPC) وحساب الأدمن لأول تشغيل"
    Write-Host "الأداة دي بتولّدلك تشفير جدول realm وحسابك الشخصي محليًا على جهازك بدون حفظ أي باسورد في أي ملف." -ForegroundColor Gray
    Write-Host ""

    $modeChoice = (Read-Host "اختر وضع التشغيل المبدئي: [1] أوفلاين بدون نت (127.0.0.1)  |  [2] هماشي ($HamachiIP) [افتراضي: 1]").Trim()
    $targetIp = if ($modeChoice -eq "2") { $HamachiIP } else { "127.0.0.1" }

    $rpcUser = (Read-Host "اسم مستخدم الربط الداخلي بين Login وGame [اضغط Enter للافتراضي MoonQueenRealmUser]").Trim()
    if ([string]::IsNullOrWhiteSpace($rpcUser)) { $rpcUser = "MoonQueenRealmUser" }

    $rpcPass = (Read-Host "اكتب باسورد قوي للربط الداخلي (نفس اللي هتحطه في MoonQueen.Game.Config.local.json)").Trim()
    if ([string]::IsNullOrWhiteSpace($rpcPass)) {
        Write-Host "[!!] لا يمكن ترك باسورد الربط الداخلي فارغًا." -ForegroundColor Red
        return
    }

    $adminUser = (Read-Host "اسم حساب الأدمن في اللعبة [اضغط Enter للافتراضي ahmedzahran]").Trim()
    if ([string]::IsNullOrWhiteSpace($adminUser)) { $adminUser = "ahmedzahran" }

    $adminPass = (Read-Host "اكتب باسورد حسابك في اللعبة").Trim()
    if ([string]::IsNullOrWhiteSpace($adminPass)) {
        Write-Host "[!!] لا يمكن ترك باسورد حساب اللعبة فارغًا." -ForegroundColor Red
        return
    }

    $rngBytes = New-Object byte[] 16
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($rngBytes)
    $rng.Dispose()
    $salt = "MQ" + (-join ($rngBytes | ForEach-Object { $_.ToString("x2") }))

    $keyHex = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("OTEzMTFhMjFlMDI4NWQ0NjY3N2FhNzVkNDRjNzI3YWM="))
    $key = New-Object byte[] ($keyHex.Length / 2)
    for ($i = 0; $i -lt $keyHex.Length; $i += 2) {
        $key[$i / 2] = [System.Convert]::ToByte($keyHex.Substring($i, 2), 16)
    }

    function Encrypt-RealmValue([string]$PlainText) {
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

    $encRpcUser = Encrypt-RealmValue $rpcUser
    $encRpcPass = Encrypt-RealmValue $rpcPass

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha.ComputeHash([System.Text.Encoding]::ASCII.GetBytes($adminPass + $salt))
        $passHash  = -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
    } finally {
        $sha.Dispose()
    }

    $sqlTemplate = @'
USE `moonqueen_account`;

TRUNCATE TABLE `conquer_account`;
TRUNCATE TABLE `conquer_account_vip`;
TRUNCATE TABLE `realm`;

INSERT INTO `realm` (
  `RealmID`, `Name`, `GameIPAddress`, `RpcIPAddress`,
  `GamePort`, `RpcPort`, `Status`, `Username`, `Password`,
  `Active`, `ProductionRealm`
) VALUES (
  'e418a91a-9574-4962-842b-4729238d3965',
  'MoonQueen',
  '__TARGET_IP__',
  '127.0.0.1',
  5816,
  9865,
  1,
  '__ENC_RPC_USER__',
  '__ENC_RPC_PASS__',
  1,
  1
);

INSERT INTO `conquer_account` (
  `Id`, `UserName`, `Password`, `Salt`,
  `AuthorityId`, `Flag`, `IpAddress`, `MacAddress`,
  `ParentId`, `Created`
) VALUES (
  1,
  '__ADMIN_USER__',
  '__PASS_HASH__',
  '__SALT__',
  8,
  1,
  '127.0.0.1',
  '000000000000',
  UUID(),
  NOW()
);
'@

    $sqlBlock = $sqlTemplate.
        Replace("__TARGET_IP__", $targetIp).
        Replace("__ENC_RPC_USER__", $encRpcUser.Replace("'", "''")).
        Replace("__ENC_RPC_PASS__", $encRpcPass.Replace("'", "''")).
        Replace("__ADMIN_USER__", $adminUser.Replace("'", "''")).
        Replace("__PASS_HASH__", $passHash).
        Replace("__SALT__", $salt)

    Write-Host ""
    Write-Host "================ استعلام SQL الجاهز للتنفيذ في HeidiSQL ================" -ForegroundColor Green
    Write-Host $sqlBlock -ForegroundColor White
    Write-Host "========================================================================" -ForegroundColor Green

    try {
        Set-Clipboard -Value $sqlBlock
        Write-Host "[OK] تم نسخ كود الـ SQL تلقائيًا إلى الحافظة (Clipboard) — تقدر تعمل Paste على طول جوّه HeidiSQL!" -ForegroundColor Cyan
    } catch {}
}

switch ($Mode) {
    "Check"        { Invoke-ReadinessCheck }
    "Offline"      { Switch-NetworkMode -TargetIP "127.0.0.1" -ModeTitle "وضع الأوفلاين المحلي بدون إنترنت" }
    "Hamachi"      { Switch-NetworkMode -TargetIP $HamachiIP  -ModeTitle "وضع شبكة الهماشي" }
    "StartAll"     { Start-AllServices }
    "StopAll"      { Stop-AllServices }
    "CryptoHelper" { Invoke-CryptoHelper }
    default {
        while ($true) {
            Write-Banner "القائمة الرئيسية لإدارة وتشغيل السيرفر"
            Write-Host "  [1] فحص الجاهزية الشامل قبل التشغيل (Pre-Flight Check)" -ForegroundColor White
            Write-Host "  [2] التحويل إلى وضع الأوفلاين بدون إنترنت (127.0.0.1)" -ForegroundColor Green
            Write-Host "  [3] التحويل إلى وضع الهماشي ($HamachiIP)" -ForegroundColor Cyan
            Write-Host "  [4] تشغيل السيرفرات الثلاثة + استوديو التحكم بالترتيب" -ForegroundColor Yellow
            Write-Host "  [5] إيقاف السيرفرات الثلاثة بأمان" -ForegroundColor Magenta
            Write-Host "  [6] مولّد تشفير الربط وحساب الأدمن (نسخ SQL جاهز لـ HeidiSQL)" -ForegroundColor Gray
            Write-Host "  [0] خروج" -ForegroundColor DarkGray
            Write-Host ""
            $choice = (Read-Host "اختر رقم العملية").Trim()
            switch ($choice) {
                "1" { Invoke-ReadinessCheck; Write-Host ""; Read-Host "اضغط Enter للعودة للقائمة" | Out-Null }
                "2" { Switch-NetworkMode -TargetIP "127.0.0.1" -ModeTitle "وضع الأوفلاين المحلي بدون إنترنت"; Write-Host ""; Read-Host "اضغط Enter للعودة للقائمة" | Out-Null }
                "3" { Switch-NetworkMode -TargetIP $HamachiIP  -ModeTitle "وضع شبكة الهماشي"; Write-Host ""; Read-Host "اضغط Enter للعودة للقائمة" | Out-Null }
                "4" { Start-AllServices; Write-Host ""; Read-Host "اضغط Enter للعودة للقائمة" | Out-Null }
                "5" { Stop-AllServices; Write-Host ""; Read-Host "اضغط Enter للعودة للقائمة" | Out-Null }
                "6" { Invoke-CryptoHelper; Write-Host ""; Read-Host "اضغط Enter للعودة للقائمة" | Out-Null }
                "0" { break }
            }
            if ($choice -eq "0") { break }
        }
    }
}
