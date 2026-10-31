<?php
declare(strict_types=1);

if (!defined('MOONQUEEN_PORTAL')) {
    http_response_code(403);
    exit('Forbidden');
}

// إعداد الجلسة الآمنة لحماية الـ CSRF والـ Anti-Bot
if (session_status() === PHP_SESSION_NONE) {
    ini_set('session.use_strict_mode', '1');
    ini_set('session.cookie_httponly', '1');
    session_set_cookie_params([
        'lifetime' => 0,
        'path'     => '/',
        'httponly' => true,
        'samesite' => 'Lax',
    ]);
    session_start();
}

/**
 * خزنة التحقق المشفر من حقوق المطور الرسمي (MoonQueen | ahmedzahran)
 */
function mq_verify_developer_rights(): array
{
    $vault = [
        0xD0, 0xEA, 0x0E, 0x2F, 0xBC, 0x70, 0x7C, 0x2C, 0xD3, 0x89, 0xF0,
        0x39, 0x20, 0x50, 0x9D, 0x53, 0x3C, 0x0D, 0xF3, 0x80, 0xA3, 0x19,
        0x49, 0x46, 0x8F, 0xC1, 0xF0, 0xFD, 0xFB, 0xC3, 0x08, 0x30, 0x6F,
    ];
    $decoded = '';
    $count = count($vault);
    for ($i = 0; $i < $count; $i++) {
        $key = ((0xC3 + ($i * 0x29)) ^ (0x5E + ($i * 11))) & 0xFF;
        $decoded .= chr($vault[$i] ^ $key);
    }

    $expectedSeal = '24f07893fd3a1945a9c7ebe34c50d6f8bc4d6f92a89dbb2ab2b0c696d227773f';
    if (!hash_equals($expectedSeal, hash('sha256', $decoded))) {
        http_response_code(500);
        exit('Developer rights integrity check failed.');
    }

    $parts = explode('|', $decoded);
    return [
        'server_name' => $parts[0],
        'developer'   => $parts[1],
        'edition'     => $parts[2],
    ];
}

$MQ_RIGHTS = mq_verify_developer_rights();

/**
 * تحميل إعدادات الداتابيز (config.local.php إن وجد، وإلا config.sample.php) + إعدادات المحتوى
 */
function mq_load_configs(): array
{
    $baseDir = dirname(__DIR__);
    $localPath = $baseDir . '/config.local.php';
    $samplePath = $baseDir . '/config.sample.php';
    $siteContentPath = $baseDir . '/إعدادات_الموقع.php';

    $hasLocalConfig = is_file($localPath);
    $sysCfg = require ($hasLocalConfig ? $localPath : $samplePath);
    $sysCfg['_has_local_config'] = $hasLocalConfig;

    $siteCfg = is_file($siteContentPath) ? (require $siteContentPath) : [];
    return [$sysCfg, $siteCfg];
}

function mq_is_db_configured(array $sysCfg): bool
{
    if (empty($sysCfg['_has_local_config'])) {
        return false;
    }
    $pw = (string)($sysCfg['db']['password'] ?? '');
    if ($pw === 'PUT_YOUR_DB_PASSWORD_HERE' || $pw === 'اكتب_باسورد_الداتابيز_بتاعك_هنا') {
        return false;
    }
    return true;
}

function mq_open_pdo(array $sysCfg, string $schemaKey): PDO
{
    $db = $sysCfg['db'];
    $host = $db['host'] ?? '127.0.0.1';
    $port = (int)($db['port'] ?? 3306);
    $schema = $db[$schemaKey];
    $charset = $db['charset'] ?? 'utf8mb4';

    $dsn = sprintf('mysql:host=%s;port=%d;dbname=%s;charset=%s', $host, $port, $schema, $charset);
    return new PDO($dsn, (string)$db['username'], (string)$db['password'], [
        PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        PDO::ATTR_EMULATE_PREPARES   => false,
        PDO::ATTR_TIMEOUT            => 3,
    ]);
}

/**
 * دوال التشفير المطابقة 100% لكلاس GameAccount في سورس MoonQueen.Login
 */
function mq_generate_salt(): string
{
    $pool = 'QWERTYUIOPASDFGHJKLZXCVBNMqwertyuioplkjhgfdsazxcvbnm1236547890';
    $maxIdx = strlen($pool) - 1;
    $out = '';
    for ($i = 0; $i < 36; $i++) {
        $out .= $pool[random_int(0, $maxIdx)];
    }
    return $out;
}

function mq_hash_password(string $password, string $salt): string
{
    return hash('sha256', $password . $salt);
}

function mq_check_password(string $input, string $storedHash, ?string $salt): bool
{
    if ($input === '' || $storedHash === '') {
        return false;
    }
    $computed = mq_hash_password($input, $salt ?? '');
    return hash_equals(strtolower($storedHash), strtolower($computed));
}

function mq_uuid_v4(): string
{
    $data = random_bytes(16);
    $data[6] = chr((ord($data[6]) & 0x0f) | 0x40);
    $data[8] = chr((ord($data[8]) & 0x3f) | 0x80);
    return vsprintf('%s%s-%s-%s-%s-%s%s%s', str_split(bin2hex($data), 4));
}

/**
 * دوال الحماية: CSRF + سؤال التحقق البسيط ضد البوتات + Rate Limit لكل IP
 */
function mq_csrf_token(): string
{
    if (empty($_SESSION['mq_csrf_token'])) {
        $_SESSION['mq_csrf_token'] = bin2hex(random_bytes(32));
    }
    return (string)$_SESSION['mq_csrf_token'];
}

function mq_verify_csrf(string $token): bool
{
    $expected = (string)($_SESSION['mq_csrf_token'] ?? '');
    return $expected !== '' && $token !== '' && hash_equals($expected, $token);
}

function mq_refresh_captcha(): string
{
    $a = random_int(2, 9);
    $b = random_int(1, 9);
    $_SESSION['mq_captcha_answer'] = (string)($a + $b);
    $_SESSION['mq_captcha_label'] = sprintf('كم ناتج %d + %d ؟', $a, $b);
    return $_SESSION['mq_captcha_label'];
}

function mq_get_captcha_label(): string
{
    if (empty($_SESSION['mq_captcha_label']) || !isset($_SESSION['mq_captcha_answer'])) {
        return mq_refresh_captcha();
    }
    return (string)$_SESSION['mq_captcha_label'];
}

function mq_verify_captcha(string $input): bool
{
    $expected = (string)($_SESSION['mq_captcha_answer'] ?? '');
    $clean = trim($input);
    $arabicDigits = ['٠','١','٢','٣','٤','٥','٦','٧','٨','٩'];
    $latinDigits  = ['0','1','2','3','4','5','6','7','8','9'];
    $clean = str_replace($arabicDigits, $latinDigits, $clean);
    return $expected !== '' && $clean !== '' && hash_equals($expected, $clean);
}

function mq_client_ip(): string
{
    $ip = (string)($_SERVER['REMOTE_ADDR'] ?? '127.0.0.1');
    return substr($ip, 0, 45);
}

function mq_check_rate_limit(array $sysCfg, string $bucket): bool
{
    $cacheDir = dirname(__DIR__) . '/cache';
    if (!is_dir($cacheDir)) {
        @mkdir($cacheDir, 0750, true);
    }

    $maxAttempts = (int)($sysCfg['rate_limit']['max_attempts'] ?? 8);
    $windowSec   = (int)($sysCfg['rate_limit']['window_sec'] ?? 300);
    $keyHash     = hash('sha256', $bucket . '|' . mq_client_ip());
    $file        = $cacheDir . '/rl_' . substr($keyHash, 0, 24) . '.json';

    $now = time();
    $timestamps = [];
    if (is_file($file)) {
        $raw = @file_get_contents($file);
        $decoded = is_string($raw) ? json_decode($raw, true) : null;
        if (is_array($decoded)) {
            foreach ($decoded as $ts) {
                if (is_int($ts) && ($now - $ts) < $windowSec) {
                    $timestamps[] = $ts;
                }
            }
        }
    }

    if (count($timestamps) >= $maxAttempts) {
        return false;
    }

    $timestamps[] = $now;
    @file_put_contents($file, json_encode($timestamps), LOCK_EX);
    return true;
}

/**
 * تحويل كود التخصص في كونكر 6192 لاسم واضح ومفهوم للاعب
 */
function mq_profession_label(int $prof): string
{
    if ($prof >= 10 && $prof <= 15) return 'تروجان (Trojan)';
    if ($prof >= 20 && $prof <= 25) return 'واريور (Warrior)';
    if ($prof >= 40 && $prof <= 45) return 'أرشر (Archer)';
    if ($prof >= 50 && $prof <= 55) return 'نينجا (Ninja)';
    if ($prof >= 60 && $prof <= 65) return 'مونك (Monk)';
    if ($prof >= 70 && $prof <= 75) return 'قرصان (Pirate)';
    if ($prof >= 80 && $prof <= 85) return 'دراجون واريور (Dragon)';
    if ($prof >= 132 && $prof <= 135) return 'وتر تاوست (Water)';
    if ($prof >= 142 && $prof <= 145) return 'فاير تاوست (Fire)';
    if ($prof >= 100 && $prof <= 102) return 'تاوست (Taoist)';
    return 'محارب (Hero)';
}

function mq_check_port(string $host, int $port, float $timeout): bool
{
    $errno = 0;
    $errstr = '';
    $fp = @fsockopen($host, $port, $errno, $errstr, $timeout);
    if (is_resource($fp)) {
        fclose($fp);
        return true;
    }
    return false;
}

/**
 * جلب حالة السيرفر واللاعبين الأونلاين وأقوى الأبطال مع كاش خفيف (15 ثانية) لعدم التحميل على السيرفر
 */
function mq_get_live_stats(array $sysCfg): array
{
    $cacheDir = dirname(__DIR__) . '/cache';
    if (!is_dir($cacheDir)) {
        @mkdir($cacheDir, 0750, true);
    }

    $ttl = max(5, (int)($sysCfg['server_check']['cache_ttl_sec'] ?? 15));
    $cacheFile = $cacheDir . '/live_stats.json';

    if (is_file($cacheFile) && (time() - (int)@filemtime($cacheFile)) < $ttl) {
        $cached = @file_get_contents($cacheFile);
        $decoded = is_string($cached) ? json_decode($cached, true) : null;
        if (is_array($decoded)) {
            return $decoded;
        }
    }

    $checkHost = (string)($sysCfg['server_check']['host'] ?? '127.0.0.1');
    $loginPort = (int)($sysCfg['server_check']['login_port'] ?? 9958);
    $gamePort  = (int)($sysCfg['server_check']['game_port'] ?? 5816);
    $timeout   = (float)($sysCfg['server_check']['socket_timeout_sec'] ?? 0.35);

    $loginOnline = mq_check_port($checkHost, $loginPort, $timeout);
    $gameOnline  = mq_check_port($checkHost, $gamePort, $timeout);
    $dbReady     = mq_is_db_configured($sysCfg);

    $stats = [
        'server_online'   => ($loginOnline && $gameOnline),
        'login_online'    => $loginOnline,
        'game_online'     => $gameOnline,
        'db_configured'   => $dbReady,
        'online_count'    => 0,
        'total_accounts'  => 0,
        'total_heroes'    => 0,
        'online_players'  => [],
        'top_heroes'      => [],
        'updated_at'      => date('H:i:s'),
    ];

    if (!$dbReady) {
        return $stats;
    }

    try {
        $accPdo = mq_open_pdo($sysCfg, 'account_schema');
        $stats['total_accounts'] = (int)$accPdo->query('SELECT COUNT(*) FROM `conquer_account`')->fetchColumn();
    } catch (Throwable $e) {
        // نحتفظ باستقرار الواجهة حتى لو قاعدة الحسابات قيد التشغيل
    }

    try {
        $gamePdo = mq_open_pdo($sysCfg, 'game_schema');
        $stats['total_heroes'] = (int)$gamePdo->query('SELECT COUNT(*) FROM `cq_user`')->fetchColumn();

        if ($gameOnline) {
            $stats['online_count'] = (int)$gamePdo->query(
                'SELECT COUNT(*) FROM `cq_user` WHERE `last_login` > 0 AND `last_login` > `last_logout`'
            )->fetchColumn();

            $stmtOnline = $gamePdo->query(
                'SELECT `name`, `level`, `metempsychosis`, `profession`, `pk`
                 FROM `cq_user`
                 WHERE `last_login` > 0 AND `last_login` > `last_logout`
                 ORDER BY `metempsychosis` DESC, `level` DESC, `pk` DESC
                 LIMIT 50'
            );
            while ($row = $stmtOnline->fetch()) {
                $stats['online_players'][] = [
                    'name'       => (string)$row['name'],
                    'level'      => (int)$row['level'],
                    'reborn'     => (int)$row['metempsychosis'],
                    'profession' => mq_profession_label((int)$row['profession']),
                    'pk'         => (int)$row['pk'],
                ];
            }
        }

        $stmtTop = $gamePdo->query(
            'SELECT `name`, `level`, `metempsychosis`, `profession`, `pk`
             FROM `cq_user`
             ORDER BY `metempsychosis` DESC, `level` DESC, `exp` DESC
             LIMIT 12'
        );
        while ($row = $stmtTop->fetch()) {
            $stats['top_heroes'][] = [
                'name'       => (string)$row['name'],
                'level'      => (int)$row['level'],
                'reborn'     => (int)$row['metempsychosis'],
                'profession' => mq_profession_label((int)$row['profession']),
                'pk'         => (int)$row['pk'],
            ];
        }
    } catch (Throwable $e) {
        // نحتفظ باستقرار الواجهة في جميع الحالات
    }

    @file_put_contents($cacheFile, json_encode($stats, JSON_UNESCAPED_UNICODE), LOCK_EX);
    return $stats;
}

function mq_esc(string $val): string
{
    return htmlspecialchars($val, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
}
