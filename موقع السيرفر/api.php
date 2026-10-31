<?php
declare(strict_types=1);

define('MOONQUEEN_PORTAL', true);
require_once __DIR__ . '/includes/bootstrap.php';

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

[$sysCfg, $siteCfg] = mq_load_configs();

$method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
$action = (string)($_GET['action'] ?? $_POST['action'] ?? '');

function mq_json_reply(int $status, bool $ok, string $message, array $extra = []): void
{
    http_response_code($status);
    echo json_encode(array_merge([
        'ok'            => $ok,
        'message'       => $message,
        'captcha_label' => mq_get_captcha_label(),
    ], $extra), JSON_UNESCAPED_UNICODE);
    exit;
}

// 1. استعلام حي خفيف لحالة السيرفر واللاعبين الأونلاين
if ($method === 'GET' && $action === 'stats') {
    $stats = mq_get_live_stats($sysCfg);
    mq_json_reply(200, true, 'OK', ['stats' => $stats]);
}

if ($method !== 'POST') {
    mq_json_reply(405, false, 'طريقة الطلب غير مدعومة.');
}

$csrf = (string)($_POST['csrf_token'] ?? '');
if (!mq_verify_csrf($csrf)) {
    mq_refresh_captcha();
    mq_json_reply(400, false, 'انتهت صلاحية الجلسة الأمنية، حدّث الصفحة وجرّب تاني.');
}

$captcha = (string)($_POST['captcha'] ?? '');
$captchaOk = mq_verify_captcha($captcha);
mq_refresh_captcha(); // نجدد السؤال دايمًا بعد كل محاولة لمنع الإعادة الآلية
if (!$captchaOk) {
    mq_json_reply(400, false, 'إجابة سؤال الأمان غير صحيحة، جرّب السؤال الجديد.');
}

if (!mq_is_db_configured($sysCfg)) {
    mq_json_reply(503, false, 'قاعدة البيانات لسه متضبطتش في ملف config.local.php على السيرفر.');
}

/**
 * فحص صيغة اسم المستخدم وكلمة المرور بما يطابق كلينت وسيرفر MoonQueen V6192 (يدعم كل الرموز وحرف z بالكامل)
 */
function mq_validate_username(string $username): ?string
{
    if (strlen($username) < 4 || strlen($username) > 16) {
        return 'اسم المستخدم لازم يكون من 4 إلى 16 حرف أو رقم إنجليزي.';
    }
    if (!preg_match('/^[A-Za-z0-9]+$/', $username)) {
        return 'اسم المستخدم يقبل الحروف والأرقام الإنجليزية فقط بدون مسافات.';
    }
    return null;
}

function mq_validate_password(string $password): ?string
{
    $len = strlen($password);
    if ($len < 6 || $len > 31) {
        return 'كلمة المرور لازم تكون من 6 إلى 31 حرف أو رقم إنجليزي.';
    }
    if (!preg_match('/^[\x21-\x7E]+$/', $password)) {
        return 'كلمة المرور لازم تكون بحروف أو أرقام أو رموز إنجليزية بدون مسافات.';
    }
    return null;
}

// 2. إنشاء حساب جديد
if ($action === 'register') {
    if (!mq_check_rate_limit($sysCfg, 'register')) {
        mq_json_reply(429, false, 'محاولات كتير في وقت قصير! استنى دقيقتين وجرّب تاني.');
    }

    $username = trim((string)($_POST['username'] ?? ''));
    $password = (string)($_POST['password'] ?? '');
    $confirm  = (string)($_POST['confirm_password'] ?? '');

    if ($err = mq_validate_username($username)) {
        mq_json_reply(400, false, $err);
    }
    if ($err = mq_validate_password($password)) {
        mq_json_reply(400, false, $err);
    }
    if (!hash_equals($password, $confirm)) {
        mq_json_reply(400, false, 'تأكيد كلمة المرور غير مطابق لكلمة المرور.');
    }

    try {
        $pdo = mq_open_pdo($sysCfg, 'account_schema');

        $chk = $pdo->prepare('SELECT `Id` FROM `conquer_account` WHERE LOWER(`UserName`) = LOWER(:u) LIMIT 1');
        $chk->execute([':u' => $username]);
        if ($chk->fetch()) {
            mq_json_reply(409, false, 'اسم المستخدم ده محجوز بالفعل، اختار اسم تاني.');
        }

        $salt = mq_generate_salt();
        $hash = mq_hash_password($password, $salt);
        $uuid = mq_uuid_v4();
        $ip   = mq_client_ip();

        $ins = $pdo->prepare(
            'INSERT INTO `conquer_account`
                (`UserName`, `Password`, `Salt`, `AuthorityId`, `Flag`, `IpAddress`, `MacAddress`, `ParentId`, `Created`)
             VALUES
                (:u, :p, :s, 1, 1, :ip, :mac, :uid, NOW())'
        );
        $ins->execute([
            ':u'   => $username,
            ':p'   => $hash,
            ':s'   => $salt,
            ':ip'  => $ip,
            ':mac' => '000000000000',
            ':uid' => $uuid,
        ]);

        // مسح كاش الإحصائيات عشان عداد الحسابات يزيد فورًا
        @unlink(__DIR__ . '/cache/live_stats.json');

        mq_json_reply(200, true, sprintf('ألف مبروك! تم إنشاء حسابك (%s) بنجاح — افتح اللودر MoonQueen.exe وادخل للعبة دلوقتي!', $username));
    } catch (Throwable $e) {
        mq_json_reply(500, false, 'تعذر الاتصال بقاعدة الحسابات حاليًا، تأكد من تشغيل MySQL واستيراد جدول الحسابات.');
    }
}

// 3. تغيير كلمة المرور
if ($action === 'change_password') {
    if (!mq_check_rate_limit($sysCfg, 'change_password')) {
        mq_json_reply(429, false, 'محاولات كتير في وقت قصير! استنى دقيقتين وجرّب تاني.');
    }

    $username    = trim((string)($_POST['username'] ?? ''));
    $oldPassword = (string)($_POST['old_password'] ?? '');
    $newPassword = (string)($_POST['new_password'] ?? '');
    $confirmNew  = (string)($_POST['confirm_password'] ?? '');

    if ($err = mq_validate_username($username)) {
        mq_json_reply(400, false, $err);
    }
    if ($oldPassword === '') {
        mq_json_reply(400, false, 'اكتب كلمة المرور الحالية الأول.');
    }
    if ($err = mq_validate_password($newPassword)) {
        mq_json_reply(400, false, $err);
    }
    if (!hash_equals($newPassword, $confirmNew)) {
        mq_json_reply(400, false, 'تأكيد كلمة المرور الجديدة غير مطابق.');
    }
    if (hash_equals($oldPassword, $newPassword)) {
        mq_json_reply(400, false, 'كلمة المرور الجديدة لازم تكون مختلفة عن القديمة.');
    }

    try {
        $pdo = mq_open_pdo($sysCfg, 'account_schema');

        $stmt = $pdo->prepare(
            'SELECT `Id`, `UserName`, `Password`, `Salt`, `Flag`
             FROM `conquer_account`
             WHERE LOWER(`UserName`) = LOWER(:u)
             LIMIT 1'
        );
        $stmt->execute([':u' => $username]);
        $acc = $stmt->fetch();

        if (!$acc || !mq_check_password($oldPassword, (string)$acc['Password'], (string)($acc['Salt'] ?? ''))) {
            mq_json_reply(401, false, 'اسم المستخدم أو كلمة المرور الحالية غير صحيحة.');
        }

        $flag = (int)$acc['Flag'];
        if (in_array($flag, [2, 3, 4], true)) {
            mq_json_reply(403, false, 'هذا الحساب موقوف أو مقفول ولا يمكن تعديل كلمة مروره.');
        }

        $newSalt = mq_generate_salt();
        $newHash = mq_hash_password($newPassword, $newSalt);

        $upd = $pdo->prepare(
            'UPDATE `conquer_account`
             SET `Password` = :p, `Salt` = :s, `Modified` = NOW()
             WHERE `Id` = :id'
        );
        $upd->execute([
            ':p'  => $newHash,
            ':s'  => $newSalt,
            ':id' => (int)$acc['Id'],
        ]);

        mq_json_reply(200, true, 'تم تغيير كلمة المرور وتأمين حسابك بنجاح! تقدر تسجّل دخولك بالباسورد الجديد فورًا.');
    } catch (Throwable $e) {
        mq_json_reply(500, false, 'تعذر الاتصال بقاعدة الحسابات حاليًا، حاول بعد لحظات.');
    }
}

mq_json_reply(400, false, 'طلب غير معروف.');
