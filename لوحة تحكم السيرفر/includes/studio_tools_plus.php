<?php
declare(strict_types=1);

/**
 * MoonQueen Control Studio — Extended Live Admin Suite (Tools #5 -> #10)
 * Brand: MoonQueen | Developer: ahmedzahran (احمد زهران)
 *
 * Tools in strict owner-approved order:
 *  #5 (1) Accounts, Ban & Unstuck Studio
 *  #6 (2) Promo & Gift Codes Studio (/redeem, /code, /gift)
 *  #7 (3) Live Inventory & Warehouse Inspector
 *  #8 (4) Auto-Broadcast & Schedule Studio
 *  #9 (5) Boss & Monster Custom Drop Studio (100% Pure Official 1.0x default)
 * #10 (6) 1-Click Database Backup & Economy Radar
 */

function mq_backups_dir(): string
{
    $dir = mq_studio_root() . DIRECTORY_SEPARATOR . 'backups';
    if (!is_dir($dir)) {
        @mkdir($dir, 0750, true);
    }
    return $dir;
}

function mq_write_json_atomic(string $path, array $data): bool
{
    $dir = dirname($path);
    if (!is_dir($dir)) {
        @mkdir($dir, 0755, true);
    }
    $encoded = json_encode($data, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    if ($encoded === false) {
        return false;
    }
    $tmp = $path . '.tmp.' . bin2hex(random_bytes(4));
    if (@file_put_contents($tmp, $encoded, LOCK_EX) === false) {
        @unlink($tmp);
        return false;
    }
    if (!@rename($tmp, $path)) {
        $copied = @copy($tmp, $path);
        @unlink($tmp);
        return $copied;
    }
    return true;
}

function mq_read_json_file(string $path, array $default = []): array
{
    if (!is_file($path)) {
        return $default;
    }
    $raw = @file_get_contents($path);
    if (!is_string($raw) || trim($raw) === '') {
        return $default;
    }
    // Strip optional UTF-8 BOM if manually edited in Windows Notepad
    if (strncmp($raw, "\xEF\xBB\xBF", 3) === 0) {
        $raw = substr($raw, 3);
    }
    $decoded = json_decode($raw, true);
    return is_array($decoded) ? $decoded : $default;
}

/**
 * Resolves local MariaDB/MySQL credentials automatically from src/Bin/net7.0/*.Config.local.json
 * (or optional local override in config.local.php) — zero plaintext passwords committed to Git.
 */
function mq_resolve_db_settings(): array
{
    $files = mq_resolve_studio_files();
    $baseDir = $files['base_dir'];
    $cfg = mq_load_config();

    $loginJson = mq_read_json_file($baseDir . DIRECTORY_SEPARATOR . 'MoonQueen.Login.Config.local.json');
    if (empty($loginJson)) {
        $loginJson = mq_read_json_file($baseDir . DIRECTORY_SEPARATOR . 'Canyon.Login.Config.local.json');
    }

    $gameJson = mq_read_json_file($baseDir . DIRECTORY_SEPARATOR . 'MoonQueen.Game.Config.local.json');
    if (empty($gameJson)) {
        $gameJson = mq_read_json_file($baseDir . DIRECTORY_SEPARATOR . 'Canyon.Game.Config.local.json');
    }

    $accDb = is_array($loginJson['Database'] ?? null) ? $loginJson['Database'] : [];
    $gameDb = is_array($gameJson['Database'] ?? null) ? $gameJson['Database'] : [];

    $host = trim((string)($cfg['db_host'] ?? $accDb['Hostname'] ?? $gameDb['Hostname'] ?? '127.0.0.1'));
    $port = (int)($cfg['db_port'] ?? $accDb['Port'] ?? $gameDb['Port'] ?? 3306);
    $user = trim((string)($cfg['db_user'] ?? $accDb['Username'] ?? $gameDb['Username'] ?? 'root'));
    $pass = (string)($cfg['db_pass'] ?? $accDb['Password'] ?? $gameDb['Password'] ?? '');
    $accSchema = trim((string)($cfg['db_account_schema'] ?? $accDb['Schema'] ?? 'moonqueen_account'));
    $gameSchema = trim((string)($cfg['db_game_schema'] ?? $gameDb['Schema'] ?? 'moonqueen_game'));

    $configured = (!empty($accDb) || !empty($gameDb) || isset($cfg['db_pass']));

    return [
        'configured' => $configured,
        'host' => $host !== '' ? $host : '127.0.0.1',
        'port' => $port > 0 ? $port : 3306,
        'user' => $user !== '' ? $user : 'root',
        'pass' => $pass,
        'account_schema' => $accSchema !== '' ? $accSchema : 'moonqueen_account',
        'game_schema' => $gameSchema !== '' ? $gameSchema : 'moonqueen_game',
    ];
}

function mq_open_pdo(string $target = 'game'): array
{
    static $pool = [];
    if (isset($pool[$target])) {
        return ['ok' => true, 'pdo' => $pool[$target], 'error' => ''];
    }

    if (!class_exists('PDO')) {
        return ['ok' => false, 'pdo' => null, 'error' => 'إضافة PDO غير مفعلة في PHP الخاص بـ XAMPP.'];
    }

    $db = mq_resolve_db_settings();
    $schema = ($target === 'account') ? $db['account_schema'] : $db['game_schema'];

    try {
        $dsn = sprintf('mysql:host=%s;port=%d;dbname=%s;charset=utf8mb4', $db['host'], $db['port'], $schema);
        $pdo = new PDO($dsn, $db['user'], $db['pass'], [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            PDO::ATTR_EMULATE_PREPARES => false,
            PDO::ATTR_TIMEOUT => 3,
        ]);
        $pool[$target] = $pdo;
        return ['ok' => true, 'pdo' => $pdo, 'error' => ''];
    } catch (Throwable $e) {
        $hint = !$db['configured']
            ? 'تأكد من إنشاء ملفات MoonQueen.Login.Config.local.json وMoonQueen.Game.Config.local.json داخل src/Bin/net7.0 وتشغيل MySQL من XAMPP.'
            : ('تعذر الاتصال بقاعدة ' . $schema . ' على 127.0.0.1:3306 — تأكد من تشغيل MySQL في XAMPP واستيراد القاعدة.');
        return ['ok' => false, 'pdo' => null, 'error' => $hint];
    }
}

// ============================================================================
// TOOL #5: ACCOUNTS, BAN & UNSTUCK STUDIO
// ============================================================================

function mq_generate_account_salt(): string
{
    $pool = 'QWERTYUIOPASDFGHJKLZXCVBNMqwertyuioplkjhgfdsazxcvbnm1236547890';
    $max = strlen($pool) - 1;
    $out = '';
    for ($i = 0; $i < 36; $i++) {
        $out .= $pool[random_int(0, $max)];
    }
    return $out;
}

function mq_hash_account_password(string $plainPassword, string $salt): string
{
    return hash('sha256', $plainPassword . $salt);
}

function mq_uuid_v4(): string
{
    $b = random_bytes(16);
    $b[6] = chr((ord($b[6]) & 0x0f) | 0x40);
    $b[8] = chr((ord($b[8]) & 0x3f) | 0x80);
    return vsprintf('%s%s-%s-%s-%s-%s%s%s', str_split(bin2hex($b), 4));
}

function mq_accounts_search(string $query = '', string $filter = 'all'): array
{
    $accConn = mq_open_pdo('account');
    if (!$accConn['ok']) {
        return ['ok' => false, 'db_ready' => false, 'error' => $accConn['error'], 'accounts' => []];
    }
    $pdoAcc = $accConn['pdo'];
    $gameConn = mq_open_pdo('game');
    $pdoGame = $gameConn['ok'] ? $gameConn['pdo'] : null;

    $q = trim($query);
    $matchedAccountIdsFromGame = [];

    if ($q !== '' && $pdoGame !== null) {
        try {
            if (ctype_digit($q)) {
                $stG = $pdoGame->prepare('SELECT account_id FROM cq_user WHERE id = :id OR account_id = :aid OR name LIKE :lk LIMIT 60');
                $stG->execute([':id' => (int)$q, ':aid' => (int)$q, ':lk' => '%' . $q . '%']);
            } else {
                $stG = $pdoGame->prepare('SELECT account_id FROM cq_user WHERE name LIKE :lk LIMIT 60');
                $stG->execute([':lk' => '%' . $q . '%']);
            }
            foreach ($stG->fetchAll() as $r) {
                $aid = (int)($r['account_id'] ?? 0);
                if ($aid > 0) {
                    $matchedAccountIdsFromGame[$aid] = true;
                }
            }
        } catch (Throwable $e) {
        }
    }

    $where = [];
    $params = [];

    if ($filter === 'banned') {
        $where[] = 'Flag IN (2, 3, 4)';
    } elseif ($filter === 'staff') {
        $where[] = 'AuthorityId >= 2';
    }

    if ($q !== '') {
        $orParts = ['UserName LIKE :qlk', 'IpAddress LIKE :iplk', 'MacAddress LIKE :maclk'];
        $params[':qlk'] = '%' . $q . '%';
        $params[':iplk'] = '%' . $q . '%';
        $params[':maclk'] = '%' . $q . '%';
        if (ctype_digit($q)) {
            $orParts[] = 'Id = :exactId';
            $params[':exactId'] = (int)$q;
        }
        if (!empty($matchedAccountIdsFromGame)) {
            $idList = implode(',', array_map('intval', array_keys($matchedAccountIdsFromGame)));
            $orParts[] = "Id IN ($idList)";
        }
        $where[] = '(' . implode(' OR ', $orParts) . ')';
    }

    $sql = 'SELECT Id, UserName, AuthorityId, Flag, IpAddress, MacAddress, Created FROM conquer_account';
    if (!empty($where)) {
        $sql .= ' WHERE ' . implode(' AND ', $where);
    }
    $sql .= ' ORDER BY Id DESC LIMIT 80';

    $st = $pdoAcc->prepare($sql);
    $st->execute($params);
    $accounts = $st->fetchAll();

    $charsByAccount = [];
    if (!empty($accounts) && $pdoGame !== null) {
        $accIds = array_values(array_unique(array_map(static fn($a) => (int)$a['Id'], $accounts)));
        $inSql = implode(',', $accIds);
        try {
            $stChars = $pdoGame->query(
                "SELECT id, account_id, name, level, metempsychosis, profession, money, emoney, emoney_mono, recordmap_id, recordx, recordy, pk
                 FROM cq_user WHERE account_id IN ($inSql)"
            );
            foreach ($stChars->fetchAll() as $ch) {
                $charsByAccount[(int)$ch['account_id']] = $ch;
            }
        } catch (Throwable $e) {
        }
    }

    $status = mq_read_server_status();
    $onlineByCharId = [];
    if (!empty($status['live_connected']) && !empty($status['online_players_list'])) {
        foreach ($status['online_players_list'] as $on) {
            $onlineByCharId[(int)($on['id'] ?? 0)] = $on;
        }
    }

    $rows = [];
    foreach ($accounts as $acc) {
        $aid = (int)$acc['Id'];
        $ch = $charsByAccount[$aid] ?? null;
        $cid = $ch ? (int)$ch['id'] : 0;
        $onlineInfo = ($cid > 0 && isset($onlineByCharId[$cid])) ? $onlineByCharId[$cid] : null;

        $rows[] = [
            'account_id' => $aid,
            'username' => (string)($acc['UserName'] ?? ''),
            'authority_id' => (int)($acc['AuthorityId'] ?? 1),
            'flag' => (int)($acc['Flag'] ?? 1),
            'ip' => (string)($acc['IpAddress'] ?? ''),
            'mac' => (string)($acc['MacAddress'] ?? ''),
            'created' => (string)($acc['Created'] ?? ''),
            'has_character' => ($ch !== null),
            'online' => ($onlineInfo !== null),
            'char_id' => $cid,
            'char_name' => $ch ? (string)$ch['name'] : '— بدون شخصية بعد —',
            'level' => $onlineInfo ? (int)$onlineInfo['level'] : ($ch ? (int)$ch['level'] : 0),
            'reborn' => $onlineInfo ? (int)$onlineInfo['reborn'] : ($ch ? (int)$ch['metempsychosis'] : 0),
            'silvers' => $onlineInfo ? (int)$onlineInfo['silvers'] : ($ch ? (int)$ch['money'] : 0),
            'cps' => $onlineInfo ? (int)$onlineInfo['cps'] : ($ch ? (int)$ch['emoney'] : 0),
            'bound_cps' => $onlineInfo ? (int)$onlineInfo['bound_cps'] : ($ch ? (int)$ch['emoney_mono'] : 0),
            'map_id' => $onlineInfo ? (int)$onlineInfo['map_id'] : ($ch ? (int)$ch['recordmap_id'] : 0),
            'x' => $onlineInfo ? (int)$onlineInfo['x'] : ($ch ? (int)$ch['recordx'] : 0),
            'y' => $onlineInfo ? (int)$onlineInfo['y'] : ($ch ? (int)$ch['recordy'] : 0),
        ];
    }

    return [
        'ok' => true,
        'db_ready' => true,
        'accounts' => $rows,
    ];
}

function mq_account_set_flag(int $accountId, int $flag, int $charId = 0): array
{
    if ($accountId <= 0) {
        return ['ok' => false, 'error' => 'رقم الحساب غير صالح.'];
    }
    if (!in_array($flag, [1, 2, 3, 4], true)) {
        return ['ok' => false, 'error' => 'قيمة حالة الحساب غير مدعومة.'];
    }

    $conn = mq_open_pdo('account');
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error']];
    }
    $pdo = $conn['pdo'];

    $check = $pdo->prepare('SELECT UserName, AuthorityId FROM conquer_account WHERE Id = :id LIMIT 1');
    $check->execute([':id' => $accountId]);
    $row = $check->fetch();
    if (!$row) {
        return ['ok' => false, 'error' => 'الحساب غير موجود.'];
    }
    if ($flag >= 2 && (int)$row['AuthorityId'] >= 8) {
        return ['ok' => false, 'error' => 'حماية الأمان: لا يمكن حظر حساب مالك السيرفر (Authority 8 / PM).'];
    }

    $upd = $pdo->prepare('UPDATE conquer_account SET Flag = :f, Modified = NOW() WHERE Id = :id');
    $upd->execute([':f' => $flag, ':id' => $accountId]);

    if ($flag >= 2) {
        mq_enqueue_studio_command([
            'Type' => 'account_kick',
            'AccountId' => $accountId,
            'PlayerId' => max(0, $charId),
            'Message' => 'تم إيقاف/حظر هذا الحساب بواسطة إدارة MoonQueen.',
        ]);
    }

    $labels = [1 => 'مفعّل وسليم', 2 => 'مقفول مؤقتاً', 3 => 'محظور', 4 => 'محظور نهائياً'];
    return [
        'ok' => true,
        'message' => sprintf('تم تحديث حالة حساب [%s] إلى (%s)%s!', $row['UserName'], $labels[$flag] ?? '', $flag >= 2 ? ' وإرسال أمر طرد فوري إن كان متصلاً' : ''),
    ];
}

function mq_account_ban_network(string $ip, string $mac): array
{
    $ip = trim($ip);
    $mac = trim($mac);
    if ($ip === '127.0.0.1' || $ip === '::1') {
        $ip = ''; // Never mass-ban localhost loopback!
    }
    if ($mac === '000000000000' || strcasecmp($mac, 'Unknown') === 0) {
        $mac = '';
    }
    if ($ip === '' && $mac === '') {
        return ['ok' => false, 'error' => 'لا يمكن حظر عنوان محلي (127.0.0.1) أو MAC فارغ حفاظاً على جهاز المالك.'];
    }

    $conn = mq_open_pdo('account');
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error']];
    }
    $pdo = $conn['pdo'];

    $conds = [];
    $params = [];
    if ($ip !== '') {
        $conds[] = 'IpAddress = :ip';
        $params[':ip'] = $ip;
    }
    if ($mac !== '') {
        $conds[] = 'MacAddress = :mac';
        $params[':mac'] = $mac;
    }

    $sql = 'UPDATE conquer_account SET Flag = 4, Modified = NOW() WHERE AuthorityId < 8 AND (' . implode(' OR ', $conds) . ')';
    $st = $pdo->prepare($sql);
    $st->execute($params);
    $affected = $st->rowCount();

    mq_enqueue_studio_command([
        'Type' => 'account_kick',
        'IpAddress' => $ip,
        'MacAddress' => $mac,
        'Message' => 'تم حظر الشبكة/الجهاز بواسطة إدارة MoonQueen.',
    ]);

    return [
        'ok' => true,
        'message' => sprintf('تم حظر %d حساب مرتبط بالـ IP/MAC نهائياً وطرد الجلسات المتصلة فوراً!', $affected),
    ];
}

function mq_account_unstuck(int $charId, int $mapId = 1002, int $x = 430, int $y = 378): array
{
    if ($charId <= 0) {
        return ['ok' => false, 'error' => 'هذا الحساب ليس عليه شخصية بعد لفك تعليقها.'];
    }
    $conn = mq_open_pdo('game');
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error']];
    }
    $pdo = $conn['pdo'];

    $st = $pdo->prepare('SELECT name FROM cq_user WHERE id = :id LIMIT 1');
    $st->execute([':id' => $charId]);
    $ch = $st->fetch();
    if (!$ch) {
        return ['ok' => false, 'error' => 'الشخصية غير موجودة في قاعدة اللعبة.'];
    }

    $upd = $pdo->prepare('UPDATE cq_user SET recordmap_id = :m, recordx = :x, recordy = :y WHERE id = :id');
    $upd->execute([':m' => $mapId, ':x' => $x, ':y' => $y, ':id' => $charId]);

    $status = mq_read_server_status();
    $isOnline = false;
    if (!empty($status['live_connected']) && !empty($status['online_players_list'])) {
        foreach ($status['online_players_list'] as $on) {
            if ((int)($on['id'] ?? 0) === $charId) {
                $isOnline = true;
                break;
            }
        }
    }

    if ($isOnline) {
        mq_enqueue_studio_command([
            'Type' => 'player_action',
            'PlayerId' => $charId,
            'Action' => 'revive',
        ]);
        mq_enqueue_studio_command([
            'Type' => 'player_action',
            'PlayerId' => $charId,
            'Action' => 'teleport',
            'MapId' => $mapId,
            'X' => $x,
            'Y' => $y,
        ]);
    }

    return [
        'ok' => true,
        'message' => sprintf('تم فك تعليق الشخصية [%s] ونقل إحداثياتها إلى توين سيتي (%d: %d,%d)%s!', $ch['name'], $mapId, $x, $y, $isOnline ? ' ونقلها حياً داخل اللعبة' : ''),
    ];
}

function mq_account_reset_password(int $accountId, string $newPassword): array
{
    $newPassword = trim($newPassword);
    if ($accountId <= 0) {
        return ['ok' => false, 'error' => 'رقم الحساب غير صالح.'];
    }
    if (strlen($newPassword) < 4 || strlen($newPassword) > 16 || !preg_match('/^[A-Za-z0-9]+$/', $newPassword)) {
        return ['ok' => false, 'error' => 'كلمة المرور في كونكر 6192 يجب أن تكون من 4 إلى 16 حرفاً أو رقماً إنجليزياً فقط.'];
    }

    $conn = mq_open_pdo('account');
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error']];
    }
    $pdo = $conn['pdo'];

    $st = $pdo->prepare('SELECT UserName FROM conquer_account WHERE Id = :id LIMIT 1');
    $st->execute([':id' => $accountId]);
    $acc = $st->fetch();
    if (!$acc) {
        return ['ok' => false, 'error' => 'الحساب غير موجود.'];
    }

    $salt = mq_generate_account_salt();
    $hash = mq_hash_account_password($newPassword, $salt);

    $upd = $pdo->prepare('UPDATE conquer_account SET Password = :p, Salt = :s, Modified = NOW() WHERE Id = :id');
    $upd->execute([':p' => $hash, ':s' => $salt, ':id' => $accountId]);

    return [
        'ok' => true,
        'message' => sprintf('تم تحديث كلمة مرور الحساب [%s] بنجاح بتشفير SHA-256 + Salt جديد!', $acc['UserName']),
    ];
}

function mq_account_set_authority(int $accountId, int $authorityId, int $charId = 0): array
{
    if ($accountId <= 0 || !in_array($authorityId, [1, 2, 7, 8], true)) {
        return ['ok' => false, 'error' => 'بيانات الصلاحية غير صالحة.'];
    }

    $conn = mq_open_pdo('account');
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error']];
    }
    $pdo = $conn['pdo'];

    $st = $pdo->prepare('SELECT UserName FROM conquer_account WHERE Id = :id LIMIT 1');
    $st->execute([':id' => $accountId]);
    $acc = $st->fetch();
    if (!$acc) {
        return ['ok' => false, 'error' => 'الحساب غير موجود.'];
    }

    $upd = $pdo->prepare('UPDATE conquer_account SET AuthorityId = :a, Modified = NOW() WHERE Id = :id');
    $upd->execute([':a' => $authorityId, ':id' => $accountId]);

    mq_enqueue_studio_command([
        'Type' => 'account_sync_authority',
        'AccountId' => $accountId,
        'PlayerId' => max(0, $charId),
        'AuthorityId' => $authorityId,
    ]);

    $names = [1 => 'لاعب عادي (1)', 2 => 'Cooperator (2)', 7 => 'مشرف GM (7)', 8 => 'مدير كامل PM (8)'];
    return [
        'ok' => true,
        'message' => sprintf('تم تعديل رتبة حساب [%s] إلى [%s] ومزامنتها حياً!', $acc['UserName'], $names[$authorityId] ?? ''),
    ];
}

function mq_account_create(string $username, string $password, int $authorityId = 1): array
{
    $username = trim($username);
    $password = trim($password);
    if (!preg_match('/^[A-Za-z0-9_]{4,16}$/', $username)) {
        return ['ok' => false, 'error' => 'اسم المستخدم يجب أن يكون من 4 إلى 16 حرفاً/رقماً إنجليزياً.'];
    }
    if (!preg_match('/^[A-Za-z0-9]{4,16}$/', $password)) {
        return ['ok' => false, 'error' => 'كلمة المرور يجب أن تكون من 4 إلى 16 حرفاً أو رقماً إنجليزياً فقط.'];
    }
    if (!in_array($authorityId, [1, 2, 7, 8], true)) {
        $authorityId = 1;
    }

    $conn = mq_open_pdo('account');
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error']];
    }
    $pdo = $conn['pdo'];

    $chk = $pdo->prepare('SELECT Id FROM conquer_account WHERE UserName = :u LIMIT 1');
    $chk->execute([':u' => $username]);
    if ($chk->fetch()) {
        return ['ok' => false, 'error' => 'اسم المستخدم مسجل بالفعل، اختر اسماً آخر.'];
    }

    $salt = mq_generate_account_salt();
    $hash = mq_hash_account_password($password, $salt);

    $ins = $pdo->prepare(
        'INSERT INTO conquer_account (UserName, Password, Salt, AuthorityId, Flag, IpAddress, MacAddress, ParentId, Created)
         VALUES (:u, :p, :s, :a, 1, :ip, :mac, :pid, NOW())'
    );
    $ins->execute([
        ':u' => $username,
        ':p' => $hash,
        ':s' => $salt,
        ':a' => $authorityId,
        ':ip' => '127.0.0.1',
        ':mac' => '000000000000',
        ':pid' => mq_uuid_v4(),
    ]);

    return [
        'ok' => true,
        'message' => sprintf('تم إنشاء الحساب [%s] بنجاح وجاهز لتسجيل الدخول في اللعبة فوراً!', $username),
    ];
}

// ============================================================================
// TOOL #6: PROMO & GIFT CODES STUDIO
// ============================================================================

function mq_read_promos(): array
{
    $files = mq_resolve_studio_files();
    $data = mq_read_json_file($files['promos'], ['Codes' => []]);
    if (!isset($data['Codes']) || !is_array($data['Codes'])) {
        $data['Codes'] = [];
    }
    return $data;
}

function mq_save_promo(array $input): array
{
    $code = strtoupper(trim((string)($input['code'] ?? '')));
    if (!preg_match('/^[A-Z0-9_\-]{3,24}$/', $code)) {
        return ['ok' => false, 'error' => 'كود الهدية يجب أن يكون من 3 إلى 24 حرفاً إنجليزياً أو رقماً (بدون مسافات).'];
    }

    $env = mq_read_promos();
    $existingIdx = null;
    $existingRow = null;
    foreach ($env['Codes'] as $idx => $c) {
        if (strcasecmp((string)($c['Code'] ?? ''), $code) === 0) {
            $existingIdx = $idx;
            $existingRow = $c;
            break;
        }
    }

    $expiresHours = mq_clamp_int($input['expires_hours'] ?? 0, 0, 87600, 0);
    $expiresUnix = 0;
    $expiresAt = '';
    if ($expiresHours > 0) {
        $expiresUnix = time() + ($expiresHours * 3600);
        $expiresAt = date('Y-m-d H:i', $expiresUnix);
    } elseif ($existingRow && !empty($input['keep_expiry'])) {
        $expiresUnix = (int)($existingRow['ExpiresUnix'] ?? 0);
        $expiresAt = (string)($existingRow['ExpiresAt'] ?? '');
    }

    $vipVal = (isset($input['vip_level']) && $input['vip_level'] !== '' && (int)$input['vip_level'] >= 0)
        ? mq_clamp_int($input['vip_level'], 0, 6, -1)
        : -1;

    $record = [
        'Code' => $code,
        'Title' => mb_substr(trim((string)($input['title'] ?? 'هدية خاصة من MoonQueen')), 0, 60, 'UTF-8'),
        'Enabled' => isset($input['enabled']) ? !empty($input['enabled']) : true,
        'MaxUses' => mq_clamp_int($input['max_uses'] ?? 0, 0, 1000000, 0),
        'UsedCount' => $existingRow ? (int)($existingRow['UsedCount'] ?? 0) : 0,
        'MinLevel' => mq_clamp_int($input['min_level'] ?? 1, 1, 140, 1),
        'ExpiresUnix' => $expiresUnix,
        'ExpiresAt' => $expiresAt,
        'OnePerIpOrMac' => !empty($input['one_per_ip_mac']),
        'SilverAmount' => mq_clamp_int($input['silver_amount'] ?? 0, 0, 2000000000, 0),
        'CpAmount' => mq_clamp_int($input['cp_amount'] ?? 0, 0, 100000000, 0),
        'BoundCpAmount' => mq_clamp_int($input['bound_cp_amount'] ?? 0, 0, 100000000, 0),
        'BlessingHours' => mq_clamp_int($input['blessing_hours'] ?? 0, 0, 8760, 0),
        'VipLevel' => $vipVal,
        'ItemType' => mq_clamp_int($input['item_type'] ?? 0, 0, 2000000000, 0),
        'ItemAmount' => mq_clamp_int($input['item_amount'] ?? 1, 1, 20, 1),
        'Plus' => mq_clamp_int($input['plus'] ?? 0, 0, 12, 0),
        'Bless' => mq_clamp_int($input['bless'] ?? 0, 0, 7, 0),
        'Enchant' => mq_clamp_int($input['enchant'] ?? 0, 0, 255, 0),
        'SocketOne' => mq_clamp_int($input['socket_one'] ?? 0, 0, 255, 0),
        'SocketTwo' => mq_clamp_int($input['socket_two'] ?? 0, 0, 255, 0),
        'Bound' => !empty($input['bound']),
        'RedeemedAccounts' => $existingRow['RedeemedAccounts'] ?? [],
        'RedeemedPlayers' => $existingRow['RedeemedPlayers'] ?? [],
        'RedeemedIps' => $existingRow['RedeemedIps'] ?? [],
        'RedeemedMacs' => $existingRow['RedeemedMacs'] ?? [],
        'RecentClaims' => $existingRow['RecentClaims'] ?? [],
    ];

    if ($record['SilverAmount'] <= 0 && $record['CpAmount'] <= 0 && $record['BoundCpAmount'] <= 0
        && $record['BlessingHours'] <= 0 && $record['VipLevel'] < 0 && $record['ItemType'] <= 0) {
        return ['ok' => false, 'error' => 'اختر جائزة واحدة على الأقل للكود (فضة أو CPs أو VIP أو بركة أو أداة).'];
    }

    if ($existingIdx !== null) {
        $env['Codes'][$existingIdx] = $record;
    } else {
        array_unshift($env['Codes'], $record);
    }

    $files = mq_resolve_studio_files();
    if (!mq_write_json_atomic($files['promos'], $env)) {
        return ['ok' => false, 'error' => 'تعذر حفظ ملف الأكواد MoonQueen.Studio.Promos.json.'];
    }

    return [
        'ok' => true,
        'message' => sprintf('تم تفعيل كود الهدية [%s] حياً — يمكن للاعبين كتابة /redeem %s في الشات الآن!', $code, $code),
        'promos' => $env,
    ];
}

function mq_toggle_or_delete_promo(string $code, string $op): array
{
    $code = strtoupper(trim($code));
    $env = mq_read_promos();
    $found = false;

    if ($op === 'delete') {
        $next = [];
        foreach ($env['Codes'] as $c) {
            if (strcasecmp((string)($c['Code'] ?? ''), $code) === 0) {
                $found = true;
                continue;
            }
            $next[] = $c;
        }
        $env['Codes'] = $next;
    } else {
        foreach ($env['Codes'] as &$c) {
            if (strcasecmp((string)($c['Code'] ?? ''), $code) === 0) {
                $c['Enabled'] = empty($c['Enabled']);
                $found = true;
                break;
            }
        }
        unset($c);
    }

    if (!$found) {
        return ['ok' => false, 'error' => 'الكود المطلوب غير موجود.'];
    }

    $files = mq_resolve_studio_files();
    mq_write_json_atomic($files['promos'], $env);

    return [
        'ok' => true,
        'message' => $op === 'delete' ? "تم حذف الكود [$code] نهائياً." : "تم تحديث حالة تفعيل الكود [$code] فوراً.",
        'promos' => $env,
    ];
}

// ============================================================================
// TOOL #7: LIVE INVENTORY & WAREHOUSE INSPECTOR
// ============================================================================

function mq_position_label(int $pos, int $ownerId = 0): string
{
    $slots = [
        0 => 'حقيبة الظهر (Inventory)',
        1 => 'الخوذة (Headwear)',
        2 => 'القلادة (Necklace)',
        3 => 'الدرع (Armor)',
        4 => 'اليد اليمنى (Right Hand)',
        5 => 'اليد اليسرى (Left Hand)',
        6 => 'الخاتم (Ring)',
        7 => 'التليزمان / القرعة (Talisman)',
        8 => 'الحذاء (Boots)',
        9 => 'الجارمنت (Garment)',
        10 => 'مروحة الهجوم (Fan)',
        11 => 'برج الدفاع (Tower)',
        12 => 'الحصان (Steed)',
        15 => 'إكسسوار يمين (R-Accessory)',
        16 => 'إكسسوار يسار (L-Accessory)',
        17 => 'درع الحصان (Steed Armor)',
        18 => 'سوط الحصان (Crop)',
        19 => 'الجناح (Wing)',
        21 => 'خوذة بديلة (Alt Head)',
        22 => 'قلادة بديلة (Alt Neck)',
        23 => 'درع بديل (Alt Armor)',
        24 => 'يمين بديل (Alt R-Hand)',
        25 => 'يسار بديل (Alt L-Hand)',
        26 => 'خاتم بديل (Alt Ring)',
        27 => 'قرعة بديلة (Alt Bottle)',
        28 => 'حذاء بديل (Alt Boots)',
        29 => 'جارمنت بديل (Alt Garment)',
        30 => 'مروحة بديلة (Alt Fan)',
        31 => 'برج بديل (Alt Tower)',
        32 => 'حصان بديل (Alt Steed)',
    ];
    if (isset($slots[$pos])) {
        return $slots[$pos];
    }
    $warehouses = [
        8 => 'مخزن توين سيتي (Twin City WH)',
        10012 => 'مخزن الفينيكس (Phoenix WH)',
        10028 => 'مخزن القردة (Ape WH)',
        10011 => 'مخزن الصحراء (Desert WH)',
        10027 => 'مخزن الطيور (Bird WH)',
        44 => 'مخزن الماركت (Market WH)',
        4101 => 'مخزن ستون سيتي (Stone WH)',
    ];
    if ($pos >= 200) {
        if (isset($warehouses[$ownerId])) {
            return $warehouses[$ownerId];
        }
        return 'مخزن / صندوق (#' . ($ownerId > 0 ? $ownerId : $pos) . ')';
    }
    return 'موضع #' . $pos;
}

function mq_gem_label(int $gem): string
{
    if ($gem <= 0) {
        return 'بدون سوكت';
    }
    if ($gem === 255) {
        return 'سوكت مفتوح فارغ';
    }
    $map = [
        1 => 'Normal Phoenix', 2 => 'Refined Phoenix', 3 => 'Super Phoenix',
        11 => 'Normal Dragon', 12 => 'Refined Dragon', 13 => 'Super Dragon',
        21 => 'Normal Fury', 22 => 'Refined Fury', 23 => 'Super Fury',
        31 => 'Normal Rainbow', 32 => 'Refined Rainbow', 33 => 'Super Rainbow',
        41 => 'Normal Kylin', 42 => 'Refined Kylin', 43 => 'Super Kylin',
        51 => 'Normal Violet', 52 => 'Refined Violet', 53 => 'Super Violet',
        61 => 'Normal Moon', 62 => 'Refined Moon', 63 => 'Super Moon',
        71 => 'Normal Tortoise', 72 => 'Refined Tortoise', 73 => 'Super Tortoise',
        101 => 'Normal Thunder', 102 => 'Refined Thunder', 103 => 'Super Thunder',
        121 => 'Normal Glory', 122 => 'Refined Glory', 123 => 'Super Glory',
    ];
    return $map[$gem] ?? ('Gem #' . $gem);
}

function mq_item_quality_tag(int $type): string
{
    if ($type >= 100000 && $type < 999999) {
        $q = $type % 10;
        if ($q === 9) return 'Super';
        if ($q === 8) return 'Elite';
        if ($q === 7) return 'Unique';
        if ($q === 6) return 'Refined';
    }
    return 'Normal';
}

function mq_inspect_character(string $query): array
{
    $q = trim($query);
    if ($q === '') {
        return ['ok' => false, 'error' => 'اكتب اسم الشخصية أو رقم الـ ID للبحث والفحص.'];
    }

    $conn = mq_open_pdo('game');
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error']];
    }
    $pdo = $conn['pdo'];

    if (ctype_digit($q)) {
        $st = $pdo->prepare('SELECT * FROM cq_user WHERE id = :id OR account_id = :aid LIMIT 1');
        $st->execute([':id' => (int)$q, ':aid' => (int)$q]);
    } else {
        $st = $pdo->prepare('SELECT * FROM cq_user WHERE name = :exact LIMIT 1');
        $st->execute([':exact' => $q]);
        $ch = $st->fetch();
        if (!$ch) {
            $st2 = $pdo->prepare('SELECT * FROM cq_user WHERE name LIKE :lk ORDER BY level DESC LIMIT 1');
            $st2->execute([':lk' => '%' . $q . '%']);
            $ch = $st2->fetch();
        }
    }
    if (!isset($ch)) {
        $ch = $st->fetch();
    }

    if (!$ch) {
        return ['ok' => false, 'error' => 'لم يتم العثور على شخصية بهذا الاسم أو الرقم في قاعدة اللعبة.'];
    }

    $charId = (int)$ch['id'];
    $status = mq_read_server_status();
    $onlinePlayer = null;
    if (!empty($status['live_connected']) && !empty($status['online_players_list'])) {
        foreach ($status['online_players_list'] as $on) {
            if ((int)($on['id'] ?? 0) === $charId) {
                $onlinePlayer = $on;
                break;
            }
        }
    }

    // Request fresh live snapshot for next refresh if online
    if ($onlinePlayer !== null) {
        mq_enqueue_studio_command([
            'Type' => 'player_inspect',
            'PlayerId' => $charId,
        ]);
    }

    $files = mq_resolve_studio_files();
    $liveSnap = mq_read_json_file($files['inspect']);
    $useLiveSnap = false;
    if ($onlinePlayer !== null
        && !empty($liveSnap['player'])
        && (int)($liveSnap['player']['id'] ?? 0) === $charId
        && (time() - (int)($liveSnap['generated_unix'] ?? 0)) <= 20
        && is_array($liveSnap['items'] ?? null)) {
        $useLiveSnap = true;
    }

    $equipped = [];
    $inventory = [];
    $warehouses = [];

    if ($useLiveSnap) {
        foreach ($liveSnap['items'] as $it) {
            $type = (int)($it['type'] ?? 0);
            $pos = (int)($it['position'] ?? 0);
            $ownerId = (int)($it['owner_id'] ?? 0);
            $formatted = [
                'id' => (int)($it['id'] ?? 0),
                'type' => $type,
                'name' => (string)($it['name'] ?? ('Item #' . $type)),
                'quality' => mq_item_quality_tag($type),
                'position' => $pos,
                'slot_label' => mq_position_label($pos, $ownerId),
                'plus' => (int)($it['plus'] ?? 0),
                'bless' => (int)($it['bless'] ?? 0),
                'enchant' => (int)($it['enchant'] ?? 0),
                'gem1' => (int)($it['gem1'] ?? 0),
                'gem1_label' => mq_gem_label((int)($it['gem1'] ?? 0)),
                'gem2' => (int)($it['gem2'] ?? 0),
                'gem2_label' => mq_gem_label((int)($it['gem2'] ?? 0)),
                'stack' => max(1, (int)($it['stack'] ?? 1)),
                'bound' => !empty($it['bound']),
            ];
            if ($pos >= 1 && $pos <= 32) {
                $equipped[] = $formatted;
            } elseif ($pos === 0) {
                $inventory[] = $formatted;
            } else {
                $warehouses[] = $formatted;
            }
        }
    } else {
        $stItems = $pdo->prepare(
            'SELECT i.id, i.type, i.owner_id, i.position, i.magic3, i.reduce_dmg, i.add_life, i.gem1, i.gem2, i.monopoly, i.accumulate_num, t.name AS item_name
             FROM cq_item i
             LEFT JOIN cq_itemtype t ON t.id = i.type
             WHERE i.player_id = :pid
             ORDER BY i.position ASC, i.id ASC'
        );
        $stItems->execute([':pid' => $charId]);
        foreach ($stItems->fetchAll() as $it) {
            $type = (int)$it['type'];
            $pos = (int)$it['position'];
            $ownerId = (int)$it['owner_id'];
            $formatted = [
                'id' => (int)$it['id'],
                'type' => $type,
                'name' => (string)($it['item_name'] ?? ('Item #' . $type)),
                'quality' => mq_item_quality_tag($type),
                'position' => $pos,
                'slot_label' => mq_position_label($pos, $ownerId),
                'plus' => (int)$it['magic3'],
                'bless' => (int)$it['reduce_dmg'],
                'enchant' => (int)$it['add_life'],
                'gem1' => (int)$it['gem1'],
                'gem1_label' => mq_gem_label((int)$it['gem1']),
                'gem2' => (int)$it['gem2'],
                'gem2_label' => mq_gem_label((int)$it['gem2']),
                'stack' => max(1, (int)$it['accumulate_num']),
                'bound' => ((int)$it['monopoly'] !== 0),
            ];
            if ($pos >= 1 && $pos <= 32) {
                $equipped[] = $formatted;
            } elseif ($pos === 0) {
                $inventory[] = $formatted;
            } else {
                $warehouses[] = $formatted;
            }
        }
    }

    return [
        'ok' => true,
        'source' => $useLiveSnap ? 'ذاكرة السيرفر الحية (Online Live)' : 'قاعدة البيانات المباشرة',
        'online' => ($onlinePlayer !== null),
        'player' => [
            'id' => $charId,
            'account_id' => (int)($ch['account_id'] ?? 0),
            'name' => (string)$ch['name'],
            'level' => $onlinePlayer ? (int)$onlinePlayer['level'] : (int)$ch['level'],
            'reborn' => $onlinePlayer ? (int)$onlinePlayer['reborn'] : (int)$ch['metempsychosis'],
            'profession' => $onlinePlayer ? (int)$onlinePlayer['profession'] : (int)$ch['profession'],
            'bp' => $onlinePlayer ? (int)$onlinePlayer['bp'] : 0,
            'silvers' => $onlinePlayer ? (int)$onlinePlayer['silvers'] : (int)$ch['money'],
            'storage_money' => (int)($ch['money_saved'] ?? 0),
            'cps' => $onlinePlayer ? (int)$onlinePlayer['cps'] : (int)$ch['emoney'],
            'bound_cps' => $onlinePlayer ? (int)$onlinePlayer['bound_cps'] : (int)$ch['emoney_mono'],
            'pk' => $onlinePlayer ? (int)$onlinePlayer['pk'] : (int)$ch['pk'],
            'map_id' => $onlinePlayer ? (int)$onlinePlayer['map_id'] : (int)$ch['recordmap_id'],
            'x' => $onlinePlayer ? (int)$onlinePlayer['x'] : (int)$ch['recordx'],
            'y' => $onlinePlayer ? (int)$onlinePlayer['y'] : (int)$ch['recordy'],
        ],
        'equipped' => $equipped,
        'inventory' => $inventory,
        'warehouses' => $warehouses,
    ];
}

function mq_delete_character_item(int $playerId, int $itemId): array
{
    if ($playerId <= 0 || $itemId <= 0) {
        return ['ok' => false, 'error' => 'معرف اللاعب أو الأداة غير صالح.'];
    }

    $status = mq_read_server_status();
    $isOnline = false;
    if (!empty($status['live_connected']) && !empty($status['online_players_list'])) {
        foreach ($status['online_players_list'] as $on) {
            if ((int)($on['id'] ?? 0) === $playerId) {
                $isOnline = true;
                break;
            }
        }
    }

    if ($isOnline) {
        $q = mq_enqueue_studio_command([
            'Type' => 'player_delete_item',
            'PlayerId' => $playerId,
            'ItemId' => $itemId,
        ]);
        return [
            'ok' => $q['ok'],
            'message' => 'اللاعب متصل حالياً — تم إرسال أمر سحب وحذف الأداة حياً من ذاكرة وحقيبة اللاعب (< 1 ثانية)!',
        ];
    }

    $conn = mq_open_pdo('game');
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error']];
    }
    $st = $conn['pdo']->prepare('DELETE FROM cq_item WHERE id = :iid AND player_id = :pid');
    $st->execute([':iid' => $itemId, ':pid' => $playerId]);

    if ($st->rowCount() <= 0) {
        return ['ok' => false, 'error' => 'لم يتم العثور على هذه الأداة في سجل الشخصية.'];
    }

    return [
        'ok' => true,
        'message' => sprintf('تم حذف الأداة (#%d) من قاعدة بيانات اللاعب بنجاح.', $itemId),
    ];
}

// ============================================================================
// TOOL #8: AUTO-BROADCAST & SCHEDULE STUDIO
// ============================================================================

function mq_read_autobroadcast(): array
{
    $files = mq_resolve_studio_files();
    $data = mq_read_json_file($files['autobroadcast'], ['Enabled' => false, 'Items' => []]);
    return [
        'Enabled' => !empty($data['Enabled']),
        'Items' => is_array($data['Items'] ?? null) ? array_values($data['Items']) : [],
    ];
}

function mq_save_autobroadcast(array $payload): array
{
    $enabled = !empty($payload['enabled']);
    $rawItems = is_array($payload['items'] ?? null) ? $payload['items'] : [];
    $clean = [];

    foreach ($rawItems as $row) {
        if (!is_array($row)) {
            continue;
        }
        $msg = mb_substr(trim((string)($row['Message'] ?? $row['message'] ?? '')), 0, 200, 'UTF-8');
        if ($msg === '') {
            continue;
        }
        $mode = strtolower(trim((string)($row['Mode'] ?? $row['mode'] ?? 'interval')));
        if (!in_array($mode, ['interval', 'daily'], true)) {
            $mode = 'interval';
        }
        $daily = trim((string)($row['DailyTime'] ?? $row['daily_time'] ?? '20:55'));
        if (!preg_match('/^([01]\d|2[0-3]):[0-5]\d$/', $daily)) {
            $daily = '20:55';
        }
        $channel = trim((string)($row['Channel'] ?? $row['channel'] ?? 'TopLeft'));
        if (!in_array($channel, ['TopLeft', 'Center', 'Talk', 'Broadcast'], true)) {
            $channel = 'TopLeft';
        }
        $id = trim((string)($row['Id'] ?? $row['id'] ?? ''));
        if ($id === '' || !preg_match('/^[A-Za-z0-9_\-]{4,36}$/', $id)) {
            $id = 'ab_' . bin2hex(random_bytes(5));
        }

        $clean[] = [
            'Id' => $id,
            'Enabled' => isset($row['Enabled']) ? !empty($row['Enabled']) : (!isset($row['enabled']) || !empty($row['enabled'])),
            'Mode' => $mode,
            'IntervalMinutes' => mq_clamp_int($row['IntervalMinutes'] ?? $row['interval_minutes'] ?? 15, 1, 1440, 15),
            'DailyTime' => $daily,
            'Channel' => $channel,
            'Message' => $msg,
        ];
    }

    $envelope = [
        'Enabled' => $enabled,
        'Items' => $clean,
    ];

    $files = mq_resolve_studio_files();
    if (!mq_write_json_atomic($files['autobroadcast'], $envelope)) {
        return ['ok' => false, 'error' => 'تعذر حفظ جدول الإعلانات التلقائية.'];
    }

    return [
        'ok' => true,
        'message' => $enabled
            ? sprintf('تم حفظ وتفعيل مجدول الإعلانات التلقائية حياً (%d رسالة نشطة بدون ريستارت)!', count($clean))
            : 'تم حفظ القائمة مع إيقاف البث التلقائي مؤقتاً.',
        'autobroadcast' => $envelope,
    ];
}

// ============================================================================
// TOOL #9: BOSS & MONSTER CUSTOM DROP STUDIO (100% Pure Official 1.0x Default)
// ============================================================================

function mq_read_custom_drops(): array
{
    $files = mq_resolve_studio_files();
    $data = mq_read_json_file($files['customdrops'], ['Profiles' => []]);
    return [
        'Profiles' => is_array($data['Profiles'] ?? null) ? array_values($data['Profiles']) : [],
    ];
}

function mq_save_custom_drop_profile(array $payload): array
{
    $mobType = mq_clamp_int($payload['monster_type'] ?? 0, 0, 2000000000, 0);
    if ($mobType <= 0) {
        return ['ok' => false, 'error' => 'اختر الزعيم أو اكتب رقم MonsterType صحيحاً أولاً.'];
    }
    $mobName = mb_substr(trim((string)($payload['monster_name'] ?? ('Monster #' . $mobType))), 0, 48, 'UTF-8');
    $mode = strtolower(trim((string)($payload['mode'] ?? 'additional')));
    if (!in_array($mode, ['additional', 'override'], true)) {
        $mode = 'additional';
    }
    $enabled = isset($payload['enabled']) ? !empty($payload['enabled']) : true;

    $rawRules = is_array($payload['rules'] ?? null) ? $payload['rules'] : [];
    $cleanRules = [];
    foreach ($rawRules as $r) {
        if (!is_array($r)) {
            continue;
        }
        $itemType = mq_clamp_int($r['ItemType'] ?? $r['item_type'] ?? 0, 0, 2000000000, 0);
        $bonusSilver = mq_clamp_int($r['BonusSilver'] ?? $r['bonus_silver'] ?? 0, 0, 2000000000, 0);
        $bonusCps = mq_clamp_int($r['BonusCps'] ?? $r['bonus_cps'] ?? 0, 0, 10000000, 0);
        $bonusBoundCps = mq_clamp_int($r['BonusBoundCps'] ?? $r['bonus_bound_cps'] ?? 0, 0, 10000000, 0);

        if ($itemType <= 0 && $bonusSilver <= 0 && $bonusCps <= 0 && $bonusBoundCps <= 0) {
            continue;
        }

        $minC = mq_clamp_int($r['MinCount'] ?? $r['min_count'] ?? 1, 1, 15, 1);
        $maxC = mq_clamp_int($r['MaxCount'] ?? $r['max_count'] ?? $minC, $minC, 15, $minC);

        $cleanRules[] = [
            'Id' => 'dr_' . bin2hex(random_bytes(4)),
            'ItemType' => $itemType,
            'ItemName' => mb_substr(trim((string)($r['ItemName'] ?? $r['item_name'] ?? ($itemType > 0 ? ('Item #' . $itemType) : 'مكافأة عملات'))), 0, 48, 'UTF-8'),
            'ChancePercent' => mq_clamp_float($r['ChancePercent'] ?? $r['chance_percent'] ?? 10.0, 0.001, 100.0, 10.0),
            'MinCount' => $minC,
            'MaxCount' => $maxC,
            'Plus' => mq_clamp_int($r['Plus'] ?? $r['plus'] ?? 0, 0, 12, 0),
            'Bless' => mq_clamp_int($r['Bless'] ?? $r['bless'] ?? 0, 0, 7, 0),
            'Sockets' => mq_clamp_int($r['Sockets'] ?? $r['sockets'] ?? 0, 0, 2, 0),
            'Bound' => !empty($r['Bound'] ?? $r['bound'] ?? false),
            'BonusSilver' => $bonusSilver,
            'BonusCps' => $bonusCps,
            'BonusBoundCps' => $bonusBoundCps,
            'WorldAnnounce' => !empty($r['WorldAnnounce'] ?? $r['world_announce'] ?? false),
        ];
    }

    if (empty($cleanRules)) {
        return ['ok' => false, 'error' => 'أضف قاعدة دروب واحدة على الأقل (أداة أو عملات) قبل الحفظ.'];
    }

    $env = mq_read_custom_drops();
    $updated = false;
    foreach ($env['Profiles'] as $i => $p) {
        if ((int)($p['MonsterType'] ?? 0) === $mobType) {
            $env['Profiles'][$i] = [
                'MonsterType' => $mobType,
                'MonsterName' => $mobName,
                'Enabled' => $enabled,
                'Mode' => $mode,
                'Rules' => $cleanRules,
            ];
            $updated = true;
            break;
        }
    }

    if (!$updated) {
        $env['Profiles'][] = [
            'MonsterType' => $mobType,
            'MonsterName' => $mobName,
            'Enabled' => $enabled,
            'Mode' => $mode,
            'Rules' => $cleanRules,
        ];
    }

    $files = mq_resolve_studio_files();
    if (!mq_write_json_atomic($files['customdrops'], $env)) {
        return ['ok' => false, 'error' => 'تعذر حفظ ملف الدروبات المخصصة.'];
    }

    return [
        'ok' => true,
        'message' => sprintf('تم تفعيل جدول الدروب المخصص للوحش [%s #%d] حياً بدون ريستارت!', $mobName, $mobType),
        'custom_drops' => $env,
    ];
}

function mq_delete_custom_drop_profile(int $mobType): array
{
    $env = mq_read_custom_drops();
    if ($mobType === 0) {
        $env['Profiles'] = [];
    } else {
        $env['Profiles'] = array_values(array_filter(
            $env['Profiles'],
            static fn($p) => (int)($p['MonsterType'] ?? 0) !== $mobType
        ));
    }

    $files = mq_resolve_studio_files();
    mq_write_json_atomic($files['customdrops'], $env);

    return [
        'ok' => true,
        'message' => $mobType === 0
            ? 'تمت استعادة الدروب الرسمي النقي 100% (Pure Official 1.0x) لجميع وحوش وزعماء السيرفر!'
            : sprintf('تم حذف الدروب المخصص للوحش #%d وإرجاعه للدروب الرسمي النقي 1.0x!', $mobType),
        'custom_drops' => $env,
    ];
}

function mq_direct_db_catalog_search(string $query): array
{
    $conn = mq_open_pdo('game');
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error'], 'items' => [], 'monsters' => []];
    }
    $pdo = $conn['pdo'];
    $q = trim($query);

    $items = [];
    $monsters = [];
    try {
        if ($q === '') {
            $stI = $pdo->query('SELECT id, name, req_level, price, emoney_price, bound_emoney_price FROM cq_itemtype ORDER BY id ASC LIMIT 50');
            $stM = $pdo->query('SELECT id, name, level, life, attack_max FROM cq_monstertype ORDER BY life DESC LIMIT 50');
        } elseif (ctype_digit($q)) {
            $stI = $pdo->prepare('SELECT id, name, req_level, price, emoney_price, bound_emoney_price FROM cq_itemtype WHERE id = :id OR name LIKE :lk ORDER BY id ASC LIMIT 60');
            $stI->execute([':id' => (int)$q, ':lk' => '%' . $q . '%']);
            $stM = $pdo->prepare('SELECT id, name, level, life, attack_max FROM cq_monstertype WHERE id = :id OR name LIKE :lk ORDER BY life DESC LIMIT 60');
            $stM->execute([':id' => (int)$q, ':lk' => '%' . $q . '%']);
        } else {
            $stI = $pdo->prepare('SELECT id, name, req_level, price, emoney_price, bound_emoney_price FROM cq_itemtype WHERE name LIKE :lk ORDER BY id ASC LIMIT 60');
            $stI->execute([':lk' => '%' . $q . '%']);
            $stM = $pdo->prepare('SELECT id, name, level, life, attack_max FROM cq_monstertype WHERE name LIKE :lk ORDER BY life DESC LIMIT 60');
            $stM->execute([':lk' => '%' . $q . '%']);
        }

        foreach ($stI->fetchAll() as $r) {
            $items[] = [
                'id' => (int)$r['id'],
                'name' => (string)$r['name'],
                'req_level' => (int)$r['req_level'],
                'silver' => (int)$r['price'],
                'cps' => (int)$r['emoney_price'],
                'bound_cps' => (int)$r['bound_emoney_price'],
            ];
        }
        foreach ($stM->fetchAll() as $m) {
            $monsters[] = [
                'id' => (int)$m['id'],
                'name' => (string)$m['name'],
                'level' => (int)$m['level'],
                'life' => (int)$m['life'],
                'attack_max' => (int)$m['attack_max'],
            ];
        }
    } catch (Throwable $e) {
        return ['ok' => false, 'error' => 'خطأ أثناء البحث في جداول اللعبة.', 'items' => [], 'monsters' => []];
    }

    return ['ok' => true, 'items' => $items, 'monsters' => $monsters];
}

// ============================================================================
// TOOL #10: 1-CLICK DATABASE BACKUP & ECONOMY RADAR
// ============================================================================

function mq_economy_radar(): array
{
    $accConn = mq_open_pdo('account');
    $gameConn = mq_open_pdo('game');

    if (!$gameConn['ok']) {
        return ['ok' => false, 'db_ready' => false, 'error' => $gameConn['error']];
    }

    $pdoGame = $gameConn['pdo'];
    $pdoAcc = $accConn['ok'] ? $accConn['pdo'] : null;

    $totalAccounts = 0;
    $bannedAccounts = 0;
    if ($pdoAcc !== null) {
        try {
            $rowA = $pdoAcc->query('SELECT COUNT(*) AS c, SUM(CASE WHEN Flag IN (2,3,4) THEN 1 ELSE 0 END) AS b FROM conquer_account')->fetch();
            $totalAccounts = (int)($rowA['c'] ?? 0);
            $bannedAccounts = (int)($rowA['b'] ?? 0);
        } catch (Throwable $e) {
        }
    }

    $uRow = $pdoGame->query(
        'SELECT COUNT(*) AS total_chars,
                COALESCE(SUM(money), 0) AS bag_silver,
                COALESCE(SUM(money_saved), 0) AS wh_silver,
                COALESCE(SUM(emoney), 0) AS total_cps,
                COALESCE(SUM(emoney_mono), 0) AS total_bound_cps
         FROM cq_user'
    )->fetch();

    $iRow = $pdoGame->query(
        'SELECT
            COALESCE(SUM(CASE WHEN type = 1088000 THEN GREATEST(1, accumulate_num) WHEN type = 720028 THEN GREATEST(1, accumulate_num) * 10 ELSE 0 END), 0) AS dragonballs_equiv,
            COALESCE(SUM(CASE WHEN type = 1088001 THEN GREATEST(1, accumulate_num) WHEN type = 720027 THEN GREATEST(1, accumulate_num) * 10 ELSE 0 END), 0) AS meteors_equiv,
            COALESCE(SUM(CASE WHEN magic3 >= 12 THEN 1 ELSE 0 END), 0) AS plus12_items,
            COALESCE(SUM(CASE WHEN gem1 > 0 AND gem2 > 0 THEN 1 ELSE 0 END), 0) AS two_socket_items,
            COUNT(*) AS total_items
         FROM cq_item'
    )->fetch();

    $richest = $pdoGame->query(
        'SELECT id, name, level, metempsychosis, emoney, emoney_mono, (money + money_saved) AS total_silver
         FROM cq_user
         ORDER BY emoney DESC, total_silver DESC
         LIMIT 15'
    )->fetchAll();

    $status = mq_read_server_status();

    return [
        'ok' => true,
        'db_ready' => true,
        'metrics' => [
            'total_accounts' => $totalAccounts,
            'banned_accounts' => $bannedAccounts,
            'total_chars' => (int)($uRow['total_chars'] ?? 0),
            'online_now' => (int)($status['online_players'] ?? 0),
            'total_silver' => (int)($uRow['bag_silver'] ?? 0) + (int)($uRow['wh_silver'] ?? 0),
            'bag_silver' => (int)($uRow['bag_silver'] ?? 0),
            'wh_silver' => (int)($uRow['wh_silver'] ?? 0),
            'total_cps' => (int)($uRow['total_cps'] ?? 0),
            'total_bound_cps' => (int)($uRow['total_bound_cps'] ?? 0),
            'dragonballs' => (int)($iRow['dragonballs_equiv'] ?? 0),
            'meteors' => (int)($iRow['meteors_equiv'] ?? 0),
            'plus12_items' => (int)($iRow['plus12_items'] ?? 0),
            'two_socket_items' => (int)($iRow['two_socket_items'] ?? 0),
            'total_items' => (int)($iRow['total_items'] ?? 0),
        ],
        'richest' => $richest,
        'backups' => mq_list_backups(),
    ];
}

function mq_find_mysqldump(): ?string
{
    $candidates = [
        'C:\\xampp\\mysql\\bin\\mysqldump.exe',
        'D:\\xampp\\mysql\\bin\\mysqldump.exe',
        'E:\\xampp\\mysql\\bin\\mysqldump.exe',
        'F:\\xampp\\mysql\\bin\\mysqldump.exe',
        '/usr/bin/mysqldump',
        '/usr/local/bin/mysqldump',
    ];
    foreach ($candidates as $c) {
        if (is_file($c)) {
            return $c;
        }
    }
    return null;
}

function mq_dump_schema_via_pdo(PDO $pdo, string $schemaName): string
{
    $lines = [];
    $lines[] = "-- MoonQueen Backup ($schemaName) — Generated " . date('Y-m-d H:i:s');
    $lines[] = "SET NAMES utf8mb4;";
    $lines[] = "SET FOREIGN_KEY_CHECKS = 0;";
    $lines[] = "CREATE DATABASE IF NOT EXISTS `{$schemaName}` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;";
    $lines[] = "USE `{$schemaName}`;";
    $lines[] = "";

    $tables = $pdo->query('SHOW TABLES')->fetchAll(PDO::FETCH_COLUMN);
    foreach ($tables as $tbl) {
        $safeTbl = str_replace('`', '``', (string)$tbl);
        $createRow = $pdo->query("SHOW CREATE TABLE `{$safeTbl}`")->fetch(PDO::FETCH_NUM);
        if (!$createRow || !isset($createRow[1])) {
            continue;
        }
        $lines[] = "DROP TABLE IF EXISTS `{$safeTbl}`;";
        $lines[] = $createRow[1] . ";";

        $stmt = $pdo->query("SELECT * FROM `{$safeTbl}`");
        $batch = [];
        while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
            $vals = [];
            foreach ($row as $v) {
                if ($v === null) {
                    $vals[] = 'NULL';
                } elseif (is_int($v) || is_float($v)) {
                    $vals[] = (string)$v;
                } else {
                    $vals[] = $pdo->quote((string)$v);
                }
            }
            $batch[] = '(' . implode(',', $vals) . ')';
            if (count($batch) >= 250) {
                $lines[] = "INSERT INTO `{$safeTbl}` VALUES " . implode(',', $batch) . ";";
                $batch = [];
            }
        }
        if (!empty($batch)) {
            $lines[] = "INSERT INTO `{$safeTbl}` VALUES " . implode(',', $batch) . ";";
        }
        $lines[] = "";
    }
    $lines[] = "SET FOREIGN_KEY_CHECKS = 1;";
    return implode("\n", $lines) . "\n";
}

function mq_dump_single_schema(array $dbCfg, string $target): array
{
    $schema = ($target === 'account') ? $dbCfg['account_schema'] : $dbCfg['game_schema'];
    $mysqldump = mq_find_mysqldump();

    if ($mysqldump !== null && function_exists('proc_open')) {
        $cmd = [
            $mysqldump,
            '--host=' . $dbCfg['host'],
            '--port=' . (string)$dbCfg['port'],
            '--user=' . $dbCfg['user'],
            '--default-character-set=utf8mb4',
            '--single-transaction',
            '--routines',
            '--Quick',
            '--databases',
            $schema,
        ];
        // Normalize --quick lowercase for MariaDB mysqldump
        $cmd[7] = '--quick';

        $env = $_ENV;
        foreach ($_SERVER as $k => $v) {
            if (is_string($v) && !isset($env[$k])) {
                $env[$k] = $v;
            }
        }
        if ($dbCfg['pass'] !== '') {
            $env['MYSQL_PWD'] = $dbCfg['pass'];
        }

        $descriptors = [
            0 => ['pipe', 'r'],
            1 => ['pipe', 'w'],
            2 => ['pipe', 'w'],
        ];
        $proc = @proc_open($cmd, $descriptors, $pipes, null, $env);
        if (is_resource($proc)) {
            fclose($pipes[0]);
            $sqlOut = stream_get_contents($pipes[1]);
            $errOut = stream_get_contents($pipes[2]);
            fclose($pipes[1]);
            fclose($pipes[2]);
            $code = proc_close($proc);
            if ($code === 0 && is_string($sqlOut) && strlen($sqlOut) > 64) {
                return ['ok' => true, 'sql' => $sqlOut];
            }
        }
    }

    $conn = mq_open_pdo($target);
    if (!$conn['ok']) {
        return ['ok' => false, 'error' => $conn['error']];
    }
    try {
        $sqlOut = mq_dump_schema_via_pdo($conn['pdo'], $schema);
        return ['ok' => true, 'sql' => $sqlOut];
    } catch (Throwable $e) {
        return ['ok' => false, 'error' => 'تعذر تصدير قاعدة ' . $schema];
    }
}

function mq_create_backup(string $scope = 'both'): array
{
    if (!in_array($scope, ['both', 'account', 'game'], true)) {
        $scope = 'both';
    }
    $dbCfg = mq_resolve_db_settings();
    $targets = ($scope === 'both') ? ['account', 'game'] : [$scope];

    $combined = '';
    foreach ($targets as $t) {
        $res = mq_dump_single_schema($dbCfg, $t);
        if (!$res['ok']) {
            return $res;
        }
        $combined .= $res['sql'] . "\n";
    }

    $stamp = date('Ymd_His');
    $useGz = function_exists('gzencode');
    $fname = sprintf('moonqueen_backup_%s_%s.%s', $scope, $stamp, $useGz ? 'sql.gz' : 'sql');
    $fullPath = mq_backups_dir() . DIRECTORY_SEPARATOR . $fname;

    $payload = $useGz ? gzencode($combined, 6) : $combined;
    if ($payload === false || @file_put_contents($fullPath, $payload, LOCK_EX) === false) {
        return ['ok' => false, 'error' => 'تعذر حفظ ملف النسخة الاحتياطية داخل مجلد backups.'];
    }

    return [
        'ok' => true,
        'message' => sprintf('تم إنشاء النسخة الاحتياطية المضغوطة [%s] بنجاح!', $fname),
        'file' => $fname,
        'backups' => mq_list_backups(),
    ];
}

function mq_is_valid_backup_filename(string $fname): bool
{
    return (bool)preg_match('/^moonqueen_backup_(both|account|game)_\d{8}_\d{6}\.(sql|sql\.gz)$/', $fname);
}

function mq_list_backups(): array
{
    $dir = mq_backups_dir();
    $files = @scandir($dir);
    if (!is_array($files)) {
        return [];
    }
    $out = [];
    foreach ($files as $f) {
        if (!mq_is_valid_backup_filename($f)) {
            continue;
        }
        $full = $dir . DIRECTORY_SEPARATOR . $f;
        if (!is_file($full)) {
            continue;
        }
        $size = (int)@filesize($full);
        $mtime = (int)@filemtime($full);
        $out[] = [
            'file' => $f,
            'size_bytes' => $size,
            'size_human' => $size >= 1048576 ? round($size / 1048576, 2) . ' MB' : round(max(1, $size) / 1024, 1) . ' KB',
            'created_at' => $mtime > 0 ? date('Y-m-d H:i:s', $mtime) : '—',
            'mtime' => $mtime,
        ];
    }
    usort($out, static fn($a, $b) => $b['mtime'] <=> $a['mtime']);
    return $out;
}

function mq_delete_backup_file(string $fname): array
{
    $fname = basename(trim($fname));
    if (!mq_is_valid_backup_filename($fname)) {
        return ['ok' => false, 'error' => 'اسم ملف النسخة الاحتياطية غير صالح.'];
    }
    $full = mq_backups_dir() . DIRECTORY_SEPARATOR . $fname;
    if (is_file($full)) {
        @unlink($full);
    }
    return [
        'ok' => true,
        'message' => sprintf('تم حذف النسخة الاحتياطية [%s].', $fname),
        'backups' => mq_list_backups(),
    ];
}
