<?php
/**
 * قالب إعدادات الاتصال بقاعدة البيانات المحلية — سيرفر MoonQueen (المطور ahmedzahran)
 *
 * خطوة التشغيل على جهازك في XAMPP:
 * 1) انسخ هذا الملف في نفس الفولدر وسمِّ النسخة الجديدة: config.local.php
 * 2) اكتب باسورد الـ MySQL/MariaDB الحقيقي بتاعك جوّه config.local.php فقط.
 * (ملف config.local.php محجوب تلقائيًا في .gitignore عشان الباسورد يفضل سري على جهازك بس).
 */

if (!defined('MOONQUEEN_PORTAL')) {
    http_response_code(403);
    exit('Forbidden');
}

return [
    'db' => [
        'host'           => '127.0.0.1',
        'port'           => 3306,
        'username'       => 'root',
        'password'       => 'PUT_YOUR_DB_PASSWORD_HERE',
        'account_schema' => 'moonqueen_account',
        'game_schema'    => 'moonqueen_game',
        'charset'        => 'utf8mb4',
    ],

    // إعدادات فحص حالة السيرفر محليًا (لا تعرض بورتات داخلية للعامة)
    'server_check' => [
        'host'               => '127.0.0.1',
        'login_port'         => 9958,
        'game_port'          => 5816,
        'socket_timeout_sec' => 0.35,
        'cache_ttl_sec'      => 15,
    ],

    // حماية ضد السبام وتخمين الباسورد (لكل عنوان IP)
    'rate_limit' => [
        'max_attempts' => 8,
        'window_sec'   => 300,
    ],
];
