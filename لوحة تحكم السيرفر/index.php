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

        <!-- Live Server Execution Feed -->
        <section class="exec-feed-card">
            <span class="exec-feed-title">📡 سجل التنفيذ اللحظي من السيرفر:</span>
            <div id="execFeedList" class="exec-feed-list">
                <span class="control-hint" style="margin:0;">جاهز لاستقبال وتأكيد الأوامر الفورية (&lt; 1 ثانية)...</span>
            </div>
        </section>

        <!-- Studio Navigation Tabs (Ordered: Rates + Tools #1 -> #2 -> #3 -> #4 + Next Ideas) -->
        <nav class="studio-tabs">
            <button type="button" class="tab-btn active" data-tab-target="paneLiveRates">
                ⚡ الريتات والدروب
            </button>
            <button type="button" class="tab-btn" data-tab-target="paneEventsStudio">
                1️⃣ 🏆 المسابقات والحروب الحية
            </button>
            <button type="button" class="tab-btn" data-tab-target="paneShopsStudio">
                2️⃣ 🛒 المتاجر والـ Mall الفوري
            </button>
            <button type="button" class="tab-btn" data-tab-target="panePlayersStudio">
                3️⃣ 🎁 رادار اللاعبين والهدايا (<span id="tabOnlineCount"><?= (int)($status['online_players'] ?? 0) ?></span>)
            </button>
            <button type="button" class="tab-btn" data-tab-target="paneBossStudio">
                4️⃣ 👹 صانع غزوات الزعماء (<span id="tabBossCount">0</span>)
            </button>
            <button type="button" class="tab-btn" data-tab-target="paneProposedSuite">
                💡 أفكار أدوات إضافية مقترحة
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

        <!-- ================================================================
             TOOL #1: LIVE EVENTS & TOURNAMENTS STUDIO
             ================================================================ -->
        <div id="paneEventsStudio" class="tab-pane">
            <section class="section-card">
                <div class="section-head">
                    <div>
                        <h2>🏆 الأداة #1: استوديو المسابقات والحروب الحية (Live Events &amp; Wars Studio)</h2>
                        <p>تم فك قيود <code>#if DEBUG</code> بالكامل لتعمل Arena و Team Arena و Elite PK و LineSkillPK في وضع <code>Release</code> و <code>Debug</code>، مع تحكم يدوي فوري بتشغيل أو إنهاء أي حرب أو بطولة بضغطة زر!</p>
                    </div>
                    <div style="display:flex; gap:10px; align-items:center; flex-wrap:wrap;">
                        <label style="font-size:0.82rem; color:var(--text-muted); font-weight:700;">مدة الحرب اليدوية:</label>
                        <select id="eventDurationSelect" class="select-input" style="min-width:140px;">
                            <option value="15">15 دقيقة</option>
                            <option value="30">30 دقيقة</option>
                            <option value="60" selected>60 دقيقة (ساعة)</option>
                            <option value="120">120 دقيقة (ساعتان)</option>
                            <option value="180">180 دقيقة (3 ساعات)</option>
                        </select>
                        <label class="checkbox-line" style="margin:0;">
                            <input id="eventBroadcastToggle" type="checkbox" checked>
                            <span>إعلان ذهبي تلقائي وسط الشاشة</span>
                        </label>
                    </div>
                </div>

                <div id="eventsCardsGrid" class="events-grid">
                    <!-- Rendered dynamically from live server snapshot -->
                </div>
            </section>
        </div>

        <!-- ================================================================
             TOOL #2: LIVE SHOP & MALL HOT-EDITOR
             ================================================================ -->
        <div id="paneShopsStudio" class="tab-pane">
            <section class="section-card">
                <div class="section-head">
                    <div>
                        <h2>🛒 الأداة #2: محرر المتاجر والـ Mall والشوب الفوري (Live Shop &amp; Mall Hot-Editor)</h2>
                        <p>استعرض وعدّل محتويات الـ Shopping Mall ومتاجر الـ NPCs وأسعار السلع (فضة / CPs / Bound CPs / نقاط الشرف / نقاط الخيل) مباشرة في قاعدة البيانات والذاكرة الحية خلال أقل من ثانية بدون ريستارت!</p>
                    </div>
                    <div style="display:flex; gap:8px; flex-wrap:wrap;">
                        <button id="btnReloadShopsLive" type="button" class="btn btn-emerald">
                            🔄 ريلود فوري لكل الشوبات والأسعار (&lt;1s)
                        </button>
                        <button id="btnRefreshShopsSnap" type="button" class="btn btn-ghost">
                            📥 تحديث العرض
                        </button>
                    </div>
                </div>

                <div class="studio-split">
                    <!-- Sidebar: Shop NPCs & Remote Mall -->
                    <div class="section-card" style="margin-bottom:0; padding:15px;">
                        <h3 style="font-size:0.95rem; color:var(--gold); margin-bottom:8px;">🏪 قائمة المتاجر والـ Mall</h3>
                        <input id="shopFilterInput" type="text" class="text-input" placeholder="فلترة المتجر بالاسم أو الـ ID (مثل 2888)..." style="margin-bottom:10px; font-size:0.82rem;">
                        <div id="shopsSidebarList" class="shop-list-box">
                            <div class="control-hint">شغّل السيرفر أو اضغط «ريلود فوري لكل الشوبات» لعرض كل المتاجر المحمّلة...</div>
                        </div>
                    </div>

                    <!-- Main Editor & Current Shop Goods -->
                    <div>
                        <div class="section-card" style="margin-bottom:16px; padding:16px;">
                            <div class="section-head" style="margin-bottom:10px;">
                                <div>
                                    <h3 style="font-size:0.98rem; color:var(--gold);">➕ إضافة / تعديل سلعة وسعرها فوراً</h3>
                                    <p>اختَر سلعة سريعة أو ابحث في <code>cq_itemtype</code> بالاسم/الرقم، وسيحفظها السيرفر ويعمل Hot-Reload في نفس اللحظة.</p>
                                </div>
                            </div>

                            <!-- Quick Popular Items -->
                            <div class="chip-row" id="shopQuickItemsRow">
                                <button type="button" class="quick-chip" data-pick-item="1088000" data-pick-name="DragonBall">🔮 DragonBall (1088000)</button>
                                <button type="button" class="quick-chip" data-pick-item="1088001" data-pick-name="Meteor">☄️ Meteor (1088001)</button>
                                <button type="button" class="quick-chip" data-pick-item="720028" data-pick-name="DBScroll">📜 DBScroll (720028)</button>
                                <button type="button" class="quick-chip" data-pick-item="720027" data-pick-name="MeteorScroll">📜 MeteorScroll (720027)</button>
                                <button type="button" class="quick-chip" data-pick-item="730001" data-pick-name="+1Stone">💎 +1 Stone (730001)</button>
                                <button type="button" class="quick-chip" data-pick-item="730003" data-pick-name="+3Stone">💎 +3 Stone (730003)</button>
                                <button type="button" class="quick-chip" data-pick-item="730006" data-pick-name="+6Stone">💎 +6 Stone (730006)</button>
                                <button type="button" class="quick-chip" data-pick-item="723700" data-pick-name="ExpBall">⚡ ExpBall (723700)</button>
                                <button type="button" class="quick-chip" data-pick-item="1200005" data-pick-name="ToughDrill">🛠️ ToughDrill (1200005)</button>
                                <button type="button" class="quick-chip" data-pick-item="723694" data-pick-name="PermanentStone">🛡️ PermanentStone (723694)</button>
                            </div>

                            <!-- Live Catalog Search Bar -->
                            <div style="display:flex; gap:8px; margin-bottom:12px;">
                                <input id="shopCatalogQuery" type="text" class="text-input" placeholder="ابحث عن أي أداة في السيرفر بالاسم الإنجليزي أو الـ ID (مثلاً: Garment أو SuperDragonGem أو 700013)...">
                                <button id="btnShopCatalogSearch" type="button" class="btn btn-gold">🔍 بحث في الداتا</button>
                            </div>
                            <div id="shopSearchResultsBox" class="chip-row" style="display:none;"></div>

                            <div class="form-grid-3">
                                <div class="field-box">
                                    <label>رقم المتجر / الـ Mall (Owner NPC ID)</label>
                                    <input id="shopFormNpcId" type="number" class="text-input" value="2888" placeholder="مثلاً 2888 للـ CP Mall">
                                </div>
                                <div class="field-box">
                                    <label>رقم الأداة (ItemType ID)</label>
                                    <input id="shopFormItemType" type="number" class="text-input" value="1088000" placeholder="مثلاً 1088000">
                                </div>
                                <div class="field-box">
                                    <label>نوع العملة بالمتجر (MoneyType)</label>
                                    <select id="shopFormMoneyType" class="select-input">
                                        <option value="1" selected>1 — نقاط كونكر (CPs / Bound CPs)</option>
                                        <option value="0">0 — فضة (Silver) / شرف / سباق</option>
                                    </select>
                                </div>
                                <div class="field-box">
                                    <label>سعر الـ CPs (EmoneyPrice) — اتركه فارغاً لعدم التغيير</label>
                                    <input id="shopFormCpPrice" type="number" class="text-input" placeholder="مثلاً: 215">
                                </div>
                                <div class="field-box">
                                    <label>سعر الـ Bound CPs — اتركه فارغاً لعدم التغيير</label>
                                    <input id="shopFormBoundCpPrice" type="number" class="text-input" placeholder="مثلاً: 0">
                                </div>
                                <div class="field-box">
                                    <label>سعر الفضة (Silver Price) — اتركه فارغاً لعدم التغيير</label>
                                    <input id="shopFormSilverPrice" type="number" class="text-input" placeholder="مثلاً: 50000">
                                </div>
                                <div class="field-box">
                                    <label>نقاط أرينا الشرف (HonorPrice اختياري)</label>
                                    <input id="shopFormHonorPrice" type="number" class="text-input" placeholder="0">
                                </div>
                                <div class="field-box">
                                    <label>نقاط الخيل (RidingPrice اختياري)</label>
                                    <input id="shopFormRidingPrice" type="number" class="text-input" placeholder="0">
                                </div>
                                <div class="field-box" style="justify-content:flex-end;">
                                    <button id="btnShopUpsert" type="button" class="btn btn-primary" style="width:100%;">
                                        💾 حفظ في الشوب + ريلود فوري
                                    </button>
                                </div>
                            </div>

                            <div id="clientShopSnippetWrap" style="margin-top:12px;">
                                <div class="control-hint">
                                    <strong>💡 مساعد ملفات الكلينت التلقائي (<code>ini/Shop.dat</code> &amp; <code>ini/emoneyShopV2.ini</code>):</strong>
                                    تعديل الأسعار للسلع الموجودة يعمل فوراً 100% من السيرفر؛ أما عند إضافة سلعة جديدة كلياً لم تكن ظاهرة بصرياً داخل واجهة المتجر في الكلينت، انسخ هذا السطر الجاهز لملف الكلينت:
                                </div>
                                <div id="clientShopSnippetCode" class="code-snippet-box">[Shop2888] — اختر أو احفظ سلعة لتوليد سطر الكلينت الجاهز تلقائياً</div>
                            </div>
                        </div>

                        <!-- Current Selected Shop Items Table -->
                        <div class="section-card" style="margin-bottom:0; padding:16px;">
                            <div class="section-head" style="margin-bottom:10px;">
                                <h3 id="selectedShopTitle" style="font-size:0.96rem; color:#fff;">📦 سلع المتجر المحدد</h3>
                                <input id="shopItemsFilterInput" type="text" class="text-input" style="max-width:240px; font-size:0.80rem;" placeholder="فلترة سلع المتجر...">
                            </div>
                            <div class="data-table-wrap">
                                <table class="studio-table">
                                    <thead>
                                        <tr>
                                            <th>Item ID</th>
                                            <th>اسم السلعة</th>
                                            <th>العملة</th>
                                            <th>سعر CPs</th>
                                            <th>Bound CPs</th>
                                            <th>سعر الفضة</th>
                                            <th>Honor / Riding</th>
                                            <th>إجراءات سريعة</th>
                                        </tr>
                                    </thead>
                                    <tbody id="selectedShopGoodsBody">
                                        <tr><td colspan="8" style="text-align:center; color:var(--text-muted);">اختر متجراً من القائمة اليمنى لعرض وتعديل سلعه...</td></tr>
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>
            </section>
        </div>

        <!-- ================================================================
             TOOL #3: LIVE ONLINE PLAYERS RADAR & GIFT / ACTION BOX
             ================================================================ -->
        <div id="panePlayersStudio" class="tab-pane">
            <section class="section-card">
                <div class="section-head">
                    <div>
                        <h2>🎁 الأداة #3: رادار اللاعبين المتصلين وصندوق الهدايا والأوامر الفوري</h2>
                        <p>تابع كل بطل متصل لحظة بلحظة، اضغط على اسمه لتحديده، وأرسل له (أو لكل المتصلين في السيرفر بضغطة واحدة!) عملات أو VIP أو قطعة عتاد مخصصة حتى <code>+12 / -7 / 255 / 2-Soc</code> أو نفّذ أوامر النقل والإنعاش والسجن في أقل من ثانية!</p>
                    </div>
                </div>

                <div class="studio-split-equal">
                    <!-- Left/Right 1: Live Online Players Table -->
                    <div class="section-card" style="margin-bottom:0; padding:16px;">
                        <div class="section-head" style="margin-bottom:10px;">
                            <h3 style="font-size:0.98rem; color:var(--gold);">🛰️ الأبطال المتصلون الآن</h3>
                            <input id="playersSearchInput" type="text" class="text-input" style="max-width:220px; font-size:0.80rem;" placeholder="بحث بالاسم أو الخريطة...">
                        </div>

                        <div class="data-table-wrap">
                            <table class="studio-table">
                                <thead>
                                    <tr>
                                        <th>الاسم</th>
                                        <th>Lvl / Reb</th>
                                        <th>BP</th>
                                        <th>الخريطة</th>
                                        <th>CPs / الفضة</th>
                                        <th>VIP</th>
                                        <th>الحالة</th>
                                    </tr>
                                </thead>
                                <tbody id="onlinePlayersTableBody">
                                    <tr><td colspan="7" style="text-align:center; color:var(--text-muted);">لا يوجد لاعبون متصلون حالياً — بمجرد دخول أي لاعب سيظهر هنا حياً!</td></tr>
                                </tbody>
                            </table>
                        </div>

                        <!-- Quick Support & Moderation Actions -->
                        <div style="margin-top:14px; padding-top:14px; border-top:1px solid var(--border-subtle);">
                            <div style="font-size:0.84rem; font-weight:800; color:var(--gold); margin-bottom:8px;">
                                ⚡ أوامر دعم وإدارة فورية للاعب المحدد:
                            </div>
                            <div style="display:flex; flex-wrap:wrap; gap:8px; margin-bottom:10px;">
                                <button type="button" class="btn btn-emerald btn-sm js-player-act" data-act="heal_revive">✨ إنعاش وعلاج كامل</button>
                                <button type="button" class="btn btn-gold btn-sm js-player-act" data-act="clear_pk">🕊️ تصفير الـ PK</button>
                                <button type="button" class="btn btn-ghost btn-sm js-player-tp" data-map="1002" data-x="300" data-y="278">🏰 نقل TwinCity</button>
                                <button type="button" class="btn btn-ghost btn-sm js-player-tp" data-map="1036" data-x="211" data-y="196">🛍️ نقل Market</button>
                                <button type="button" class="btn btn-ghost btn-sm js-player-tp" data-map="1038" data-x="345" data-y="335">⚔️ نقل GuildWar</button>
                                <button type="button" class="btn btn-crimson btn-sm js-player-act" data-act="jail">⛓️ سجن البوتات (6002)</button>
                                <button type="button" class="btn btn-emerald btn-sm js-player-act" data-act="unjail">🔓 إفراج من السجن</button>
                                <button type="button" class="btn btn-crimson btn-sm js-player-act" data-act="kick">🚪 طرد آمن (Kick)</button>
                            </div>
                            <div class="form-grid-3">
                                <input id="tpCustomMap" type="number" class="text-input" placeholder="Map ID (مثلاً 1002)">
                                <input id="tpCustomX" type="number" class="text-input" placeholder="X (مثلاً 300)">
                                <div style="display:flex; gap:6px;">
                                    <input id="tpCustomY" type="number" class="text-input" placeholder="Y (مثلاً 278)">
                                    <button id="btnCustomTeleport" type="button" class="btn btn-ghost btn-sm">نقل</button>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Gift & Custom Item Builder -->
                    <div class="section-card" style="margin-bottom:0; padding:16px;">
                        <h3 style="font-size:0.98rem; color:var(--gold); margin-bottom:10px;">🎁 صندوق إرسال الهدايا والعتاد الفوري</h3>

                        <div class="form-grid-2" style="margin-bottom:10px;">
                            <div class="field-box">
                                <label>اسم اللاعب المستهدف (أو اختَر من الجدول)</label>
                                <input id="giftPlayerName" type="text" class="text-input" placeholder="اسم البطل داخل اللعبة...">
                                <input id="giftPlayerId" type="hidden" value="0">
                            </div>
                            <div class="field-box">
                                <label>تعديل مستوى الـ VIP (اختياري)</label>
                                <select id="giftVipLevel" class="select-input">
                                    <option value="-1" selected>بدون تغيير مستوى VIP</option>
                                    <option value="0">VIP 0 (إلغاء)</option>
                                    <option value="1">VIP 1</option>
                                    <option value="2">VIP 2</option>
                                    <option value="3">VIP 3</option>
                                    <option value="4">VIP 4</option>
                                    <option value="5">VIP 5</option>
                                    <option value="6">VIP 6 (أعلى مستوى ملكي)</option>
                                </select>
                            </div>
                        </div>

                        <div class="form-grid-2" style="margin-bottom:12px;">
                            <div class="field-box">
                                <label>منح نقاط كونكر (CPs)</label>
                                <input id="giftCpAmount" type="number" class="text-input" value="0" min="0">
                            </div>
                            <div class="field-box">
                                <label>منح نقاط مقيدة (Bound CPs)</label>
                                <input id="giftBoundCpAmount" type="number" class="text-input" value="0" min="0">
                            </div>
                            <div class="field-box">
                                <label>منح فضة (Silver)</label>
                                <input id="giftSilverAmount" type="number" class="text-input" value="0" min="0">
                            </div>
                            <div class="field-box">
                                <label>ساعات بركة السماء (Heaven Blessing Hours)</label>
                                <input id="giftBlessingHours" type="number" class="text-input" value="0" min="0">
                            </div>
                        </div>

                        <div style="padding-top:10px; border-top:1px solid var(--border-subtle); margin-bottom:10px;">
                            <div style="font-size:0.84rem; font-weight:800; color:#fff; margin-bottom:6px;">🗡️ إرفاق أداة أو قطعة عتاد مخصصة (اختياري):</div>
                            <div class="chip-row" id="giftQuickItemsRow">
                                <button type="button" class="quick-chip" data-gift-item="1088000">🔮 DragonBall</button>
                                <button type="button" class="quick-chip" data-gift-item="720028">📜 DBScroll</button>
                                <button type="button" class="quick-chip" data-gift-item="720027">📜 MeteorScroll</button>
                                <button type="button" class="quick-chip" data-gift-item="730008">💎 +8 Stone</button>
                                <button type="button" class="quick-chip" data-gift-item="723700">⚡ ExpBall</button>
                                <button type="button" class="quick-chip" data-gift-item="700013">🐉 SuperDragonGem</button>
                                <button type="button" class="quick-chip" data-gift-item="700073">🐢 SuperTortoiseGem</button>
                            </div>

                            <div class="form-grid-3">
                                <div class="field-box">
                                    <label>رقم الأداة (ItemType)</label>
                                    <input id="giftItemType" type="number" class="text-input" value="0" min="0" placeholder="0 = بدون أداة">
                                </div>
                                <div class="field-box">
                                    <label>الكمية (1 - 20)</label>
                                    <input id="giftItemAmount" type="number" class="text-input" value="1" min="1" max="20">
                                </div>
                                <div class="field-box">
                                    <label>البلس (+Plus 0..12)</label>
                                    <input id="giftItemPlus" type="number" class="text-input" value="0" min="0" max="12">
                                </div>
                                <div class="field-box">
                                    <label>الديمج / البلس (-Bless)</label>
                                    <select id="giftItemBless" class="select-input">
                                        <option value="0">0%</option>
                                        <option value="1">-1%</option>
                                        <option value="3">-3%</option>
                                        <option value="5">-5%</option>
                                        <option value="7">-7% (ماكس)</option>
                                    </select>
                                </div>
                                <div class="field-box">
                                    <label>الدم (Enchant HP 0..255)</label>
                                    <input id="giftItemEnchant" type="number" class="text-input" value="0" min="0" max="255">
                                </div>
                                <div class="field-box" style="justify-content:flex-end;">
                                    <label class="checkbox-line" style="margin:0;">
                                        <input id="giftItemBound" type="checkbox">
                                        <span>مقيدة (Bound)</span>
                                    </label>
                                </div>
                                <div class="field-box">
                                    <label>الجوهرة الأولى (Socket 1)</label>
                                    <select id="giftSocketOne" class="select-input">
                                        <option value="0">بدون سوكت 1</option>
                                        <option value="255">سوكت مفتوح فارغ</option>
                                        <option value="13">Super Dragon Gem (+15% ATK)</option>
                                        <option value="3">Super Phoenix Gem (+15% MATK)</option>
                                        <option value="73">Super Tortoise Gem (-6% DMG)</option>
                                        <option value="33">Super Rainbow Gem (+25% EXP)</option>
                                        <option value="53">Super Violet Gem</option>
                                        <option value="63">Super Moon Gem</option>
                                        <option value="103">Super Thunder Gem (+500 MATK)</option>
                                        <option value="123">Super Glory Gem (+500 Shield)</option>
                                    </select>
                                </div>
                                <div class="field-box">
                                    <label>الجوهرة الثانية (Socket 2)</label>
                                    <select id="giftSocketTwo" class="select-input">
                                        <option value="0">بدون سوكت 2</option>
                                        <option value="255">سوكت مفتوح فارغ</option>
                                        <option value="13">Super Dragon Gem (+15% ATK)</option>
                                        <option value="3">Super Phoenix Gem (+15% MATK)</option>
                                        <option value="73">Super Tortoise Gem (-6% DMG)</option>
                                        <option value="33">Super Rainbow Gem (+25% EXP)</option>
                                        <option value="53">Super Violet Gem</option>
                                        <option value="63">Super Moon Gem</option>
                                        <option value="103">Super Thunder Gem (+500 MATK)</option>
                                        <option value="123">Super Glory Gem (+500 Shield)</option>
                                    </select>
                                </div>
                                <div class="field-box">
                                    <label>رسالة تهنئة خاصة (اختياري)</label>
                                    <input id="giftMessage" type="text" class="text-input" placeholder="مبروك هدية خاصة من إدارة MoonQueen!">
                                </div>
                            </div>
                        </div>

                        <div style="display:flex; gap:10px; flex-wrap:wrap; margin-top:12px;">
                            <button id="btnSendGiftSingle" type="button" class="btn btn-primary" style="flex:1;">
                                🎁 إرسال الهدية للاعب المحدد الآن
                            </button>
                            <button id="btnSendGiftAllOnline" type="button" class="btn btn-gold" style="flex:1;">
                                🎉 إرسال الهدية لكل المتصلين الآن!
                            </button>
                        </div>
                    </div>
                </div>
            </section>
        </div>

        <!-- ================================================================
             TOOL #4: LIVE BOSS & INVASION SPAWNER STUDIO
             ================================================================ -->
        <div id="paneBossStudio" class="tab-pane">
            <section class="section-card">
                <div class="section-head">
                    <div>
                        <h2>👹 الأداة #4: صانع غزوات الزعماء والوحوش الفوري (Live Boss &amp; Invasion Spawner)</h2>
                        <p>استدعِ أي زعيم عملاق أو موجة وحوش عبر محرك الذكاء الاصطناعي الرسمي (<code>MoonQueen.Ai</code>) في أي مدينة أو خريطة بضغطة زر مع تحذير ذهبي عالمي ورادار حي لدم الزعماء ومسحهم متى شئت!</p>
                    </div>
                    <button id="btnClearAllBosses" type="button" class="btn btn-crimson">
                        🧹 مسح كل وحوش الغزوات المستدعاة الآن
                    </button>
                </div>

                <div class="studio-split-equal">
                    <!-- Spawner Controls -->
                    <div class="section-card" style="margin-bottom:0; padding:16px;">
                        <h3 style="font-size:0.98rem; color:var(--gold); margin-bottom:8px;">🐉 اختر الزعيم ونقطة الإنزال</h3>

                        <div style="font-size:0.80rem; color:var(--text-muted); margin-bottom:4px;">أشهر زعماء كونكر (أو ابحث بالأسفل في جدول <code>cq_monstertype</code>):</div>
                        <div class="chip-row" id="bossPresetChips">
                            <button type="button" class="quick-chip" data-boss-id="20070" data-boss-name="TeratoDragon">🐉 TeratoDragon (20070)</button>
                            <button type="button" class="quick-chip" data-boss-id="20060" data-boss-name="SnowBanshee">❄️ SnowBanshee (20060)</button>
                            <button type="button" class="quick-chip" data-boss-id="20160" data-boss-name="ThrillingSpook">👻 ThrillingSpook (20160)</button>
                            <button type="button" class="quick-chip" data-boss-id="20300" data-boss-name="NemesisTyrant">🔱 NemesisTyrant (20300)</button>
                            <button type="button" class="quick-chip" data-boss-id="4145" data-boss-name="SwordMaster">⚔️ SwordMaster (4145)</button>
                            <button type="button" class="quick-chip" data-boss-id="3820" data-boss-name="Raikou">⚡ Raikou (3820)</button>
                        </div>

                        <!-- Live Monster Catalog Search -->
                        <div style="display:flex; gap:8px; margin-bottom:12px;">
                            <input id="bossCatalogQuery" type="text" class="text-input" placeholder="ابحث عن أي زعيم أو وحش بالاسم الإنجليزي أو الـ ID (مثلاً: Dragon أو Banshee)...">
                            <button id="btnBossCatalogSearch" type="button" class="btn btn-gold">🔍 بحث وحوش</button>
                        </div>
                        <div id="bossSearchResultsBox" class="chip-row" style="display:none;"></div>

                        <div style="font-size:0.80rem; color:var(--text-muted); margin-bottom:4px;">نقاط إنزال سريعة (Hotspots):</div>
                        <div class="chip-row" id="bossMapChips">
                            <button type="button" class="quick-chip" data-map="1002" data-x="300" data-y="278">🏰 TwinCity Center (1002: 300,278)</button>
                            <button type="button" class="quick-chip" data-map="1002" data-x="428" data-y="378">🌉 TwinCity Gate (1002: 428,378)</button>
                            <button type="button" class="quick-chip" data-map="1015" data-x="717" data-y="571">🦅 BirdIsland (1015: 717,571)</button>
                            <button type="button" class="quick-chip" data-map="1000" data-x="500" data-y="650">🏜️ DesertCity (1000: 500,650)</button>
                            <button type="button" class="quick-chip" data-map="1020" data-x="565" data-y="562">🦍 ApeCity (1020: 565,562)</button>
                            <button type="button" class="quick-chip" data-map="1011" data-x="188" data-y="264">🌲 PhoenixCastle (1011: 188,264)</button>
                        </div>

                        <div class="form-grid-3" style="margin-top:8px;">
                            <div class="field-box">
                                <label>رقم الزعيم / الوحش (MonsterType)</label>
                                <input id="bossMonsterType" type="number" class="text-input" value="20070">
                            </div>
                            <div class="field-box">
                                <label>العدد في الموجة (1 - 25)</label>
                                <input id="bossSpawnCount" type="number" class="text-input" value="1" min="1" max="25">
                            </div>
                            <div class="field-box">
                                <label>نطاق الانتشار (Radius)</label>
                                <input id="bossScatterRadius" type="number" class="text-input" value="4" min="0" max="25">
                            </div>
                            <div class="field-box">
                                <label>رقم الخريطة (Map ID)</label>
                                <input id="bossMapId" type="number" class="text-input" value="1002">
                            </div>
                            <div class="field-box">
                                <label>الإحداثي X</label>
                                <input id="bossCoordX" type="number" class="text-input" value="300">
                            </div>
                            <div class="field-box">
                                <label>الإحداثي Y</label>
                                <input id="bossCoordY" type="number" class="text-input" value="278">
                            </div>
                        </div>

                        <div class="field-box" style="margin-top:10px;">
                            <label>رسالة التحذير العالمية للغزوة (اتركها فارغة للرسالة الحماسية التلقائية)</label>
                            <input id="bossAnnounceMsg" type="text" class="text-input" placeholder="مثال: الزعيم الأسطوري TeratoDragon نزل وسط TwinCity الآن! هجوم يا أبطال MoonQueen!">
                        </div>

                        <label class="checkbox-line">
                            <input id="bossBroadcastToggle" type="checkbox" checked>
                            <span>إرسال تحذير ذهبي وسط الشاشة لكل السيرفر لحظة نزول الزعيم</span>
                        </label>

                        <button id="btnSpawnBossNow" type="button" class="btn btn-primary" style="width:100%; margin-top:12px;">
                            🔥 إطلاق الغزوة / استدعاء الزعيم فوراً (&lt; 1 ثانية)
                        </button>
                    </div>

                    <!-- Active Spawned Bosses Live Radar -->
                    <div class="section-card" style="margin-bottom:0; padding:16px;">
                        <div class="section-head" style="margin-bottom:10px;">
                            <h3 style="font-size:0.98rem; color:var(--gold);">📡 رادار الزعماء المستدعين الأحياء الآن</h3>
                        </div>
                        <div class="data-table-wrap">
                            <table class="studio-table">
                                <thead>
                                    <tr>
                                        <th>الزعيم</th>
                                        <th>Lvl</th>
                                        <th>الدم الحالي (HP)</th>
                                        <th>الموقع</th>
                                        <th>مسح</th>
                                    </tr>
                                </thead>
                                <tbody id="spawnedBossesTableBody">
                                    <tr><td colspan="5" style="text-align:center; color:var(--text-muted);">لا يوجد زعماء غزوات مستدعون حالياً.</td></tr>
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            </section>
        </div>

        <!-- ================================================================
             NEXT-WAVE PROPOSED ADMIN TOOLS IDEAS (#5 to #10)
             ================================================================ -->
        <div id="paneProposedSuite" class="tab-pane">
            <section class="section-card">
                <div class="section-head">
                    <div>
                        <h2>💡 أفكار أدوات إدارية إضافية مقترحة لتطوير MoonQueen في المراحل القادمة</h2>
                        <p>بعد اكتمال وتشغيل الأدوات الخمس الأساسية حياً (الريتات + المسابقات + المتاجر + رادار اللاعبين والهدايا + غزوات الزعماء)، هذه 6 أفكار عملية جديدة مصممة خصيصاً لسورس <code>MoonQueen V6192</code>:</p>
                    </div>
                </div>

                <div class="suite-grid">
                    <article class="suite-card">
                        <span class="suite-badge">فكرة مقترحة #5 — تسويق وجذب لاعبين</span>
                        <h3>🎟️ مولّد أكواد الهدايا الترويجية الفوري (Live Promo &amp; Gift Code Studio)</h3>
                        <p>أنشئ أكواد ترويجية لصفحة الفيسبوك أو الديسكورد (مثل <code>MOONQUEEN2026</code>) بعدد استخدامات محدد وهدية مخصصة يصرفها اللاعب داخل اللعبة بأمر <code>/claim</code> أو من NPC بدون ريستارت!</p>
                        <ul class="suite-points">
                            <li>قفل تلقائي بعد مرة واحدة لكل حساب أو لكل جهاز (MAC Address)</li>
                            <li>تحديد تاريخ انتهاء بالساعة والدقيقة وحزمة جوائز مرنة</li>
                            <li>عداد حي يعرض من قام بصرف الكود لحظة بلحظة</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">فكرة مقترحة #6 — حماية الاقتصاد</span>
                        <h3>🛡️ رادار مراقبة الاقتصاد وكاشف التضخم (Live Economy &amp; Anti-Dupe Watchdog)</h3>
                        <p>لوحة تراقب إجمالي الـ CPs والفضة وقطع الـ +12 في السيرفر وتنبهك فوراً لو زادت ثروة لاعب فجأة بشكل غير طبيعي!</p>
                        <ul class="suite-points">
                            <li>كشف أعلى 20 حساب في الـ CPs والفضة والـ DragonBalls بضغطة زر</li>
                            <li>تتبع حي لعمليات المتاجرة الكبيرة (<code>cq_trade</code>) ورمي الأدوات على الأرض</li>
                            <li>تجميد مؤقت لحساب مشتبه به لحين المراجعة</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">فكرة مقترحة #7 — أمان الداتابيز</span>
                        <h3>💾 خزنة النسخ الاحتياطي الذكي بضغطة زر (One-Click MariaDB Snapshot Vault)</h3>
                        <p>حفظ نسخة احتياطية مضغوطة ومؤرخة من <code>moonqueen_account</code> و <code>moonqueen_game</code> قبل أي تعديل كبير بضغطة واحدة من المتصفح!</p>
                        <ul class="suite-points">
                            <li>جدولة نسخ تلقائي كل 6 ساعات مع الاحتفاظ بآخر 10 نسخ نظيفة</li>
                            <li>حفظ النسخ في مجلد محلي آمن مستبعد من Git بالكامل</li>
                            <li>عرض حجم كل قاعدة بيانات وعدد الحسابات والشخصيات المسجلة</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">فكرة مقترحة #8 — تصميم بدون كود</span>
                        <h3>🧙 المصمم المرئي لحوارات الـ NPCs والـ Teleporters (Visual NPC &amp; Lua Builder)</h3>
                        <p>صمّم أي NPC جديد (مرشد، ناقل خرائط، بائع حزم، مستبدل توكنات) عبر نموذج عربي بسيط يولّد كود Lua نظيف ويفعّله حياً بدون ريستارت!</p>
                        <ul class="suite-points">
                            <li>معاينة شكل حوار الـ NPC وصورته (Avatar) قبل الحفظ</li>
                            <li>دعم شروط اللفل والريبورن والـ CPs والفضة أوتوماتيكياً</li>
                            <li>ريلود فوري لـ <code>LuaScriptManager</code> بضغطة واحدة</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">فكرة مقترحة #9 — عدالة اللعب</span>
                        <h3>🕵️ كاشف الحسابات المتعددة وحارس الـ MAC/IP (Multi-Client &amp; Bot Inspector)</h3>
                        <p>يعرض اللاعبين الفاتحين أكثر من حساب من نفس الـ IP أو الـ MAC Address، مع إمكانية تحديد حد أقصى للحسابات داخل المسابقات!</p>
                        <ul class="suite-points">
                            <li>منع دخول أكثر من حساب واحد من نفس الجهاز في Elite PK و CTF و Skill PK</li>
                            <li>قائمة حظر/فك حظر فوري للـ Account أو الـ MAC من نفس الشاشة</li>
                            <li>تنبيه عند محاولة دخول متكررة فاشلة</li>
                        </ul>
                    </article>

                    <article class="suite-card">
                        <span class="suite-badge">فكرة مقترحة #10 — أتمتة السيرفر</span>
                        <h3>⏰ المجدول التلقائي لإيفنتات السيرفر (Auto Happy-Hour &amp; Invasion Scheduler)</h3>
                        <p>جدول أسبوعي ذكي يشغّل Happy Hour 2x كل خميس وجمعة وينزّل زعيم غزوة كل ساعتين أوتوماتيكياً ويرجع للوضع البيور 1x وحده بعد انتهاء الوقت!</p>
                        <ul class="suite-points">
                            <li>يعمل أوتوماتيكياً حتى وأنت نائم أو خارج البيت</li>
                            <li>إعلانات عدّ تنازلي قبل الإيفنت بـ 5 دقائق</li>
                            <li>عودة تلقائية آمنة لريتات السيرفر الأصلية فور انتهاء المدة</li>
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
