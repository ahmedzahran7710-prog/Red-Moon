<?php
declare(strict_types=1);

require_once __DIR__ . '/includes/studio_core.php';

$authError = '';
$configured = mq_is_configured();

if (isset($_GET['logout']) && $_SERVER['REQUEST_METHOD'] === 'POST') {
    if (mq_verify_csrf($_POST['csrf_token'] ?? '')) {
        session_unset();
        session_destroy();
        header('Location: ./index.php');
        exit;
    }
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['studio_auth_mode'])) {
    if (!mq_verify_csrf($_POST['csrf_token'] ?? '')) {
        $authError = 'انتهت صلاحية رمز الأمان، أعد المحاولة.';
    } elseif ($_POST['studio_auth_mode'] === 'setup' && !$configured) {
        $pass1 = (string)($_POST['new_password'] ?? '');
        $pass2 = (string)($_POST['confirm_password'] ?? '');
        if (strlen($pass1) < 6) {
            $authError = 'يرجى اختيار كلمة مرور لا تقل عن 6 أحرف لحماية لوحة التحكم.';
        } elseif (!hash_equals($pass1, $pass2)) {
            $authError = 'كلمتا المرور غير متطابقتين.';
        } else {
            $hash = password_hash($pass1, PASSWORD_BCRYPT);
            if (mq_save_local_config(['admin_password_hash' => $hash])) {
                session_regenerate_id(true);
                $_SESSION['mq_admin_ok'] = true;
                $_SESSION['mq_last_active'] = time();
                header('Location: ./index.php');
                exit;
            }
            $authError = 'تعذر حفظ ملف config.local.php — تأكد من صلاحية الكتابة في المجلد.';
        }
    } elseif ($_POST['studio_auth_mode'] === 'login' && $configured) {
        $fails = (int)($_SESSION['mq_login_fails'] ?? 0);
        $lockUntil = (int)($_SESSION['mq_lock_until'] ?? 0);
        if ($lockUntil > time()) {
            $wait = $lockUntil - time();
            $authError = "تم إيقاف المحاولات مؤقتاً لمدة {$wait} ثانية للحماية.";
        } else {
            $cfg = mq_load_config();
            $pass = (string)($_POST['password'] ?? '');
            if (password_verify($pass, (string)($cfg['admin_password_hash'] ?? ''))) {
                $_SESSION['mq_login_fails'] = 0;
                $_SESSION['mq_lock_until'] = 0;
                session_regenerate_id(true);
                $_SESSION['mq_admin_ok'] = true;
                $_SESSION['mq_last_active'] = time();
                header('Location: ./index.php');
                exit;
            }
            $fails++;
            $_SESSION['mq_login_fails'] = $fails;
            if ($fails >= 5) {
                $_SESSION['mq_lock_until'] = time() + 30;
                $_SESSION['mq_login_fails'] = 0;
            }
            $authError = 'كلمة المرور غير صحيحة.';
        }
    }
}

$csrf = mq_csrf_token();
$authed = mq_is_authenticated();
$rates = $authed ? mq_read_rates() : mq_default_rates();
$status = $authed ? mq_read_server_status() : [];
$localState = $authed ? mq_load_local_state() : [];
$customPathVal = (string)($localState['custom_rates_path'] ?? '');

function h(string $s): string
{
    return htmlspecialchars($s, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
}
?>
<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>MoonQueen Control Studio — لوحة التحكم الفورية بالسيرفر</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Cairo:wght@500;600;700;800;900&family=Cinzel:wght@700;800&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="./assets/css/studio.css">
</head>
<body>

<header class="studio-topbar">
    <div class="topbar-inner">
        <div class="brand-cluster">
            <div class="moon-emblem" aria-hidden="true">🌙</div>
            <div class="brand-titles">
                <h1>
                    <span class="brand-en"><?= h(MQ_STUDIO_BRAND) ?></span>
                    <span>استوديو التحكم الفوري بالسيرفر</span>
                </h1>
                <div class="dev-seal">
                    حماية وتطوير حصري: <strong><?= h(MQ_STUDIO_DEV_EN) ?> (<?= h(MQ_STUDIO_DEV_AR) ?>)</strong> — ريلود حي بدون ريستارت
                </div>
            </div>
        </div>

        <?php if ($authed): ?>
        <div class="topbar-actions">
            <span id="dirtyBadge" class="dirty-badge">● تعديلات جاهزة للتطبيق</span>

            <div id="syncPill" class="sync-pill <?= !empty($status['live_connected']) ? 'online' : 'standby' ?>">
                <span class="pulse-dot"></span>
                <span id="syncPillText">
                    <?= !empty($status['live_connected'])
                        ? 'متصل بالسيرفر حياً (< 1 ثانية)'
                        : 'وضع التجهيز المسبق (السيرفر متوقف حالياً)' ?>
                </span>
            </div>

            <button type="button" class="btn btn-primary js-save-rates">
                ⚡ حفظ وريلود فوري الآن
            </button>

            <form method="post" action="./index.php?logout=1" style="display:inline;">
                <input type="hidden" name="csrf_token" value="<?= h($csrf) ?>">
                <button type="submit" class="btn btn-ghost">خروج</button>
            </form>
        </div>
        <?php endif; ?>
    </div>
</header>

<?php if (!$authed): ?>
    <main class="auth-wrap">
        <section class="section-card">
            <?php if (!$configured): ?>
                <div class="section-head">
                    <div>
                        <h2>🔐 الإعداد الأول للمالك (مرة واحدة فقط)</h2>
                        <p>اختر كلمة مرور خاصة بك لقفل لوحة تحكم MoonQueen على جهازك (تُحفظ مشفرة بنظام Bcrypt ولا تُرفع أبداً إلى Git).</p>
                    </div>
                </div>
                <?php if ($authError !== ''): ?>
                    <div class="alert-box alert-error"><?= h($authError) ?></div>
                <?php endif; ?>
                <form method="post" action="./index.php">
                    <input type="hidden" name="csrf_token" value="<?= h($csrf) ?>">
                    <input type="hidden" name="studio_auth_mode" value="setup">
                    <div class="control-group">
                        <label class="control-label" for="new_password">كلمة مرور المالك الجديدة</label>
                        <input id="new_password" name="new_password" type="password" class="text-input" required minlength="6" placeholder="6 أحرف أو أكثر..." style="margin-top:8px;">
                    </div>
                    <div class="control-group">
                        <label class="control-label" for="confirm_password">تأكيد كلمة المرور</label>
                        <input id="confirm_password" name="confirm_password" type="password" class="text-input" required minlength="6" placeholder="أعد كتابة كلمة المرور..." style="margin-top:8px;">
                    </div>
                    <button type="submit" class="btn btn-primary" style="width:100%; margin-top:12px;">
                        🚀 تفعيل ودخول لوحة التحكم الآن
                    </button>
                </form>
            <?php else: ?>
                <div class="section-head">
                    <div>
                        <h2>👑 تسجيل دخول مالك السيرفر</h2>
                        <p>اللوحة محمية ومقصورة على الجهاز المحلي (127.0.0.1) فقط.</p>
                    </div>
                </div>
                <?php if ($authError !== ''): ?>
                    <div class="alert-box alert-error"><?= h($authError) ?></div>
                <?php endif; ?>
                <form method="post" action="./index.php">
                    <input type="hidden" name="csrf_token" value="<?= h($csrf) ?>">
                    <input type="hidden" name="studio_auth_mode" value="login">
                    <div class="control-group">
                        <label class="control-label" for="password">كلمة مرور لوحة التحكم</label>
                        <input id="password" name="password" type="password" class="text-input" required autofocus placeholder="أدخل كلمة المرور..." style="margin-top:8px;">
                    </div>
                    <button type="submit" class="btn btn-primary" style="width:100%; margin-top:12px;">
                        🔓 فتح استوديو التحكم
                    </button>
                </form>
            <?php endif; ?>
        </section>
    </main>
<?php else: ?>
    <main id="mqStudioApp" class="studio-container" data-csrf="<?= h($csrf) ?>">

        <!-- Telemetry Strip -->
        <section class="telemetry-grid">
            <div class="stat-tile">
                <span class="stat-label">اللاعبون المتصلون الآن</span>
                <span id="statOnlinePlayers" class="stat-value"><?= (int)($status['online_players'] ?? 0) ?></span>
                <span id="statMaxOnline" class="stat-sub">أعلى ذروة: <?= (int)($status['max_online'] ?? 0) ?> لاعب</span>
            </div>
            <div class="stat-tile">
                <span class="stat-label">القالب النشط حالياً</span>
                <span id="statActivePreset" class="stat-value" style="font-size:1.08rem;">
                    <?= h((string)($status['active_rates']['PresetName'] ?? $rates['PresetName'])) ?>
                </span>
                <span class="stat-sub">يتحدث تلقائياً بدون ريستارت</span>
            </div>
            <div class="stat-tile">
                <span class="stat-label">آخر ريلود استلمه السيرفر</span>
                <span id="statLastReload" class="stat-value" style="font-size:1.05rem; direction:ltr; text-align:right;">
                    <?= h((string)($status['last_reload_at'] ?? 'لم يتم التشغيل بعد')) ?>
                </span>
                <span class="stat-sub">فحص حي كل ثانية من BasicThread</span>
            </div>
            <div class="stat-tile">
                <span class="stat-label">ملف الربط الفوري (MoonQueen.Rates.json)</span>
                <span id="statTargetPath" class="stat-value" style="font-size:0.78rem; direction:ltr; text-align:right; word-break:break-all;">
                    <?= h((string)($status['rates_path'] ?? '')) ?>
                </span>
                <span class="stat-sub">كتابة ذرّية آمنة (Atomic Sync)</span>
            </div>
        </section>

        <!-- Studio Navigation Tabs -->
        <nav class="studio-tabs">
            <button type="button" class="tab-btn active" data-tab-target="paneLiveRates">
                ⚡ محرك الريتات والدروب والأوامر الفورية (نشط الآن)
            </button>
            <button type="button" class="tab-btn" data-tab-target="paneProposedSuite">
                🧰 كتالوج الأدوات الإدارية القادمة المقترحة (اختر للمرحلة القادمة)
            </button>
        </nav>

        <!-- TAB 1: LIVE RATES & INSTANT COMMANDS -->
        <div id="paneLiveRates" class="tab-pane active">

            <!-- Quick Presets -->
            <section class="section-card">
                <div class="section-head">
                    <div>
                        <h2>🎯 قوالب جاهزة بضغطة زر (One-Click Presets)</h2>
                        <p>اختر قالباً جاهزاً أو عدّل أي رقم يدوياً بالأسفل، ثم اضغط <strong>حفظ وريلود فوري الآن</strong> ليُطبّق في نفس اللحظة.</p>
                    </div>
                    <div style="min-width:250px;">
                        <input id="inputPresetName" type="text" class="text-input" value="<?= h((string)$rates['PresetName']) ?>" placeholder="اسم الوضع الحالي...">
                    </div>
                </div>

                <div class="presets-grid">
                    <button type="button" class="preset-card" data-preset="classic">
                        <div class="preset-title">🛡️ كونكر الأصلية النقية (Pure Official 1x)</div>
                        <div class="preset-desc">الأساس الرسمي النقي 100% لكونكر أونلاين (V6192) بدون أي تعديل خاص: 1x خبرة وسحر وبروف ودروب وسوكت ودراجون بول.</div>
                    </button>
                    <button type="button" class="preset-card" data-preset="weekend">
                        <div class="preset-title">🔥 إيفنت نهاية الأسبوع (Weekend 2x)</div>
                        <div class="preset-desc">خبرة ومهارات 2x، فضة 1.5x، دروب وجودة 1.25x، مع فرصة ميتيور خفيفة لتنشيط السيرفر.</div>
                    </button>
                    <button type="button" class="preset-card" data-preset="happyhour">
                        <div class="preset-title">⚔️ ساعة الذروة (Happy Hour 3x EXP)</div>
                        <div class="preset-desc">دفعة تلفيل قوية لمدة محددة: خبرة 3x، سلاح وسحر 2.5x، وفضة 2x لجميع الخرائط.</div>
                    </button>
                    <button type="button" class="preset-card" data-preset="dropfever">
                        <div class="preset-title">💎 مهرجان الدروب وصيد الـ CPs</div>
                        <div class="preset-desc">مضاعفة الدروب والجودة والـ DragonBall إلى 2x مع تفعيل 1.5% فرصة ربح 1–5 CPs من الوحوش.</div>
                    </button>
                </div>
            </section>

            <!-- 3-Column Sliders Grid -->
            <div class="rates-columns">

                <!-- Column 1: EXP & Skills -->
                <section class="section-card" style="margin-bottom:0;">
                    <div class="section-head">
                        <div>
                            <h2>⚡ الخبرة والمهارات (EXP &amp; Mastery)</h2>
                            <p>تؤثر فوراً في <code>Character</code> و <code>MagicData</code> مع أول ضربة.</p>
                        </div>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">مضاعف خبرة التلفيل (Experience Rate)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_ExperienceRate" type="range" min="0.1" max="20" step="0.1" value="<?= h((string)$rates['ExperienceRate']) ?>">
                            <input id="num_ExperienceRate" type="number" class="num-box" min="0.05" max="100" step="0.05" value="<?= h((string)$rates['ExperienceRate']) ?>">
                        </div>
                        <div class="control-hint">الافتراضي <code>1.0x</code> — يتحكم في سرعة رفع لفل الشخصية من الوحوش.</div>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">مضاعف خبرة المهارات السحرية (Magic EXP)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_MagicExpRate" type="range" min="0.1" max="20" step="0.1" value="<?= h((string)$rates['MagicExpRate']) ?>">
                            <input id="num_MagicExpRate" type="number" class="num-box" min="0.05" max="100" step="0.05" value="<?= h((string)$rates['MagicExpRate']) ?>">
                        </div>
                        <div class="control-hint">الافتراضي <code>1.0x</code> — يتحكم في سرعة تلفيل الـ Spells والمهارات القتالية.</div>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">مضاعف بروف السلاح (Weapon Skill EXP)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_WeaponSkillExpRate" type="range" min="0.1" max="20" step="0.1" value="<?= h((string)$rates['WeaponSkillExpRate']) ?>">
                            <input id="num_WeaponSkillExpRate" type="number" class="num-box" min="0.05" max="100" step="0.05" value="<?= h((string)$rates['WeaponSkillExpRate']) ?>">
                        </div>
                        <div class="control-hint">الافتراضي <code>1.0x</code> — يتحكم في سرعة رفع الـ Proficiency للأسلحة.</div>
                    </div>
                </section>

                <!-- Column 2: Silver, Equipment & Quality -->
                <section class="section-card" style="margin-bottom:0;">
                    <div class="section-head">
                        <div>
                            <h2>💰 الفضة والدروب والجودة</h2>
                            <p>متصلة مباشرة بمعادلات القتل في <code>Monster.cs</code>.</p>
                        </div>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">مضاعف كمية الفضة (Silver Amount Rate)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_SilverDropRate" type="range" min="0" max="15" step="0.1" value="<?= h((string)$rates['SilverDropRate']) ?>">
                            <input id="num_SilverDropRate" type="number" class="num-box" min="0" max="100" step="0.1" value="<?= h((string)$rates['SilverDropRate']) ?>">
                        </div>
                        <div class="control-hint">يضاعف كمية الذهب/الفضة الساقطة على الأرض من كل وحش.</div>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">نسبة سقوط الفضة % (Silver Drop Chance)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_SilverDropChancePercent" type="range" min="0" max="100" step="1" value="<?= (int)$rates['SilverDropChancePercent'] ?>">
                            <input id="num_SilverDropChancePercent" type="number" class="num-box" min="0" max="100" step="1" value="<?= (int)$rates['SilverDropChancePercent'] ?>">
                        </div>
                        <span id="calcSilver" class="live-calc-tag">الوحش العادي: 25% | الوحش الأخضر: 8%</span>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">مضاعف سقوط المعدات (Equipment Drop Rate)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_EquipmentDropRate" type="range" min="0" max="10" step="0.1" value="<?= h((string)$rates['EquipmentDropRate']) ?>">
                            <input id="num_EquipmentDropRate" type="number" class="num-box" min="0" max="25" step="0.1" value="<?= h((string)$rates['EquipmentDropRate']) ?>">
                        </div>
                        <div class="control-hint">يتحكم في كثافة سقوط القطع والأدوات من الوحوش (1x = Classic+).</div>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">مضاعف جودة القطع (Super / Elite / Unique)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_QualityItemRate" type="range" min="0" max="10" step="0.1" value="<?= h((string)$rates['QualityItemRate']) ?>">
                            <input id="num_QualityItemRate" type="number" class="num-box" min="0" max="50" step="0.1" value="<?= h((string)$rates['QualityItemRate']) ?>">
                        </div>
                        <span id="calcQuality" class="live-calc-tag">داخل لفة الجودة -> Super: 0.025% | Elite: 0.12% | Unique: 0.42%</span>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">مضاعف سقوط السوكت للأسلحة (Weapon Socket Rate)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_SocketDropRate" type="range" min="0" max="20" step="0.5" value="<?= h((string)($rates['SocketDropRate'] ?? 1.0)) ?>">
                            <input id="num_SocketDropRate" type="number" class="num-box" min="0" max="100" step="0.1" value="<?= h((string)($rates['SocketDropRate'] ?? 1.0)) ?>">
                        </div>
                        <span id="calcSocket" class="live-calc-tag">الأصلي النقي (1x) -> 1-Socket: 0.100% | 2-Socket: 0.005%</span>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">مضاعف سقوط +1 من الوحوش (+1 Composition Rate)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_PlusOneDropRate" type="range" min="0" max="10" step="0.5" value="<?= h((string)($rates['PlusOneDropRate'] ?? 1.0)) ?>">
                            <input id="num_PlusOneDropRate" type="number" class="num-box" min="0" max="100" step="0.1" value="<?= h((string)($rates['PlusOneDropRate'] ?? 1.0)) ?>">
                        </div>
                        <span id="calcPlusOne" class="live-calc-tag">الأصلي النقي (1x) -> نسبة +1 على الأرض: 1.00%</span>
                    </div>
                </section>

                <!-- Column 3: DragonBall, Meteor & Monster CPs -->
                <section class="section-card" style="margin-bottom:0;">
                    <div class="section-head">
                        <div>
                            <h2>🐉 النوادر وصيد الـ CPs</h2>
                            <p>تحكم دقيق في الكرات النادرة وإيفنتات الـ CPs من الوحوش.</p>
                        </div>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">مضاعف دراجون بول (DragonBall Drop Rate)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_DragonBallDropRate" type="range" min="0" max="50" step="0.5" value="<?= h((string)$rates['DragonBallDropRate']) ?>">
                            <input id="num_DragonBallDropRate" type="number" class="num-box" min="0" max="1000" step="0.1" value="<?= h((string)$rates['DragonBallDropRate']) ?>">
                        </div>
                        <span id="calcDb" class="live-calc-tag">الأصلي النقي (1x): 625 / 54,000,000 (~1 كل 86,400 وحش على الأرض)</span>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">ميتيور إضافي لكل 100,000 وحش (Meteor Bonus)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_MeteorDropPer100k" type="range" min="0" max="500" step="5" value="<?= (int)$rates['MeteorDropPer100k'] ?>">
                            <input id="num_MeteorDropPer100k" type="number" class="num-box" min="0" max="100000" step="1" value="<?= (int)$rates['MeteorDropPer100k'] ?>">
                        </div>
                        <span id="calcMeteor" class="live-calc-tag">مغلق افتراضياً (0%)</span>
                    </div>

                    <div class="control-group">
                        <div class="control-top">
                            <span class="control-label">نسبة سقوط CPs من الوحوش % (Monster CP Event)</span>
                        </div>
                        <div class="slider-row">
                            <input id="rng_MonsterCpChancePercent" type="range" min="0" max="15" step="0.1" value="<?= h((string)$rates['MonsterCpChancePercent']) ?>">
                            <input id="num_MonsterCpChancePercent" type="number" class="num-box" min="0" max="100" step="0.05" value="<?= h((string)$rates['MonsterCpChancePercent']) ?>">
                        </div>
                        <div class="inline-pair">
                            <div>
                                <span class="control-hint">أقل كمية CPs</span>
                                <input id="num_MonsterCpMin" type="number" class="num-box" style="width:100%; margin-top:4px;" min="1" max="100000" value="<?= (int)$rates['MonsterCpMin'] ?>">
                            </div>
                            <div>
                                <span class="control-hint">أعلى كمية CPs</span>
                                <input id="num_MonsterCpMax" type="number" class="num-box" style="width:100%; margin-top:4px;" min="1" max="100000" value="<?= (int)$rates['MonsterCpMax'] ?>">
                            </div>
                        </div>
                        <span id="calcMonsterCp" class="live-calc-tag">مغلق حالياً (Classic+ نقي)</span>
                    </div>
                </section>
            </div>

            <!-- Instant Broadcast & Live Reload Actions Dock -->
            <div class="actions-dock">
                <section class="section-card" style="margin-bottom:0;">
                    <div class="section-head">
                        <div>
                            <h2>📢 الإعلان العالمي الفوري داخل اللعبة (Live World Broadcast)</h2>
                            <p>أرسل رسالة ذهبية تظهر لجميع اللاعبين المتصلين الآن في أقل من ثانية.</p>
                        </div>
                    </div>
                    <div style="display:grid; grid-template-columns: 1fr 190px; gap:10px;">
                        <input id="broadcastMessage" type="text" class="text-input" maxlength="180" placeholder="مثال: تم تفعيل إيفنت الخبرة المضاعفة 2x لمدة ساعتين في MoonQueen!">
                        <select id="broadcastChannel" class="text-input">
                            <option value="TopLeft">أعلى الشاشة (TopLeft)</option>
                            <option value="Center">منتصف الشاشة (Center)</option>
                            <option value="Talk">الشات العام (Talk)</option>
                        </select>
                    </div>
                    <label class="checkbox-line">
                        <input id="autoAnnounceOnSave" type="checkbox" checked>
                        <span>إرسال نص الإعلان أعلاه تلقائياً للاعبين عند الضغط على <strong>حفظ وريلود فوري الآن</strong> (إذا كانت الخانة غير فارغة)</span>
                    </label>
                    <div style="margin-top:14px; display:flex; gap:10px; flex-wrap:wrap;">
                        <button id="btnBroadcastNow" type="button" class="btn btn-gold">
                            📣 إرسال الإعلان الآن فقط
                        </button>
                        <button type="button" class="btn btn-primary js-save-rates">
                            ⚡ حفظ الريتات + تطبيق الريلود فوراً
                        </button>
                    </div>
                </section>

                <section class="section-card" style="margin-bottom:0;">
                    <div class="section-head">
                        <div>
                            <h2>🔄 ريلود الـ Actions وملفات Lua</h2>
                            <p>عدّلت حوار NPC في قاعدة البيانات (<code>cq_action</code> / <code>cq_task</code>) أو سكربت <code>.lua</code>؟ طبّقه حياً بدون قفل السيرفر!</p>
                        </div>
                    </div>
                    <button id="btnReloadActions" type="button" class="btn btn-ghost" style="width:100%; padding:13px; border-color:var(--border-gold); color:var(--gold);">
                        🔄 تحديث cq_action + cq_task + سكربتات Lua فوراً
                    </button>

                    <div style="margin-top:16px; padding-top:14px; border-top:1px solid var(--border-subtle);">
                        <div class="control-hint" style="margin-bottom:6px;">
                            <strong>مسار ملف الريتات المخصص (اختياري):</strong> اتركه فارغاً للمزامنة التلقائية مع <code>src/Bin/Debug</code> و <code>Release</code>.
                        </div>
                        <div style="display:flex; gap:8px;">
                            <input id="customRatesPathInput" type="text" class="text-input" style="direction:ltr; font-size:0.82rem;" value="<?= h($customPathVal) ?>" placeholder="Auto-Detect (src/Bin/Debug & Release)">
                            <button id="btnSaveTargetPath" type="button" class="btn btn-ghost">ربط</button>
                        </div>
                    </div>
                </section>
            </div>
        </div>

        <!-- TAB 2: PROPOSED NEXT-GEN ADMIN TOOLS SUITE -->
        <div id="paneProposedSuite" class="tab-pane">
            <section class="section-card">
                <div class="section-head">
                    <div>
                        <h2>🧰 حزمة أدوات التحكم الفورية المقترحة لـ MoonQueen (المرحلة 4 وما بعدها)</h2>
                        <p>كل أداة من الأدوات التالية صُممت خصيصاً لمعمارية <code>CanyonV6192</code> بحيث تدعم <strong>التعديل والريلود اللحظي داخل السيرفر بدون ريستارت</strong>. اختر منها ما تحب نبدأ به بعد تجربة أداة الريتات!</p>
                    </div>
                </div>

                <div class="suite-grid">
                    <article class="suite-card">
                        <span class="suite-badge">الأداة المقترحة #1 — الأولوية القصوى</span>
                        <h3>🏟️ استوديو المسابقات والإيفنتات الحية (Live Events &amp; PK Studio)</h3>
                        <p>تشغيل أو إيقاف المسابقات بضغطة زر وتعديل جوائزها ومواعيدها حياً، مع فك قيد <code>#if DEBUG</code> الذي يخفي Arena و ElitePK في وضع Release!</p>
                        <ul class="suite-points">
                            <li>تشغيل فوري لـ Guild War و Elite PK و Class PK و Capture The Flag و Skill Team PK</li>
                            <li>تعديل جوائز الـ CPs والفضة لكل بطولة وتطبيقها في نفس اللحظة</li>
                            <li>عدّاد تنازلي حي وإعلان أوتوماتيكي للاعبين قبل بدء البطولة</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">الأداة المقترحة #2 — اقتصاد ومبيعات</span>
                        <h3>🛒 محرر المتاجر والـ Mall الفوري (Live Shop &amp; CP Mall Editor)</h3>
                        <p>واجهة مرئية شيك بالصور والأسماء لإضافة أو حذف أو تعديل أسعار السلع في <code>emoney_shop</code> ومتاجر الـ NPCs مع ريلود فوري للذاكرة.</p>
                        <ul class="suite-points">
                            <li>بحث ذكي بالاسم أو الـ Item ID مع معاينة خصائص القطعة قبل إضافتها</li>
                            <li>عمل خصومات مؤقتة (Flash Sales) وتحديثها بدون قفل السيرفر</li>
                            <li>فلتر أمان يمنع وضع أدوات كاسرة للتوازن بالخطأ</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">الأداة المقترحة #3 — إدارة اللاعبين</span>
                        <h3>👑 رادار الأبطال وصندوق الجوائز الفوري (Live Player &amp; Mail Radar)</h3>
                        <p>متابعة حية لكل اللاعبين المتصلين على الخريطة مع إرسال هدايا أو تعويضات تصل لصندوق البريد داخل اللعبة (<code>MailManager</code>) في نفس الثانية!</p>
                        <ul class="suite-points">
                            <li>إرسال حزمة ترحيب أو هدية جماعية لكل المتصلين الآن بضغطة واحدة</li>
                            <li>منح أو تعديل مستوى الـ VIP، نقل لاعب عالق (Unstuck)، أو كتم/حظر فوري</li>
                            <li>سجل عمليات إداري كامل (Audit Log) يحفظ كل عملية بالتاريخ والسبب</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">الأداة المقترحة #4 — زعماء وحماس</span>
                        <h3>🐉 صانع غزوات الوحوش والزعماء (Live Boss Spawner &amp; Invasion Maker)</h3>
                        <p>استدعِ زعماء مثل TeratoDragon أو SnowBanshee في أي خريطة وإحداثيات بضغطة زر مع رسالة تحذير عالمية ملحمية للاعبين!</p>
                        <ul class="suite-points">
                            <li>قوالب غزوات جاهزة (غزوة Twin City، غزوة Bird Island، زعيم الساعة)</li>
                            <li>تحديد جائزة القاتل الأخير وتفعيلها فوراً بدون تعديل جداول معقدة</li>
                            <li>تعديل كثافة وسرعة ريسبون الوحوش في خرائط التلفيل حياً</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">الأداة المقترحة #5 — حوارات ومهمات</span>
                        <h3>📜 المصمم المرئي للـ NPCs والأكواد الترويجية (NPC &amp; Promo Code Studio)</h3>
                        <p>بدل كتابة أرقام <code>cq_action</code> المعقدة يدوياً في HeidiSQL، صمّم حوار أي NPC أو أنشئ كود هدية (Promo Code) من شاشة رسومية سهلة!</p>
                        <ul class="suite-points">
                            <li>إنشاء أكواد هدايا بعدد استخدامات وتاريخ انتهاء محدد لكل حساب</li>
                            <li>توليد <code>cq_action</code> تلقائياً وعمل ريلود فوري ليعمل الـ NPC في نفس اللحظة</li>
                            <li>حماية مدمجة لحقوق MoonQueen و ahmedzahran على كل رسالة NPC</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">الأداة المقترحة #6 — رقابة وأمان</span>
                        <h3>🛡️ مرصد الاقتصاد وكاشف الثغرات (Economy Watchdog &amp; Backup One-Click)</h3>
                        <p>لوحة رسومية تراقب إجمالي الـ CPs والفضة والـ +12 داخل السيرفر لحظة بلحظة مع زر نسخ احتياطي فوري لقواعد البيانات!</p>
                        <ul class="suite-points">
                            <li>تنبيه فوري إذا زادت ثروة حساب بشكل غير طبيعي في وقت قصير</li>
                            <li>نسخ احتياطي بضغطة زر لقاعدتي <code>moonqueen_account</code> و <code>moonqueen_game</code></li>
                            <li>مؤشر صحة اتصال خدمات السيرفر الأربع (Account / Game / AI / GM)</li>
                        </ul>
                    </article>
                </div>
            </section>
        </div>

    </main>
<?php endif; ?>

<div id="studioToast" class="studio-toast" role="status" aria-live="polite"></div>

<footer class="studio-footer">
    <strong><?= h(MQ_STUDIO_BRAND) ?> Control Studio</strong> — جميع حقوق التطوير والملكية محفوظة ومؤمّنة حصرياً للمطور
    <strong><?= h(MQ_STUDIO_DEV_EN) ?> (<?= h(MQ_STUDIO_DEV_AR) ?>)</strong>
</footer>

<?php if ($authed): ?>
<script src="./assets/js/studio.js"></script>
<?php endif; ?>
</body>
</html>
