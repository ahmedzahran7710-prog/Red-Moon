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
        // Basic sanity check: prevent traversal to unexpected extensions
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

http_response_code(400);
echo json_encode(['ok' => false, 'error' => 'أمر غير معروف.'], JSON_UNESCAPED_UNICODE);
