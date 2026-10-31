<?php
declare(strict_types=1);

require_once __DIR__ . '/includes/studio_core.php';

header('Content-Type: application/json; charset=utf-8');

if (!mq_is_authenticated()) {
    http_response_code(401);
    echo json_encode([
        'ok' => false,
        'error' => 'انتهت الجلسة، يرجى تسجيل الدخول مجدداً.',
    ], JSON_UNESCAPED_UNICODE);
    exit;
}

$action = (string)($_GET['action'] ?? $_POST['action'] ?? 'status');

if ($action === 'status') {
    echo json_encode([
        'ok' => true,
        'status' => mq_read_server_status(),
        'saved_rates' => mq_read_rates(),
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

if ($action === 'shops') {
    echo json_encode([
        'ok' => true,
        'shops_data' => mq_read_shops_snapshot(),
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

if ($action === 'search_results') {
    echo json_encode([
        'ok' => true,
        'search' => mq_read_search_snapshot(),
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['ok' => false, 'error' => 'Method Not Allowed'], JSON_UNESCAPED_UNICODE);
    exit;
}

$rawBody = file_get_contents('php://input');
$payload = is_string($rawBody) && $rawBody !== '' ? json_decode($rawBody, true) : $_POST;
if (!is_array($payload)) {
    $payload = [];
}

$csrf = (string)($payload['csrf_token'] ?? $_SERVER['HTTP_X_CSRF_TOKEN'] ?? '');
if (!mq_verify_csrf($csrf)) {
    http_response_code(403);
    echo json_encode(['ok' => false, 'error' => 'رمز الحماية (CSRF) غير صالح، حدّث الصفحة وحاول مجدداً.'], JSON_UNESCAPED_UNICODE);
    exit;
}

if ($action === 'save_rates') {
    $existing = mq_read_rates();
    $incoming = is_array($payload['rates'] ?? null) ? $payload['rates'] : $payload;
    $normalized = mq_normalize_rates($incoming, $existing);

    $autoAnnounce = !empty($payload['announce_on_save']);
    $announceMsg = trim((string)($payload['announce_message'] ?? ''));
    if ($autoAnnounce && $announceMsg !== '') {
        $normalized['BroadcastMessage'] = mb_substr($announceMsg, 0, 180, 'UTF-8');
        $normalized['BroadcastChannel'] = in_array(($payload['announce_channel'] ?? ''), ['TopLeft', 'Center', 'Talk'], true)
            ? $payload['announce_channel']
            : 'TopLeft';
        $normalized['BroadcastStamp'] = (int)round(microtime(true) * 1000);
    }

    $writeRes = mq_write_rates_atomic($normalized);
    echo json_encode([
        'ok' => $writeRes['ok'],
        'message' => $writeRes['ok']
            ? 'تم حفظ الريتات وإرسال إشارة الريلود الفوري إلى MoonQueen.Game!'
            : implode(' | ', $writeRes['errors']),
        'rates' => $normalized,
        'write' => $writeRes,
        'status' => mq_read_server_status(),
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

if ($action === 'broadcast') {
    $msg = trim((string)($payload['message'] ?? ''));
    if ($msg === '') {
        echo json_encode(['ok' => false, 'error' => 'اكتب نص الإعلان أولاً قبل الإرسال.'], JSON_UNESCAPED_UNICODE);
        exit;
    }
    $channel = (string)($payload['channel'] ?? 'TopLeft');
    if (!in_array($channel, ['TopLeft', 'Center', 'Talk'], true)) {
        $channel = 'TopLeft';
    }

    $existing = mq_read_rates();
    $existing['BroadcastMessage'] = mb_substr($msg, 0, 180, 'UTF-8');
    $existing['BroadcastChannel'] = $channel;
    $existing['BroadcastStamp'] = (int)round(microtime(true) * 1000);

    $writeRes = mq_write_rates_atomic($existing);
    echo json_encode([
        'ok' => $writeRes['ok'],
        'message' => $writeRes['ok']
            ? 'تم إرسال الإعلان العالمي إلى السيرفر (يظهر للاعبين خلال أقل من ثانية)!'
            : implode(' | ', $writeRes['errors']),
        'rates' => $existing,
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

if ($action === 'reload_actions') {
    $existing = mq_read_rates();
    $existing['ReloadActionsStamp'] = (int)round(microtime(true) * 1000);

    $writeRes = mq_write_rates_atomic($existing);
    echo json_encode([
        'ok' => $writeRes['ok'],
        'message' => $writeRes['ok']
            ? 'تم إرسال أمر ريلود فوري لـ cq_action و cq_task وسكربتات Lua!'
            : implode(' | ', $writeRes['errors']),
        'rates' => $existing,
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

if ($action === 'set_target_path') {
    $customPath = trim((string)($payload['custom_rates_path'] ?? ''));
    if ($customPath !== '') {
        if (is_dir($customPath)) {
            $customPath = rtrim($customPath, "\\/") . DIRECTORY_SEPARATOR . 'MoonQueen.Rates.json';
        } elseif (!preg_match('/MoonQueen\.Rates\.json$/i', $customPath)) {
            echo json_encode([
                'ok' => false,
                'error' => 'المسار يجب أن يشير إلى مجلد MoonQueen.Game.exe أو ملف MoonQueen.Rates.json مباشرة.',
            ], JSON_UNESCAPED_UNICODE);
            exit;
        }
    }

    $saved = mq_save_local_state(['custom_rates_path' => $customPath]);
    echo json_encode([
        'ok' => $saved,
        'message' => $customPath === ''
            ? 'تم تفعيل وضع الاكتشاف التلقائي الذكي (Debug + Release)!'
            : 'تم ربط اللوحة بالمسار المخصص بنجاح!',
        'status' => mq_read_server_status(),
        'saved_rates' => mq_read_rates(),
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

if ($action === 'studio_command') {
    $cmdType = strtolower(trim((string)($payload['type'] ?? '')));
    $allowedTypes = [
        'event_control',
        'shop_reload',
        'shop_upsert',
        'shop_delete',
        'item_update_price',
        'catalog_search',
        'player_reward',
        'reward_all_online',
        'player_action',
        'boss_spawn',
        'boss_clear',
    ];

    if (!in_array($cmdType, $allowedTypes, true)) {
        http_response_code(400);
        echo json_encode(['ok' => false, 'error' => 'نوع الأمر غير مدعوم: ' . $cmdType], JSON_UNESCAPED_UNICODE);
        exit;
    }

    $cmd = [
        'Type' => $cmdType,
        'EventKey' => mb_substr(trim((string)($payload['event_key'] ?? '')), 0, 40, 'UTF-8'),
        'Action' => mb_substr(trim((string)($payload['sub_action'] ?? '')), 0, 32, 'UTF-8'),
        'DurationMinutes' => mq_clamp_int($payload['duration_minutes'] ?? 60, 5, 360, 60),
        'Broadcast' => !empty($payload['broadcast']),
        'NpcId' => mq_clamp_int($payload['npc_id'] ?? 0, 0, 2000000000, 0),
        'GoodsId' => mq_clamp_int($payload['goods_id'] ?? 0, 0, 2000000000, 0),
        'ItemType' => mq_clamp_int($payload['item_type'] ?? 0, 0, 2000000000, 0),
        'MoneyType' => mq_clamp_int($payload['money_type'] ?? 0, 0, 5, 0),
        'SilverPrice' => isset($payload['silver_price']) && $payload['silver_price'] !== '' ? mq_clamp_int($payload['silver_price'], 0, 2000000000, -1) : -1,
        'CpPrice' => isset($payload['cp_price']) && $payload['cp_price'] !== '' ? mq_clamp_int($payload['cp_price'], 0, 2000000000, -1) : -1,
        'BoundCpPrice' => isset($payload['bound_cp_price']) && $payload['bound_cp_price'] !== '' ? mq_clamp_int($payload['bound_cp_price'], 0, 2000000000, -1) : -1,
        'HonorPrice' => isset($payload['honor_price']) && $payload['honor_price'] !== '' ? mq_clamp_int($payload['honor_price'], 0, 2000000000, -1) : -1,
        'RidingPrice' => isset($payload['riding_price']) && $payload['riding_price'] !== '' ? mq_clamp_int($payload['riding_price'], 0, 2000000000, -1) : -1,
        'PlayerId' => mq_clamp_int($payload['player_id'] ?? 0, 0, 2000000000, 0),
        'PlayerName' => mb_substr(trim((string)($payload['player_name'] ?? '')), 0, 32, 'UTF-8'),
        'SilverAmount' => mq_clamp_int($payload['silver_amount'] ?? 0, 0, 2000000000, 0),
        'CpAmount' => mq_clamp_int($payload['cp_amount'] ?? 0, 0, 100000000, 0),
        'BoundCpAmount' => mq_clamp_int($payload['bound_cp_amount'] ?? 0, 0, 100000000, 0),
        'BlessingHours' => mq_clamp_int($payload['blessing_hours'] ?? 0, 0, 8760, 0),
        'VipLevel' => isset($payload['vip_level']) && $payload['vip_level'] !== '' && (int)$payload['vip_level'] >= 0
            ? mq_clamp_int($payload['vip_level'], 0, 6, -1)
            : -1,
        'ItemAmount' => mq_clamp_int($payload['item_amount'] ?? 1, 1, 20, 1),
        'Plus' => mq_clamp_int($payload['plus'] ?? 0, 0, 12, 0),
        'Bless' => mq_clamp_int($payload['bless'] ?? 0, 0, 7, 0),
        'Enchant' => mq_clamp_int($payload['enchant'] ?? 0, 0, 255, 0),
        'SocketOne' => mq_clamp_int($payload['socket_one'] ?? 0, 0, 255, 0),
        'SocketTwo' => mq_clamp_int($payload['socket_two'] ?? 0, 0, 255, 0),
        'Bound' => !empty($payload['bound']),
        'Message' => mb_substr(trim((string)($payload['message'] ?? '')), 0, 180, 'UTF-8'),
        'MapId' => mq_clamp_int($payload['map_id'] ?? 0, 0, 2000000000, 0),
        'X' => mq_clamp_int($payload['x'] ?? 0, 0, 2000, 0),
        'Y' => mq_clamp_int($payload['y'] ?? 0, 0, 2000, 0),
        'MonsterType' => mq_clamp_int($payload['monster_type'] ?? 0, 0, 2000000000, 0),
        'Count' => mq_clamp_int($payload['count'] ?? 1, 1, 25, 1),
        'ScatterRadius' => mq_clamp_int($payload['scatter_radius'] ?? 3, 0, 25, 3),
        'RoleId' => mq_clamp_int($payload['role_id'] ?? 0, 0, 2000000000, 0),
        'Query' => mb_substr(trim((string)($payload['query'] ?? '')), 0, 60, 'UTF-8'),
    ];

    $queued = mq_enqueue_studio_command($cmd);
    echo json_encode([
        'ok' => $queued['ok'],
        'cmd_id' => $queued['cmd_id'],
        'message' => $queued['ok']
            ? '⚡ تم إرسال الأمر الفوري إلى MoonQueen.Game — جاري التنفيذ خلال أقل من ثانية!'
            : ($queued['error'] ?? 'تعذر إرسال الأمر.'),
    ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

http_response_code(400);
echo json_encode(['ok' => false, 'error' => 'أمر غير معروف.'], JSON_UNESCAPED_UNICODE);
