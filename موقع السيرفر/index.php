<?php
declare(strict_types=1);

define('MOONQUEEN_PORTAL', true);
require_once __DIR__ . '/includes/bootstrap.php';

[$sysCfg, $siteCfg] = mq_load_configs();
$stats        = mq_get_live_stats($sysCfg);
$csrfToken    = mq_csrf_token();
$captchaLabel = mq_get_captcha_label();
$dbConfigured = mq_is_db_configured($sysCfg);

$general   = $siteCfg['general'] ?? [];
$downloads = $siteCfg['downloads'] ?? [];
$steps     = $siteCfg['install_steps'] ?? [];
$social    = $siteCfg['social'] ?? [];
$systems   = $siteCfg['game_systems'] ?? [];
$events    = $siteCfg['events_schedule'] ?? [];

$serverName = $MQ_RIGHTS['server_name'];
$developer  = $MQ_RIGHTS['developer'];
?>
<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title><?= mq_esc($serverName) ?> — الموقع الرسمي للسيرفر | تطوير <?= mq_esc($developer) ?></title>
  <meta name="description" content="<?= mq_esc((string)($general['hero_subtitle'] ?? '')) ?>">
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Cairo:wght@500;700;800;900&display=swap" rel="stylesheet">
  <link rel="stylesheet" href="assets/css/style.css">
</head>
<body>
  <canvas id="emberCanvas" aria-hidden="true"></canvas>

  <div class="page-wrap">
    <?php if (!empty($general['announcement'])): ?>
      <div class="announce-bar">
        <?= mq_esc((string)$general['announcement']) ?>
      </div>
    <?php endif; ?>

    <!-- شريط التنقل العلوي -->
    <header class="navbar">
      <div class="container nav-inner">
        <a href="#top" class="brand">
          <div class="brand-moon">MQ</div>
          <div>
            <span class="brand-title"><?= mq_esc($serverName) ?></span>
            <span class="brand-sub">Classic+ V6192 • By <?= mq_esc($developer) ?></span>
          </div>
        </a>

        <ul class="nav-links">
          <li><a href="#top">الرئيسية</a></li>
          <li><a href="#account" data-jump-auth="register">إنشاء حساب</a></li>
          <li><a href="#account" data-jump-auth="password">تغيير الباسورد</a></li>
          <li><a href="#online">اللاعبين الأونلاين</a></li>
          <li><a href="#download">تحميل اللعبة</a></li>
          <li><a href="#systems">شرح الأنظمة</a></li>
        </ul>

        <div class="status-pill">
          <span id="navStatusDot" class="status-dot <?= $stats['server_online'] ? '' : 'offline' ?>"></span>
          <span id="navStatusText">
            <?= $stats['server_online'] ? ('أونلاين (' . (int)$stats['online_count'] . ')') : 'مغلق مؤقتًا' ?>
          </span>
        </div>
      </div>
    </header>

    <!-- الواجهة الرئيسية (البنت والقمر الأحمر) -->
    <section id="top" class="hero">
      <div class="hero-bg"></div>
      <div class="container">
        <div class="hero-content">
          <div class="hero-tag">
            <span>🌙</span>
            <span><?= mq_esc((string)($general['hero_badge'] ?? 'V6192 Classic+')) ?></span>
          </div>
          <h1>
            ملحمة القمر الأحمر تبدأ في عالم <span><?= mq_esc($serverName) ?></span>
          </h1>
          <p><?= mq_esc((string)($general['hero_subtitle'] ?? '')) ?></p>
          <div class="hero-actions">
            <a href="#account" data-jump-auth="register" class="btn btn-primary">سجّل حسابك مجانًا الآن</a>
            <a href="#download" class="btn btn-outline">تحميل الكلينت واللودر</a>
            <a href="#systems" class="btn btn-outline">تعرّف على أنظمة السيرفر</a>
          </div>
        </div>
      </div>
    </section>

    <!-- شريط إحصائيات السيرفر الحية -->
    <div class="container">
      <div class="stats-strip">
        <div class="stat-box">
          <span class="stat-label">حالة السيرفر الآن</span>
          <span id="statServerStatus" class="stat-value <?= $stats['server_online'] ? 'online' : 'crimson' ?>">
            <?= $stats['server_online'] ? 'متصل وشغال (ONLINE)' : 'تحت التجهيز / أوفلاين' ?>
          </span>
        </div>
        <div class="stat-box">
          <span class="stat-label">اللاعبين المتصلين الآن</span>
          <span id="statOnlineCount" class="stat-value gold"><?= (int)$stats['online_count'] ?></span>
        </div>
        <div class="stat-box">
          <span class="stat-label">إجمالي الحسابات المسجلة</span>
          <span id="statTotalAccounts" class="stat-value"><?= (int)$stats['total_accounts'] ?></span>
        </div>
        <div class="stat-box">
          <span class="stat-label">طراز وإصدار السيرفر</span>
          <span class="stat-value crimson"><?= mq_esc((string)($general['version_label'] ?? 'V6192 Classic+')) ?></span>
        </div>
      </div>
    </div>

    <!-- قسم إنشاء الحساب وتغيير كلمة المرور -->
    <section id="account" class="section">
      <div class="container">
        <div class="section-head">
          <span class="section-kicker">بوابة حسابات الأبطال</span>
          <h2>أنشئ حسابك أو غيّر كلمة مرورك في ثوانٍ</h2>
          <p>حسابك بيتفعل فورًا على سيرفر <?= mq_esc($serverName) ?> ومؤمّن بتشفير قياسي كامل لحماية شخصيتك وممتلكاتك.</p>
        </div>

        <div class="account-grid">
          <div class="portrait-card">
            <img src="assets/img/queen-portrait.jpg" alt="<?= mq_esc($serverName) ?> Warrior Queen">
            <div class="portrait-overlay">
              <h3>إرشادات سريعة لحساب آمن</h3>
              <ul class="tips-list">
                <li>اسم المستخدم بالحروف أو الأرقام الإنجليزية فقط (من 4 إلى 16 حرف).</li>
                <li>كلمة المرور من 6 إلى 31 حرف/رقم إنجليزي (كل الحروف شغالة 100% بما فيها حرف z).</li>
                <li>متستخدمش نفس الباسورد اللي بتستخدمه في أي سيرفر تاني لحماية أكونتك.</li>
                <li>بعد التسجيل افتح اللودر الرسمي <strong>MoonQueen.exe</strong> وادخل للعبة فورًا!</li>
              </ul>
            </div>
          </div>

          <div class="auth-card">
            <div class="tabs-bar">
              <button type="button" class="tab-btn active" data-auth-tab="register">إنشاء حساب جديد</button>
              <button type="button" class="tab-btn" data-auth-tab="password">تغيير كلمة المرور</button>
            </div>

            <!-- تبويب تسجيل حساب جديد -->
            <div class="tab-pane active" data-auth-pane="register">
              <form class="js-auth-form" method="post" action="api.php" autocomplete="off">
                <input type="hidden" name="action" value="register">
                <input type="hidden" name="csrf_token" value="<?= mq_esc($csrfToken) ?>">

                <div class="alert-box" role="alert"></div>

                <div class="form-group">
                  <label for="reg_username">اسم المستخدم (Account ID)</label>
                  <input id="reg_username" class="form-control" type="text" name="username" minlength="4" maxlength="16" required placeholder="مثال: Hero2026">
                </div>

                <div class="form-group">
                  <label for="reg_password">كلمة المرور</label>
                  <div class="input-wrap">
                    <input id="reg_password" class="form-control" type="password" name="password" minlength="6" maxlength="31" required placeholder="6 إلى 31 حرف أو رقم إنجليزي">
                    <button type="button" class="pw-toggle">إظهار</button>
                  </div>
                </div>

                <div class="form-group">
                  <label for="reg_confirm">تأكيد كلمة المرور</label>
                  <div class="input-wrap">
                    <input id="reg_confirm" class="form-control" type="password" name="confirm_password" minlength="6" maxlength="31" required placeholder="أعد كتابة كلمة المرور">
                    <button type="button" class="pw-toggle">إظهار</button>
                  </div>
                </div>

                <div class="form-group">
                  <label>سؤال الأمان ضد البوتات</label>
                  <div class="captcha-row">
                    <div class="captcha-question js-captcha-label"><?= mq_esc($captchaLabel) ?></div>
                    <input class="form-control" type="text" name="captcha" required placeholder="اكتب الناتج بالأرقام">
                  </div>
                </div>

                <button type="submit" class="btn btn-primary" style="width: 100%;">إنشاء الحساب الآن</button>
              </form>
            </div>

            <!-- تبويب تغيير كلمة المرور -->
            <div class="tab-pane" data-auth-pane="password">
              <form class="js-auth-form" method="post" action="api.php" autocomplete="off">
                <input type="hidden" name="action" value="change_password">
                <input type="hidden" name="csrf_token" value="<?= mq_esc($csrfToken) ?>">

                <div class="alert-box" role="alert"></div>

                <div class="form-group">
                  <label for="cp_username">اسم المستخدم</label>
                  <input id="cp_username" class="form-control" type="text" name="username" minlength="4" maxlength="16" required placeholder="اكتب اسم حسابك">
                </div>

                <div class="form-group">
                  <label for="cp_old">كلمة المرور الحالية</label>
                  <div class="input-wrap">
                    <input id="cp_old" class="form-control" type="password" name="old_password" minlength="1" maxlength="31" required placeholder="كلمة المرور القديمة">
                    <button type="button" class="pw-toggle">إظهار</button>
                  </div>
                </div>

                <div class="form-group">
                  <label for="cp_new">كلمة المرور الجديدة</label>
                  <div class="input-wrap">
                    <input id="cp_new" class="form-control" type="password" name="new_password" minlength="6" maxlength="31" required placeholder="كلمة المرور الجديدة (6-31 حرف)">
                    <button type="button" class="pw-toggle">إظهار</button>
                  </div>
                </div>

                <div class="form-group">
                  <label for="cp_confirm">تأكيد كلمة المرور الجديدة</label>
                  <div class="input-wrap">
                    <input id="cp_confirm" class="form-control" type="password" name="confirm_password" minlength="6" maxlength="31" required placeholder="أعد كتابة كلمة المرور الجديدة">
                    <button type="button" class="pw-toggle">إظهار</button>
                  </div>
                </div>

                <div class="form-group">
                  <label>سؤال الأمان ضد البوتات</label>
                  <div class="captcha-row">
                    <div class="captcha-question js-captcha-label"><?= mq_esc($captchaLabel) ?></div>
                    <input class="form-control" type="text" name="captcha" required placeholder="اكتب الناتج بالأرقام">
                  </div>
                </div>

                <button type="submit" class="btn btn-primary" style="width: 100%;">حفظ كلمة المرور الجديدة</button>
              </form>
            </div>

            <?php if (!$dbConfigured): ?>
              <div class="db-setup-note">
                تنبيه لمالك السيرفر فقط: لتفعيل ربط الحسابات على جهازك، انسخ ملف <strong>config.sample.php</strong> إلى <strong>config.local.php</strong> واكتب باسورد الـ MySQL بتاعك جواه.
              </div>
            <?php endif; ?>
          </div>
        </div>
      </div>
    </section>

    <!-- قسم اللاعبين الأونلاين وأقوى الأبطال -->
    <section id="online" class="section section-alt">
      <div class="container">
        <div class="section-head">
          <span class="section-kicker">نبض العالم المباشر</span>
          <h2>اللاعبين المتصلين الآن وأبطال <?= mq_esc($serverName) ?></h2>
          <p>قائمة حية بتتحدث تلقائيًا لعرض الأبطال المتواجدين داخل عالم اللعبة الآن وأعلى الشخصيات مستوى.</p>
        </div>

        <div class="roster-toolbar">
          <div class="roster-tabs">
            <button type="button" class="tab-btn active" data-roster-tab="online">
              اللاعبين الأونلاين الآن (<?= (int)$stats['online_count'] ?>)
            </button>
            <button type="button" class="tab-btn" data-roster-tab="top">
              أقوى أبطال السيرفر
            </button>
          </div>

          <button type="button" id="btnRefreshStats" class="btn btn-outline" style="padding: 9px 18px; font-size: 0.9rem;">
            تحديث القائمة الآن ↻
          </button>
        </div>

        <!-- قائمة المتصلين الآن -->
        <div class="tab-pane active" data-roster-pane="online">
          <div id="onlinePlayersGrid" class="players-grid">
            <?php if (empty($stats['online_players'])): ?>
              <div class="empty-roster">
                <?= $stats['server_online']
                    ? 'لا يوجد لاعبون متصلون في هذه اللحظة — ادخل دلوقتي وكن أول أبطال الخريطة!'
                    : 'السيرفر في وضع التجهيز حاليًا — بمجرد تشغيل السيرفر هيظهر كل اللاعبين الأونلاين هنا تلقائيًا.' ?>
              </div>
            <?php else: ?>
              <?php foreach ($stats['online_players'] as $p): ?>
                <div class="player-card">
                  <div class="player-top">
                    <span class="player-name"><?= mq_esc((string)$p['name']) ?></span>
                    <span class="rank-chip">متصل الآن</span>
                  </div>
                  <div class="player-meta">
                    <span class="meta-pill"><?= mq_esc((string)$p['profession']) ?></span>
                    <span class="meta-pill reborn">
                      <?= (int)$p['reborn'] >= 2 ? '2nd Reborn ✦✦' : ((int)$p['reborn'] === 1 ? '1st Reborn ✦' : 'Non-Reborn') ?>
                    </span>
                    <span class="meta-pill">Lv. <?= (int)$p['level'] ?></span>
                    <span class="meta-pill">PK: <?= (int)$p['pk'] ?></span>
                  </div>
                </div>
              <?php endforeach; ?>
            <?php endif; ?>
          </div>
        </div>

        <!-- قائمة أقوى الأبطال -->
        <div class="tab-pane" data-roster-pane="top">
          <div id="topHeroesGrid" class="players-grid">
            <?php if (empty($stats['top_heroes'])): ?>
              <div class="empty-roster">
                قائمة الأبطال هتتملي تلقائيًا مع أول الشخصيات اللي تنزل عالم <?= mq_esc($serverName) ?>!
              </div>
            <?php else: ?>
              <?php foreach ($stats['top_heroes'] as $idx => $p): ?>
                <div class="player-card">
                  <div class="player-top">
                    <span class="player-name"><?= mq_esc((string)$p['name']) ?></span>
                    <span class="rank-chip">#<?= (int)($idx + 1) ?></span>
                  </div>
                  <div class="player-meta">
                    <span class="meta-pill"><?= mq_esc((string)$p['profession']) ?></span>
                    <span class="meta-pill reborn">
                      <?= (int)$p['reborn'] >= 2 ? '2nd Reborn ✦✦' : ((int)$p['reborn'] === 1 ? '1st Reborn ✦' : 'Non-Reborn') ?>
                    </span>
                    <span class="meta-pill">Lv. <?= (int)$p['level'] ?></span>
                    <span class="meta-pill">PK: <?= (int)$p['pk'] ?></span>
                  </div>
                </div>
              <?php endforeach; ?>
            <?php endif; ?>
          </div>
        </div>
      </div>
    </section>

    <!-- قسم تحميل اللعبة واللودر -->
    <section id="download" class="section">
      <div class="container">
        <div class="section-head">
          <span class="section-kicker">مركز التحميل الرسمي</span>
          <h2>حمّل كلينت ولودر <?= mq_esc($serverName) ?> وابدأ اللعب</h2>
          <p>كل ملفات التشغيل مفحوصة ومجهزة على إصدار V6192 عشان تشتغل معاك مباشرة بدون أي تعقيد.</p>
        </div>

        <div class="download-grid">
          <?php foreach ($downloads as $dl):
            $hasUrl = !empty(trim((string)($dl['url'] ?? '')));
          ?>
            <div class="download-card">
              <div>
                <div class="dl-badge-row">
                  <span class="dl-badge"><?= mq_esc((string)($dl['badge'] ?? 'تحميل')) ?></span>
                  <span class="dl-status <?= $hasUrl ? 'ready' : '' ?>">
                    <?= $hasUrl ? ('جاهز للتحميل • ' . mq_esc((string)($dl['size'] ?? ''))) : 'قريبًا — جاري تجهيز الرابط' ?>
                  </span>
                </div>
                <h3><?= mq_esc((string)($dl['title'] ?? '')) ?></h3>
                <p style="margin-top: 8px;"><?= mq_esc((string)($dl['description'] ?? '')) ?></p>
              </div>

              <?php if ($hasUrl): ?>
                <a href="<?= mq_esc(trim((string)$dl['url'])) ?>" target="_blank" rel="noopener noreferrer" class="btn btn-primary">
                  ⬇ <?= mq_esc((string)($dl['button_text'] ?? 'تحميل الآن')) ?>
                </a>
              <?php else: ?>
                <a href="#download" data-coming-soon="1" class="btn btn-disabled">
                  ⏳ <?= mq_esc((string)($dl['button_text'] ?? 'تحميل')) ?> (قريبًا)
                </a>
              <?php endif; ?>
            </div>
          <?php endforeach; ?>
        </div>

        <?php if (!empty($steps)): ?>
          <div class="steps-card">
            <h3>خطوات التشغيل السريعة في دقيقتين:</h3>
            <ol class="steps-grid">
              <?php foreach ($steps as $i => $stepText): ?>
                <li class="step-item">
                  <span class="step-num"><?= (int)($i + 1) ?></span>
                  <span><?= mq_esc((string)$stepText) ?></span>
                </li>
              <?php endforeach; ?>
            </ol>
          </div>
        <?php endif; ?>
      </div>
    </section>

    <!-- قسم شرح أنظمة اللعبة المبسطة للاعبين -->
    <section id="systems" class="section section-alt">
      <div class="container">
        <div class="section-head">
          <span class="section-kicker">دليل عالم السيرفر</span>
          <h2>شرح أنظمة ومميزات سيرفر <?= mq_esc($serverName) ?></h2>
          <p>كل اللي محتاج تعرفه كلاعب عن طبيعة السيرفر، الفارم، التجهيز، والبطولات اليومية والأسبوعية بشرح بسيط وواضح.</p>
        </div>

        <div class="systems-banner">
          <img src="assets/img/systems-battle.jpg" alt="أنظمة وحروب عالم <?= mq_esc($serverName) ?>">
          <div class="systems-banner-text">
            <span class="sys-tag">تجربة Classic+ الأصيلة</span>
            <h3>سيرفر معمول عشان تستمتع بكل دقيقة لعب</h3>
            <p style="color: #e2e8f0;">
              في <?= mq_esc($serverName) ?> مفيش حاجة اسمها أكونت يخلص في ساعة ولا تعجيز يضيع وقتك؛ كل وحش بتقتله، وكل قطعة بترقيها، وكل بطولة بتدخلها بتفرق فعلًا في قوة اسمك وسط السيرفر.
            </p>
          </div>
        </div>

        <div class="systems-grid">
          <?php foreach ($systems as $sys): ?>
            <article class="system-card">
              <span class="sys-tag"><?= mq_esc((string)($sys['tag'] ?? 'نظام السيرفر')) ?></span>
              <h3><?= mq_esc((string)($sys['title'] ?? '')) ?></h3>
              <p><?= mq_esc((string)($sys['description'] ?? '')) ?></p>
              <?php if (!empty($sys['highlights']) && is_array($sys['highlights'])): ?>
                <ul class="sys-highlights">
                  <?php foreach ($sys['highlights'] as $hl): ?>
                    <li><?= mq_esc((string)$hl) ?></li>
                  <?php endforeach; ?>
                </ul>
              <?php endif; ?>
            </article>
          <?php endforeach; ?>
        </div>

        <?php if (!empty($events)): ?>
          <div class="section-head" style="margin-bottom: 26px;">
            <span class="section-kicker">مواعيد المنافسات والحروب</span>
            <h2>أبرز الفعاليات والبطولات داخل اللعبة</h2>
          </div>

          <div class="events-grid">
            <?php foreach ($events as $ev): ?>
              <div class="event-card">
                <h4><?= mq_esc((string)($ev['name'] ?? '')) ?></h4>
                <div class="event-time">⏰ <?= mq_esc((string)($ev['time'] ?? '')) ?></div>
                <div class="event-reward">🏆 الجوائز: <?= mq_esc((string)($ev['reward'] ?? '')) ?></div>
              </div>
            <?php endforeach; ?>
          </div>
        <?php endif; ?>
      </div>
    </section>

    <!-- تذييل الموقع وحقوق المطور المؤمّنة -->
    <footer class="footer">
      <div class="container">
        <?php
          $hasSocial = !empty($social['discord']) || !empty($social['whatsapp']) || !empty($social['facebook']);
          if ($hasSocial):
        ?>
          <div class="social-row">
            <?php if (!empty($social['discord'])): ?>
              <a class="btn btn-outline" href="<?= mq_esc((string)$social['discord']) ?>" target="_blank" rel="noopener noreferrer">مجتمع Discord</a>
            <?php endif; ?>
            <?php if (!empty($social['whatsapp'])): ?>
              <a class="btn btn-outline" href="<?= mq_esc((string)$social['whatsapp']) ?>" target="_blank" rel="noopener noreferrer">جروب WhatsApp</a>
            <?php endif; ?>
            <?php if (!empty($social['facebook'])): ?>
              <a class="btn btn-outline" href="<?= mq_esc((string)$social['facebook']) ?>" target="_blank" rel="noopener noreferrer">صفحة Facebook</a>
            <?php endif; ?>
          </div>
        <?php endif; ?>

        <p class="footer-credits">
          جميع الحقوق محفوظة © <?= date('Y') ?> لسيرفر <strong><?= mq_esc($serverName) ?></strong> (V6192 Classic+) —
          برمجة وتطوير المطور <strong><?= mq_esc($developer) ?></strong>.
        </p>
      </div>
    </footer>
  </div>

  <div id="mqToast" class="toast-popup" role="status" aria-live="polite"></div>
  <script src="assets/js/app.js"></script>
</body>
</html>
