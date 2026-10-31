<?php
declare(strict_types=1);

/**
 * MoonQueen Control Studio — Sample Configuration
 * Engineered by ahmedzahran (احمد زهران)
 *
 * ملاحظة: لا تكتب أي باسورد نصي هنا.
 * عند فتح اللوحة لأول مرة على جهازك (127.0.0.1)، ستطلب منك تعيين كلمة مرور المالك
 * وسيتم حفظها مشفرة تلقائياً بنظام Bcrypt داخل config.local.php (المستبعد من Git).
 */

return [
    // اتركه فارغاً للاكتشاف التلقائي لمجلد src/Bin/Debug/net7.0 أو Release/net7.0،
    // أو ضع المسار الكامل لملف MoonQueen.Rates.json بجوار MoonQueen.Game.exe:
    // مثال: 'F:\\AhmedZahran\\Red-Moon\\src\\Bin\\Debug\\net7.0\\MoonQueen.Rates.json'
    'rates_file_path' => '',

    // هاش كلمة مرور الأدمن (يُنشأ تلقائياً عند أول تشغيل عبر شاشة الإعداد السريع)
    'admin_password_hash' => '',

    // مهلة الجلسة بالثواني (افتراضياً 8 ساعات عمل مريحة للمالك على جهازه الشخصي)
    'session_Idle_seconds' => 28800,
];
