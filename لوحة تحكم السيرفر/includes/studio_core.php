<?php
declare(strict_types=1);

/**
 * MoonQueen Control Studio — Core Engine & Live Sync Bridge
 * Brand: MoonQueen | Developer: ahmedzahran (احمد زهران)
 */

const MQ_STUDIO_BRAND = 'MoonQueen';
const MQ_STUDIO_DEV_EN = 'ahmedzahran';
const MQ_STUDIO_DEV_AR = 'احمد زهران';
const MQ_STUDIO_SEAL_SHA256 = '6c48c68206466d66367d294891463a2a123616c1b973361001c8750215303f25';

function mq_verify_developer_seal(): void
{
    $payload = MQ_STUDIO_BRAND . '|' . MQ_STUDIO_DEV_EN . '|' . MQ_STUDIO_DEV_AR;
    $expected = hash('sha256', 'MoonQueen|ahmedzahran|احمد زهران');
    if ($payload !== 'MoonQueen|ahmedzahran|احمد زهران' || !hash_equals($expected, hash('sha256', $payload))) {
        http_response_code(500);
        exit('Tamper detected: MoonQueen developer rights seal (ahmedzahran) is immutable.');
    }
}

mq_verify_developer_seal();

function mq_enforce_local_only(): void
{
    $remote = $_SERVER['REMOTE_ADDR'] ?? '';
    $allowed = ['127.0.0.1', '::1', '::ffff:127.0.0.1'];
    if (!in_array($remote, $allowed, true)) {
        http_response_code(403);
        header('Content-Type: text/plain; charset=utf-8');
        exit('403 Forbidden — لوحة تحكم MoonQueen مسموح بفتحها من جهاز المالك فقط (127.0.0.1).');
    }
}

mq_enforce_local_only();

header('X-Content-Type-Options: nosniff');
header('X-Frame-Options: SAMEORIGIN');
header('Referrer-Policy: no-referrer');
header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');

if (session_status() === PHP_SESSION_NONE) {
    session_name('MQ_STUDIO_SESS');
    session_set_cookie_params([
        'lifetime' => 0,
        'path' => '/',
        'httponly' => true,
        'samesite' => 'Strict',
    ]);
    session_start();
}

function mq_studio_root(): string
{
    return dirname(__DIR__);
}

function mq_repo_root(): string
{
    return dirname(mq_studio_root());
}

function mq_local_config_path(): string
{
    return mq_studio_root() . DIRECTORY_SEPARATOR . 'config.local.php';
}

function mq_local_state_path(): string
{
    return mq_studio_root() . DIRECTORY_SEPARATOR . 'studio-state.local.json';
}

function mq_load_config(): array
{
    $base = require mq_studio_root() . DIRECTORY_SEPARATOR . 'config.sample.php';
    $localFile = mq_local_config_path();
    if (is_file($localFile)) {
        $local = require $localFile;
        if (is_array($local)) {
            $base = array_merge($base, $local);
        }
    }
    return $base;
}

function mq_save_local_config(array $values): bool
{
    $current = [];
    $localFile = mq_local_config_path();
    if (is_file($localFile)) {
        $loaded = require $localFile;
        if (is_array($loaded)) {
            $current = $loaded;
        }
    }
    $merged = array_merge($current, $values);
    $export = "<?php\ndeclare(strict_types=1);\n\n// Auto-generated local owner configuration (gitignored)\nreturn " . var_export($merged, true) . ";\n";
    $tmp = $localFile . '.tmp';
    if (file_put_contents($tmp, $export, LOCK_EX) === false) {
        return false;
    }
    return @rename($tmp, $localFile);
}

function mq_load_local_state(): array
{
    $path = mq_local_state_path();
    if (!is_file($path)) {
        return [];
    }
    $raw = @file_get_contents($path);
    if (!is_string($raw) || $raw === '') {
        return [];
    }
    $decoded = json_decode($raw, true);
    return is_array($decoded) ? $decoded : [];
}

function mq_save_local_state(array $patch): bool
{
    $merged = array_merge(mq_load_local_state(), $patch);
    $path = mq_local_state_path();
    $tmp = $path . '.tmp';
    $json = json_encode($merged, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    if ($json === false || file_put_contents($tmp, $json, LOCK_EX) === false) {
        return false;
    }
    return @rename($tmp, $path);
}

function mq_csrf_token(): string
{
    if (empty($_SESSION['mq_csrf'])) {
        $_SESSION['mq_csrf'] = bin2hex(random_bytes(32));
    }
    return (string)$_SESSION['mq_csrf'];
}

function mq_verify_csrf(?string $token): bool
{
    if (!is_string($token) || $token === '' || empty($_SESSION['mq_csrf'])) {
        return false;
    }
    return hash_equals((string)$_SESSION['mq_csrf'], $token);
}

function mq_is_configured(): bool
{
    $cfg = mq_load_config();
    return !empty($cfg['admin_password_hash']) && is_string($cfg['admin_password_hash']);
}

function mq_is_authenticated(): bool
{
    if (!mq_is_configured()) {
        return false;
    }
    if (empty($_SESSION['mq_admin_ok'])) {
        return false;
    }
    $cfg = mq_load_config();
    $idleMax = (int)($cfg['session_Idle_seconds'] ?? 28800);
    $lastActive = (int)($_SESSION['mq_last_active'] ?? 0);
    if ($lastActive > 0 && (time() - $lastActive) > $idleMax) {
        session_unset();
        return false;
    }
    $_SESSION['mq_last_active'] = time();
    return true;
}

function mq_default_rates(): array
{
    return [
        'PresetName' => 'Classic+ Default (1x)',
        'ExperienceRate' => 1.0,
        'MagicExpRate' => 1.0,
        'WeaponSkillExpRate' => 1.0,
        'SilverDropRate' => 1.0,
        'SilverDropChancePercent' => 25,
        'EquipmentDropRate' => 1.0,
        'QualityItemRate' => 1.0,
        'DragonBallDropRate' => 1.0,
        'MeteorDropPer100k' => 0,
        'MonsterCpChancePercent' => 0.0,
        'MonsterCpMin' => 1,
        'MonsterCpMax' => 3,
        'BroadcastMessage' => '',
        'BroadcastChannel' => 'TopLeft',
        'BroadcastStamp' => 0,
        'ReloadActionsStamp' => 0,
    ];
}

function mq_clamp_float($val, float $min, float $max, float $fallback): float
{
    if (!is_numeric($val)) {
        return $fallback;
    }
    $f = (float)$val;
    if (!is_finite($f)) {
        return $fallback;
    }
    return round(max($min, min($max, $f)), 4);
}

function mq_clamp_int($val, int $min, int $max, int $fallback): int
{
    if (!is_numeric($val)) {
        return $fallback;
    }
    $i = (int)round((float)$val);
    return max($min, min($max, $i));
}

function mq_normalize_rates(array $input, ?array $existing = null): array
{
    $base = array_merge(mq_default_rates(), $existing ?? []);
    $preset = trim((string)($input['PresetName'] ?? $base['PresetName']));
    if ($preset === '') {
        $preset = 'Classic+ Custom';
    }
    $preset = mb_substr($preset, 0, 64, 'UTF-8');

    $cpMin = mq_clamp_int($input['MonsterCpMin'] ?? $base['MonsterCpMin'], 1, 100000, 1);
    $cpMax = mq_clamp_int($input['MonsterCpMax'] ?? $base['MonsterCpMax'], $cpMin, 100000, max(3, $cpMin));

    $channel = trim((string)($input['BroadcastChannel'] ?? $base['BroadcastChannel']));
    if (!in_array($channel, ['TopLeft', 'Center', 'Talk'], true)) {
        $channel = 'TopLeft';
    }

    $msg = trim((string)($input['BroadcastMessage'] ?? $base['BroadcastMessage']));
    $msg = mb_substr($msg, 0, 180, 'UTF-8');

    return [
        'PresetName' => $preset,
        'ExperienceRate' => mq_clamp_float($input['ExperienceRate'] ?? $base['ExperienceRate'], 0.05, 100.0, 1.0),
        'MagicExpRate' => mq_clamp_float($input['MagicExpRate'] ?? $base['MagicExpRate'], 0.05, 100.0, 1.0),
        'WeaponSkillExpRate' => mq_clamp_float($input['WeaponSkillExpRate'] ?? $base['WeaponSkillExpRate'], 0.05, 100.0, 1.0),
        'SilverDropRate' => mq_clamp_float($input['SilverDropRate'] ?? $base['SilverDropRate'], 0.0, 100.0, 1.0),
        'SilverDropChancePercent' => mq_clamp_int($input['SilverDropChancePercent'] ?? $base['SilverDropChancePercent'], 0, 100, 25),
        'EquipmentDropRate' => mq_clamp_float($input['EquipmentDropRate'] ?? $base['EquipmentDropRate'], 0.0, 25.0, 1.0),
        'QualityItemRate' => mq_clamp_float($input['QualityItemRate'] ?? $base['QualityItemRate'], 0.0, 50.0, 1.0),
        'DragonBallDropRate' => mq_clamp_float($input['DragonBallDropRate'] ?? $base['DragonBallDropRate'], 0.0, 100.0, 1.0),
        'MeteorDropPer100k' => mq_clamp_int($input['MeteorDropPer100k'] ?? $base['MeteorDropPer100k'], 0, 100000, 0),
        'MonsterCpChancePercent' => mq_clamp_float($input['MonsterCpChancePercent'] ?? $base['MonsterCpChancePercent'], 0.0, 100.0, 0.0),
        'MonsterCpMin' => $cpMin,
        'MonsterCpMax' => $cpMax,
        'BroadcastMessage' => $msg,
        'BroadcastChannel' => $channel,
        'BroadcastStamp' => mq_clamp_int($input['BroadcastStamp'] ?? $base['BroadcastStamp'], 0, PHP_INT_MAX, 0),
        'ReloadActionsStamp' => mq_clamp_int($input['ReloadActionsStamp'] ?? $base['ReloadActionsStamp'], 0, PHP_INT_MAX, 0),
    ];
}

/**
 * Resolves primary + mirror target paths for MoonQueen.Rates.json
 * so Debug and Release builds in src/Bin stay automatically synchronized.
 */
function mq_resolve_targets(): array
{
    $cfg = mq_load_config();
    $state = mq_load_local_state();
    $custom = trim((string)($state['custom_rates_path'] ?? $cfg['rates_file_path'] ?? ''));

    if ($custom !== '') {
        if (is_dir($custom)) {
            $custom = rtrim($custom, "\\/") . DIRECTORY_SEPARATOR . 'MoonQueen.Rates.json';
        }
        return [
            'mode' => 'custom',
            'primary' => $custom,
            'mirrors' => [],
        ];
    }

    $repo = mq_repo_root();
    $debugDir = $repo . DIRECTORY_SEPARATOR . 'src' . DIRECTORY_SEPARATOR . 'Bin' . DIRECTORY_SEPARATOR . 'Debug' . DIRECTORY_SEPARATOR . 'net7.0';
    $releaseDir = $repo . DIRECTORY_SEPARATOR . 'src' . DIRECTORY_SEPARATOR . 'Bin' . DIRECTORY_SEPARATOR . 'Release' . DIRECTORY_SEPARATOR . 'net7.0';

    $debugRates = $debugDir . DIRECTORY_SEPARATOR . 'MoonQueen.Rates.json';
    $releaseRates = $releaseDir . DIRECTORY_SEPARATOR . 'MoonQueen.Rates.json';

    $debugStatus = $debugDir . DIRECTORY_SEPARATOR . 'MoonQueen.Rates.status.json';
    $releaseStatus = $releaseDir . DIRECTORY_SEPARATOR . 'MoonQueen.Rates.status.json';

    $debugMtime = is_file($debugStatus) ? (int)@filemtime($debugStatus) : 0;
    $releaseMtime = is_file($releaseStatus) ? (int)@filemtime($releaseStatus) : 0;

    if ($releaseMtime > $debugMtime) {
        return [
            'mode' => 'auto',
            'primary' => $releaseRates,
            'mirrors' => is_dir($debugDir) ? [$debugRates] : [],
        ];
    }

    if (is_dir($releaseDir) && !is_dir($debugDir)) {
        return [
            'mode' => 'auto',
            'primary' => $releaseRates,
            'mirrors' => [],
        ];
    }

    $mirrors = [];
    if (is_dir($releaseDir)) {
        $mirrors[] = $releaseRates;
    }

    return [
        'mode' => 'auto',
        'primary' => $debugRates,
        'mirrors' => $mirrors,
    ];
}

function mq_read_rates(): array
{
    $targets = mq_resolve_targets();
    $primary = $targets['primary'];
    if (is_file($primary)) {
        $raw = @file_get_contents($primary);
        if (is_string($raw) && $raw !== '') {
            $parsed = json_decode($raw, true);
            if (is_array($parsed)) {
                return mq_normalize_rates($parsed);
            }
        }
    }
    return mq_default_rates();
}

function mq_write_rates_atomic(array $rates): array
{
    $targets = mq_resolve_targets();
    $allPaths = array_merge([$targets['primary']], $targets['mirrors']);
    $json = json_encode($rates, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . "\n";

    $written = [];
    $errors = [];

    foreach ($allPaths as $idx => $path) {
        $dir = dirname($path);
        if (!is_dir($dir)) {
            if ($idx === 0) {
                @mkdir($dir, 0775, true);
            } else {
                continue;
            }
        }
        $tmp = $path . '.studio.tmp';
        if (@file_put_contents($tmp, $json, LOCK_EX) === false || !@rename($tmp, $path)) {
            @unlink($tmp);
            if ($idx === 0) {
                $errors[] = 'تعذر الكتابة في المسار: ' . $path;
            }
        } else {
            $written[] = $path;
        }
    }

    return [
        'ok' => empty($errors) && !empty($written),
        'primary' => $targets['primary'],
        'written' => $written,
        'errors' => $errors,
    ];
}

function mq_read_server_status(): array
{
    $targets = mq_resolve_targets();
    $primary = $targets['primary'];
    $statusPath = preg_replace('/\.json$/i', '.status.json', $primary) ?: ($primary . '.status.json');

    $result = [
        'live_connected' => false,
        'mode' => $targets['mode'],
        'rates_path' => $primary,
        'status_path' => $statusPath,
        'rates_file_exists' => is_file($primary),
        'server_name' => MQ_STUDIO_BRAND,
        'developer' => MQ_STUDIO_DEV_EN,
        'online_players' => 0,
        'max_online' => 0,
        'last_reload_at' => 'لم يتم التشغيل بعد',
        'updated_at' => '—',
        'seconds_since_heartbeat' => null,
        'active_rates' => null,
    ];

    if (!is_file($statusPath)) {
        return $result;
    }

    $raw = @file_get_contents($statusPath);
    if (!is_string($raw) || $raw === '') {
        return $result;
    }

    $snap = json_decode($raw, true);
    if (!is_array($snap)) {
        return $result;
    }

    $updatedUnix = isset($snap['updated_unix']) ? (int)$snap['updated_unix'] : (int)@filemtime($statusPath);
    $age = max(0, time() - $updatedUnix);

    $result['live_connected'] = ($age <= 12);
    $result['online_players'] = (int)($snap['online_players'] ?? 0);
    $result['max_online'] = (int)($snap['max_online'] ?? 0);
    $result['last_reload_at'] = (string)($snap['last_reload_at'] ?? '—');
    $result['updated_at'] = (string)($snap['updated_at'] ?? '—');
    $result['seconds_since_heartbeat'] = $age;
    $result['active_rates'] = is_array($snap['active_rates'] ?? null) ? $snap['active_rates'] : null;

    return $result;
}
