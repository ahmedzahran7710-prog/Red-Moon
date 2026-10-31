(function () {
    'use strict';

    const PRESETS = {
        classic: {
            PresetName: 'Pure Official Conquer (1x)',
            ExperienceRate: 1.0,
            MagicExpRate: 1.0,
            WeaponSkillExpRate: 1.0,
            SilverDropRate: 1.0,
            SilverDropChancePercent: 25,
            EquipmentDropRate: 1.0,
            QualityItemRate: 1.0,
            SocketDropRate: 1.0,
            PlusOneDropRate: 1.0,
            DragonBallDropRate: 1.0,
            MeteorDropPer100k: 0,
            MonsterCpChancePercent: 0.0,
            MonsterCpMin: 1,
            MonsterCpMax: 3,
            suggestedMsg: 'تم ضبط ريتات السيرفر على الأساس الرسمي النقي لكونكر أونلاين (Pure Official 1x).'
        },
        weekend: {
            PresetName: 'Weekend Boost (2x)',
            ExperienceRate: 2.0,
            MagicExpRate: 2.0,
            WeaponSkillExpRate: 2.0,
            SilverDropRate: 1.5,
            SilverDropChancePercent: 30,
            EquipmentDropRate: 1.25,
            QualityItemRate: 1.25,
            SocketDropRate: 1.25,
            PlusOneDropRate: 1.25,
            DragonBallDropRate: 1.5,
            MeteorDropPer100k: 15,
            MonsterCpChancePercent: 0.0,
            MonsterCpMin: 1,
            MonsterCpMax: 3,
            suggestedMsg: 'انطلق إيفنت نهاية الأسبوع في MoonQueen! الخبرة والمهارات 2x والفضة والدروب مضاعف!'
        },
        happyhour: {
            PresetName: 'Happy Hour Fever (3x EXP)',
            ExperienceRate: 3.0,
            MagicExpRate: 2.5,
            WeaponSkillExpRate: 2.5,
            SilverDropRate: 2.0,
            SilverDropChancePercent: 35,
            EquipmentDropRate: 1.5,
            QualityItemRate: 1.5,
            SocketDropRate: 1.5,
            PlusOneDropRate: 1.5,
            DragonBallDropRate: 2.0,
            MeteorDropPer100k: 25,
            MonsterCpChancePercent: 0.0,
            MonsterCpMin: 1,
            MonsterCpMax: 3,
            suggestedMsg: 'ساعة الذروة بدأت الآن في MoonQueen! خبرة 3x وفضة 2x لجميع الأبطال!'
        },
        dropfever: {
            PresetName: 'Drop & CP Hunt Event',
            ExperienceRate: 1.5,
            MagicExpRate: 1.5,
            WeaponSkillExpRate: 1.5,
            SilverDropRate: 1.5,
            SilverDropChancePercent: 32,
            EquipmentDropRate: 2.0,
            QualityItemRate: 2.0,
            SocketDropRate: 2.0,
            PlusOneDropRate: 2.0,
            DragonBallDropRate: 3.0,
            MeteorDropPer100k: 45,
            MonsterCpChancePercent: 1.5,
            MonsterCpMin: 1,
            MonsterCpMax: 5,
            suggestedMsg: 'إيفنت صيد النوادر والـ CPs نشط الآن في MoonQueen! دروب وجودة 2x وفرصة CPs من الوحوش!'
        }
    };

    const RATE_FIELDS = [
        'ExperienceRate',
        'MagicExpRate',
        'WeaponSkillExpRate',
        'SilverDropRate',
        'SilverDropChancePercent',
        'EquipmentDropRate',
        'QualityItemRate',
        'SocketDropRate',
        'PlusOneDropRate',
        'DragonBallDropRate',
        'MeteorDropPer100k',
        'MonsterCpChancePercent',
        'MonsterCpMin',
        'MonsterCpMax'
    ];

    const rootEl = document.getElementById('mqStudioApp');
    if (!rootEl) {
        return;
    }

    const csrfToken = rootEl.getAttribute('data-csrf') || '';
    let isDirty = false;
    let awaitingServerReload = false;
    let baselineReloadStamp = '';

    // State for Tools #1..#4
    const pendingCommandIds = new Set();
    const seenResultIds = new Set();
    let latestEvents = [];
    let latestPlayers = [];
    let latestBosses = [];
    let selectedPlayerId = 0;
    let selectedPlayerName = '';
    let shopsSnapshot = { shops: [] };
    let selectedShopNpcId = 2888;
    let pendingCatalogTarget = '';

    function esc(str) {
        return String(str == null ? '' : str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function showToast(msg, isError) {
        const toast = document.getElementById('studioToast');
        if (!toast) return;
        toast.textContent = msg;
        toast.classList.toggle('error', !!isError);
        toast.classList.add('show');
        clearTimeout(showToast._timer);
        showToast._timer = setTimeout(function () {
            toast.classList.remove('show');
        }, 4500);
    }

    function setDirty(flag) {
        isDirty = !!flag;
        const badge = document.getElementById('dirtyBadge');
        if (badge) {
            badge.classList.toggle('visible', isDirty);
        }
    }

    function getFieldVal(name) {
        const num = document.getElementById('num_' + name);
        return num ? parseFloat(num.value || '0') : 0;
    }

    function setFieldVal(name, val) {
        const num = document.getElementById('num_' + name);
        const rng = document.getElementById('rng_' + name);
        if (num) num.value = val;
        if (rng) rng.value = val;
    }

    function updateLiveMathPreviews() {
        const silverChance = Math.round(getFieldVal('SilverDropChancePercent'));
        const greenChance = Math.max(0, Math.floor((silverChance * 8) / 25));
        const silverTag = document.getElementById('calcSilver');
        if (silverTag) {
            silverTag.textContent = 'الوحش العادي: ' + silverChance + '% | الوحش الأخضر: ' + greenChance + '%';
        }

        const qRate = Math.max(0, getFieldVal('QualityItemRate'));
        const superPct = Math.min(100, (2500 * qRate) / 100000).toFixed(3);
        const elitePct = Math.min(100, ((14500 - 2500) * qRate) / 100000).toFixed(2);
        const uniquePct = Math.min(100, ((56500 - 14500) * qRate) / 100000).toFixed(2);
        const qTag = document.getElementById('calcQuality');
        if (qTag) {
            qTag.textContent = 'داخل لفة الجودة -> Super: ' + superPct + '% | Elite: ' + elitePct + '% | Unique: ' + uniquePct + '%';
        }

        const socRate = Math.max(0, getFieldVal('SocketDropRate'));
        const socTag = document.getElementById('calcSocket');
        if (socTag) {
            if (socRate <= 0) {
                socTag.textContent = 'مغلق (0%) — لا تسقط أسلحة بسوكت من الوحوش';
            } else {
                const oneSocPct = Math.min(100, (100 * socRate) / 1000).toFixed(3);
                const twoSocPct = Math.min(100, (5 * socRate) / 1000).toFixed(4);
                socTag.textContent = 'للأسلحة الساقطة -> 1-Socket: ' + oneSocPct + '% | 2-Socket: ' + twoSocPct + '%';
            }
        }

        const plusRate = Math.max(0, getFieldVal('PlusOneDropRate'));
        const plusTag = document.getElementById('calcPlusOne');
        if (plusTag) {
            const plusPct = Math.min(100, 1.0 * plusRate).toFixed(2);
            plusTag.textContent = 'نسبة سقوط قطعة +1 من الوحوش: ' + plusPct + '%';
        }

        const dbRate = Math.max(0, getFieldVal('DragonBallDropRate'));
        const dbTag = document.getElementById('calcDb');
        if (dbTag) {
            if (dbRate <= 0) {
                dbTag.textContent = 'مغلق (0%) — لا تسقط DragonBall من الوحوش';
            } else {
                const approxKills = Math.max(1, Math.round(86400 / dbRate));
                dbTag.textContent = 'معدل أصلي نقي: تقريباً 1 DragonBall كل ' + approxKills.toLocaleString('en-US') + ' وحش على الأرض';
            }
        }

        const meteorPer100k = Math.max(0, Math.round(getFieldVal('MeteorDropPer100k')));
        const meteorTag = document.getElementById('calcMeteor');
        if (meteorTag) {
            if (meteorPer100k === 0) {
                meteorTag.textContent = 'مغلق افتراضياً (0%) — يعتمد على دروب الخرائط الأصلي فقط';
            } else {
                const pct = (meteorPer100k / 1000).toFixed(3);
                const approxKills = Math.max(1, Math.round(100000 / meteorPer100k));
                meteorTag.textContent = 'فرصة إضافية: ' + pct + '% (تقريباً 1 ميتيور كل ' + approxKills.toLocaleString('en-US') + ' وحش)';
            }
        }

        const cpChance = Math.max(0, getFieldVal('MonsterCpChancePercent'));
        const cpMin = Math.max(1, Math.round(getFieldVal('MonsterCpMin')));
        const cpMax = Math.max(cpMin, Math.round(getFieldVal('MonsterCpMax')));
        const cpTag = document.getElementById('calcMonsterCp');
        if (cpTag) {
            if (cpChance <= 0) {
                cpTag.textContent = 'مغلق حالياً (كونكر الأصلية 0% بدون CPs عشوائية من الوحوش)';
            } else {
                cpTag.textContent = 'مُفعّل: فرصة ' + cpChance + '% لربح (' + cpMin + ' إلى ' + cpMax + ' CPs) مباشرة في الحقيبة';
            }
        }
    }

    // Bind sliders <-> number boxes
    RATE_FIELDS.forEach(function (name) {
        const rng = document.getElementById('rng_' + name);
        const num = document.getElementById('num_' + name);
        if (rng && num) {
            rng.addEventListener('input', function () {
                num.value = rng.value;
                setDirty(true);
                updateLiveMathPreviews();
            });
            num.addEventListener('input', function () {
                rng.value = num.value;
                setDirty(true);
                updateLiveMathPreviews();
            });
        } else if (num) {
            num.addEventListener('input', function () {
                setDirty(true);
                updateLiveMathPreviews();
            });
        }
    });

    const presetNameInput = document.getElementById('inputPresetName');
    if (presetNameInput) {
        presetNameInput.addEventListener('input', function () {
            setDirty(true);
        });
    }

    document.querySelectorAll('[data-preset]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const key = btn.getAttribute('data-preset');
            const p = PRESETS[key];
            if (!p) return;

            document.querySelectorAll('[data-preset]').forEach(function (b) {
                b.classList.remove('selected');
            });
            btn.classList.add('selected');

            if (presetNameInput) presetNameInput.value = p.PresetName;
            RATE_FIELDS.forEach(function (f) {
                if (typeof p[f] !== 'undefined') {
                    setFieldVal(f, p[f]);
                }
            });

            const msgInput = document.getElementById('broadcastMessage');
            if (msgInput && p.suggestedMsg) {
                msgInput.value = p.suggestedMsg;
            }

            setDirty(true);
            updateLiveMathPreviews();
            showToast('تم تحميل قالب "' + p.PresetName + '" — اضغط "حفظ وريلود فوري" لتطبيقه الآن!');
        });
    });

    // Tabs switching helper
    function switchStudioTab(target) {
        document.querySelectorAll('[data-tab-target]').forEach(function (b) {
            b.classList.toggle('active', b.getAttribute('data-tab-target') === target);
        });
        document.querySelectorAll('.tab-pane').forEach(function (pane) {
            pane.classList.toggle('active', pane.id === target);
        });
        if (target === 'paneShopsStudio' && (!shopsSnapshot.shops || shopsSnapshot.shops.length === 0)) {
            loadShopsSnapshot();
        } else if (target === 'paneAccountsStudio') {
            loadAccountsList();
        } else if (target === 'panePromosStudio') {
            loadPromosList();
        } else if (target === 'paneAutoBroadcastStudio') {
            loadAutoBroadcast();
        } else if (target === 'paneCustomDropsStudio') {
            loadCustomDrops();
        } else if (target === 'paneBackupEconomyStudio') {
            loadEconomyRadar();
        }
    }

    document.querySelectorAll('[data-tab-target]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            switchStudioTab(btn.getAttribute('data-tab-target'));
        });
    });

    async function apiPost(action, bodyObj) {
        bodyObj.csrf_token = csrfToken;
        const resp = await fetch('./api.php?action=' + encodeURIComponent(action), {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-Token': csrfToken
            },
            body: JSON.stringify(bodyObj)
        });
        return await resp.json();
    }

    async function sendStudioCommand(payload, onQueuedMsg) {
        try {
            const res = await apiPost('studio_command', payload);
            if (res && res.ok) {
                if (res.cmd_id) {
                    pendingCommandIds.add(res.cmd_id);
                }
                showToast(onQueuedMsg || res.message);
                setTimeout(pollStatus, 650);
                return res;
            }
            showToast((res && (res.error || res.message)) || 'تعذر إرسال الأمر.', true);
            return res;
        } catch (err) {
            showToast('تعذر الاتصال باللوحة المحلية.', true);
            return { ok: false };
        }
    }

    function collectRatesPayload() {
        const rates = {
            PresetName: presetNameInput ? presetNameInput.value : 'Pure Official Conquer (1x)'
        };
        RATE_FIELDS.forEach(function (f) {
            rates[f] = getFieldVal(f);
        });
        return rates;
    }

    async function saveAndReloadRates() {
        const saveBtns = document.querySelectorAll('.js-save-rates');
        saveBtns.forEach(function (b) { b.disabled = true; });

        try {
            const autoAnnounceEl = document.getElementById('autoAnnounceOnSave');
            const msgEl = document.getElementById('broadcastMessage');
            const chEl = document.getElementById('broadcastChannel');

            const lastReloadEl = document.getElementById('statLastReload');
            baselineReloadStamp = lastReloadEl ? lastReloadEl.textContent : '';

            const res = await apiPost('save_rates', {
                rates: collectRatesPayload(),
                announce_on_save: !!(autoAnnounceEl && autoAnnounceEl.checked),
                announce_message: msgEl ? msgEl.value : '',
                announce_channel: chEl ? chEl.value : 'TopLeft'
            });

            if (res.ok) {
                setDirty(false);
                if (res.status && res.status.live_connected) {
                    awaitingServerReload = true;
                    showToast('⚡ تم الحفظ! جاري تأكيد استلام الريلود داخل MoonQueen.Game...');
                } else {
                    showToast('✅ تم حفظ الريتات في ملف السيرفر! ستعمل تلقائياً فور تشغيل MoonQueen.Game.exe.');
                }
                if (res.status) renderStatus(res.status);
            } else {
                showToast(res.message || res.error || 'حدث خطأ أثناء الحفظ.', true);
            }
        } catch (err) {
            showToast('تعذر الاتصال بالواجهة المحلية، تأكد من استمرار تشغيل اللوحة.', true);
        } finally {
            saveBtns.forEach(function (b) { b.disabled = false; });
        }
    }

    document.querySelectorAll('.js-save-rates').forEach(function (btn) {
        btn.addEventListener('click', saveAndReloadRates);
    });

    const btnBroadcastNow = document.getElementById('btnBroadcastNow');
    if (btnBroadcastNow) {
        btnBroadcastNow.addEventListener('click', async function () {
            const msgEl = document.getElementById('broadcastMessage');
            const chEl = document.getElementById('broadcastChannel');
            btnBroadcastNow.disabled = true;
            try {
                const res = await apiPost('broadcast', {
                    message: msgEl ? msgEl.value : '',
                    channel: chEl ? chEl.value : 'TopLeft'
                });
                showToast(res.ok ? res.message : (res.error || res.message), !res.ok);
            } catch (e) {
                showToast('تعذر إرسال الإعلان حالياً.', true);
            } finally {
                btnBroadcastNow.disabled = false;
            }
        });
    }

    const btnReloadActions = document.getElementById('btnReloadActions');
    if (btnReloadActions) {
        btnReloadActions.addEventListener('click', async function () {
            btnReloadActions.disabled = true;
            try {
                const res = await apiPost('reload_actions', {});
                showToast(res.ok ? res.message : (res.error || res.message), !res.ok);
            } catch (e) {
                showToast('تعذر إرسال أمر ريلود الـ Actions.', true);
            } finally {
                btnReloadActions.disabled = false;
            }
        });
    }

    const btnSavePath = document.getElementById('btnSaveTargetPath');
    if (btnSavePath) {
        btnSavePath.addEventListener('click', async function () {
            const pathEl = document.getElementById('customRatesPathInput');
            btnSavePath.disabled = true;
            try {
                const res = await apiPost('set_target_path', {
                    custom_rates_path: pathEl ? pathEl.value : ''
                });
                showToast(res.ok ? res.message : (res.error || res.message), !res.ok);
                if (res.ok && res.status) {
                    renderStatus(res.status);
                }
            } catch (e) {
                showToast('تعذر حفظ المسار.', true);
            } finally {
                btnSavePath.disabled = false;
            }
        });
    }

    // =========================================================================
    // TOOL #1: LIVE EVENTS & TOURNAMENTS STUDIO
    // =========================================================================
    const CONTROLLABLE_EVENTS = new Set([
        'guild_war',
        'capture_the_flag',
        'elite_pk',
        'line_skill_pk',
        'family_war',
        'quiz_show',
        'horse_racing'
    ]);

    function renderEventsGrid(events) {
        latestEvents = Array.isArray(events) ? events : [];
        const grid = document.getElementById('eventsCardsGrid');
        if (!grid) return;

        if (latestEvents.length === 0) {
            grid.innerHTML = '<div class="control-hint">لا توجد بيانات إيفنتات حالياً.</div>';
            return;
        }

        grid.innerHTML = latestEvents.map(function (ev) {
            const active = !!ev.is_active;
            const controllable = CONTROLLABLE_EVENTS.has(ev.key);
            const extraOwner = ev.owner ? (' | المالك الحالي: <strong>' + esc(ev.owner) + '</strong>') : '';
            const extraUntil = ev.manual_until ? (' | مستمرة حتى: <code>' + esc(ev.manual_until) + '</code>') : '';

            const buttonsHtml = controllable
                ? '<div class="event-actions">' +
                    '<button type="button" class="btn btn-emerald btn-sm" data-ev-act="start" data-ev-key="' + esc(ev.key) + '">▶ تشغيل فوري الآن</button>' +
                    '<button type="button" class="btn btn-crimson btn-sm" data-ev-act="stop" data-ev-key="' + esc(ev.key) + '">⏹ إيقاف وحسم النتيجة</button>' +
                  '</div>'
                : '<div class="event-actions">' +
                    '<span class="control-hint" style="margin:0;">✅ مفعّلة تلقائياً على مدار الساعة في وضع Release و Debug</span>' +
                  '</div>';

            return (
                '<article class="event-card ' + (active ? 'active-war' : '') + '">' +
                    '<div class="event-top">' +
                        '<div>' +
                            '<div class="event-title">' + esc(ev.name) + '</div>' +
                            '<div class="event-meta">الخريطة: <code>#' + esc(ev.map_id) + '</code> | المرحلة: <code>' + esc(ev.stage) + '</code>' + extraOwner + extraUntil + '</div>' +
                        '</div>' +
                        '<span class="event-pill ' + (active ? 'running' : 'idle') + '">' +
                            (active ? '🔥 نشط الآن' : '⏸ متوقف / انتظار') +
                        '</span>' +
                    '</div>' +
                    buttonsHtml +
                '</article>'
            );
        }).join('');
    }

    const eventsGridEl = document.getElementById('eventsCardsGrid');
    if (eventsGridEl) {
        eventsGridEl.addEventListener('click', function (e) {
            const btn = e.target.closest('[data-ev-act]');
            if (!btn) return;
            const evKey = btn.getAttribute('data-ev-key');
            const act = btn.getAttribute('data-ev-act');
            const durEl = document.getElementById('eventDurationSelect');
            const bcEl = document.getElementById('eventBroadcastToggle');
            sendStudioCommand({
                type: 'event_control',
                event_key: evKey,
                sub_action: act,
                duration_minutes: durEl ? parseInt(durEl.value || '60', 10) : 60,
                broadcast: !!(bcEl && bcEl.checked)
            });
        });
    }

    // =========================================================================
    // TOOL #2: LIVE SHOP & MALL HOT-EDITOR
    // =========================================================================
    function updateClientShopSnippet() {
        const box = document.getElementById('clientShopSnippetCode');
        if (!box) return;
        const npcId = parseInt((document.getElementById('shopFormNpcId') || {}).value || '2888', 10) || 2888;
        const itemId = parseInt((document.getElementById('shopFormItemType') || {}).value || '1088000', 10) || 1088000;
        const moneyType = parseInt((document.getElementById('shopFormMoneyType') || {}).value || '1', 10);

        if (moneyType === 1 || npcId === 2888 || npcId === 6000 || npcId === 6001) {
            box.textContent =
                '; لملف الكلينت ini/emoneyShopV2.ini (عند إضافة سلعة جديدة كلياً للـ Shopping Mall):\n' +
                'Item=' + itemId + ' 1 0 0 0 0 0 0';
        } else {
            box.textContent =
                '; لملف الكلينت ini/Shop.dat تحت قسم [Shop' + npcId + '] (وزوّد ItemAmount بواحد):\n' +
                'ItemX=' + itemId;
        }
    }

    ['shopFormNpcId', 'shopFormItemType', 'shopFormMoneyType'].forEach(function (id) {
        const el = document.getElementById(id);
        if (el) el.addEventListener('input', updateClientShopSnippet);
        if (el) el.addEventListener('change', updateClientShopSnippet);
    });
    updateClientShopSnippet();

    async function loadShopsSnapshot() {
        try {
            const resp = await fetch('./api.php?action=shops', { cache: 'no-store' });
            if (!resp.ok) return;
            const data = await resp.json();
            if (data && data.ok && data.shops_data) {
                shopsSnapshot = data.shops_data;
                renderShopsSidebar();
                renderSelectedShopGoods();
            }
        } catch (err) {
            // Ignore
        }
    }

    function renderShopsSidebar() {
        const listEl = document.getElementById('shopsSidebarList');
        if (!listEl) return;
        const shops = Array.isArray(shopsSnapshot.shops) ? shopsSnapshot.shops : [];
        const filterVal = ((document.getElementById('shopFilterInput') || {}).value || '').trim().toLowerCase();

        if (shops.length === 0) {
            listEl.innerHTML = '<div class="control-hint">شغّل MoonQueen.Game.exe أو اضغط «ريلود فوري لكل الشوبات» ليقوم السيرفر بتصدير كل المتاجر المحمّلة فوراً! (ويمكنك الإضافة مباشرة عبر كتابة رقم المتجر).</div>';
            return;
        }

        const filtered = shops.filter(function (s) {
            if (!filterVal) return true;
            return String(s.npc_id).includes(filterVal) || String(s.name || '').toLowerCase().includes(filterVal);
        });

        if (filtered.length > 0 && !filtered.some(function (s) { return s.npc_id === selectedShopNpcId; })) {
            selectedShopNpcId = filtered[0].npc_id;
        }

        listEl.innerHTML = filtered.map(function (s) {
            const active = s.npc_id === selectedShopNpcId;
            return (
                '<button type="button" class="shop-nav-btn ' + (active ? 'active' : '') + '" data-select-shop="' + s.npc_id + '">' +
                    '<span><strong>' + esc(s.name) + '</strong> <small style="color:var(--text-muted);">(#' + s.npc_id + ')</small></span>' +
                    '<span class="event-pill idle">' + s.items_count + ' سلعة</span>' +
                '</button>'
            );
        }).join('');
    }

    function renderSelectedShopGoods() {
        const tbody = document.getElementById('selectedShopGoodsBody');
        const titleEl = document.getElementById('selectedShopTitle');
        if (!tbody) return;

        const shops = Array.isArray(shopsSnapshot.shops) ? shopsSnapshot.shops : [];
        const shop = shops.find(function (s) { return s.npc_id === selectedShopNpcId; });
        if (!shop) {
            tbody.innerHTML = '<tr><td colspan="8" style="text-align:center; color:var(--text-muted);">اختر متجراً من القائمة اليمنى بعد تشغيل السيرفر لعرض سلعه الحية...</td></tr>';
            return;
        }

        if (titleEl) {
            titleEl.textContent = '📦 سلع المتجر: ' + shop.name + ' (#' + shop.npc_id + ') — ' + shop.items_count + ' سلعة';
        }

        const q = ((document.getElementById('shopItemsFilterInput') || {}).value || '').trim().toLowerCase();
        const items = (shop.items || []).filter(function (it) {
            if (!q) return true;
            return String(it.itemtype).includes(q) || String(it.item_name || '').toLowerCase().includes(q);
        });

        if (items.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" style="text-align:center; color:var(--text-muted);">لا توجد سلع مطابقة في هذا المتجر.</td></tr>';
            return;
        }

        tbody.innerHTML = items.map(function (it) {
            const curLabel = it.moneytype === 1 ? '💎 CPs' : '🪙 Silver/Pts';
            return (
                '<tr>' +
                    '<td><code>' + it.itemtype + '</code></td>' +
                    '<td><strong>' + esc(it.item_name) + '</strong></td>' +
                    '<td>' + curLabel + '</td>' +
                    '<td>' + Number(it.cp_price || 0).toLocaleString('en-US') + '</td>' +
                    '<td>' + Number(it.bound_cp_price || 0).toLocaleString('en-US') + '</td>' +
                    '<td>' + Number(it.silver_price || 0).toLocaleString('en-US') + '</td>' +
                    '<td>' + (it.honor_price || 0) + ' / ' + (it.riding_price || 0) + '</td>' +
                    '<td>' +
                        '<button type="button" class="btn btn-ghost btn-sm" data-edit-good=\'' + JSON.stringify(it).replace(/'/g, '&#39;') + '\'>✏️ تعديل</button> ' +
                        '<button type="button" class="btn btn-crimson btn-sm" data-del-good="' + it.goods_id + '" data-del-npc="' + shop.npc_id + '" data-del-item="' + it.itemtype + '">🗑️ حذف</button>' +
                    '</td>' +
                '</tr>'
            );
        }).join('');
    }

    const shopsSidebarEl = document.getElementById('shopsSidebarList');
    if (shopsSidebarEl) {
        shopsSidebarEl.addEventListener('click', function (e) {
            const btn = e.target.closest('[data-select-shop]');
            if (!btn) return;
            selectedShopNpcId = parseInt(btn.getAttribute('data-select-shop'), 10);
            const npcInput = document.getElementById('shopFormNpcId');
            if (npcInput) npcInput.value = selectedShopNpcId;
            updateClientShopSnippet();
            renderShopsSidebar();
            renderSelectedShopGoods();
        });
    }

    const shopFilterEl = document.getElementById('shopFilterInput');
    if (shopFilterEl) shopFilterEl.addEventListener('input', renderShopsSidebar);

    const shopItemsFilterEl = document.getElementById('shopItemsFilterInput');
    if (shopItemsFilterEl) shopItemsFilterEl.addEventListener('input', renderSelectedShopGoods);

    const shopGoodsBodyEl = document.getElementById('selectedShopGoodsBody');
    if (shopGoodsBodyEl) {
        shopGoodsBodyEl.addEventListener('click', function (e) {
            const editBtn = e.target.closest('[data-edit-good]');
            if (editBtn) {
                try {
                    const it = JSON.parse(editBtn.getAttribute('data-edit-good'));
                    document.getElementById('shopFormNpcId').value = it.owner_id;
                    document.getElementById('shopFormItemType').value = it.itemtype;
                    document.getElementById('shopFormMoneyType').value = it.moneytype;
                    document.getElementById('shopFormCpPrice').value = it.cp_price;
                    document.getElementById('shopFormBoundCpPrice').value = it.bound_cp_price;
                    document.getElementById('shopFormSilverPrice').value = it.silver_price;
                    document.getElementById('shopFormHonorPrice').value = it.honor_price;
                    document.getElementById('shopFormRidingPrice').value = it.riding_price;
                    updateClientShopSnippet();
                    showToast('تم تحميل بيانات [' + it.item_name + '] في نموذج التعديل الفوري.');
                } catch (err) {}
                return;
            }

            const delBtn = e.target.closest('[data-del-good]');
            if (delBtn) {
                const goodsId = parseInt(delBtn.getAttribute('data-del-good'), 10);
                const npcId = parseInt(delBtn.getAttribute('data-del-npc'), 10);
                const itemType = parseInt(delBtn.getAttribute('data-del-item'), 10);
                sendStudioCommand({
                    type: 'shop_delete',
                    goods_id: goodsId,
                    npc_id: npcId,
                    item_type: itemType
                }).then(function () {
                    setTimeout(loadShopsSnapshot, 1200);
                });
            }
        });
    }

    document.querySelectorAll('[data-pick-item]').forEach(function (chip) {
        chip.addEventListener('click', function () {
            const id = chip.getAttribute('data-pick-item');
            const el = document.getElementById('shopFormItemType');
            if (el) el.value = id;
            updateClientShopSnippet();
        });
    });

    const btnShopUpsert = document.getElementById('btnShopUpsert');
    if (btnShopUpsert) {
        btnShopUpsert.addEventListener('click', function () {
            const npcId = parseInt(document.getElementById('shopFormNpcId').value || '0', 10);
            const itemType = parseInt(document.getElementById('shopFormItemType').value || '0', 10);
            const moneyType = parseInt(document.getElementById('shopFormMoneyType').value || '1', 10);
            if (!npcId || !itemType) {
                showToast('يرجى إدخال رقم المتجر (NPC ID) ورقم الأداة (ItemType).', true);
                return;
            }
            sendStudioCommand({
                type: 'shop_upsert',
                npc_id: npcId,
                item_type: itemType,
                money_type: moneyType,
                cp_price: document.getElementById('shopFormCpPrice').value,
                bound_cp_price: document.getElementById('shopFormBoundCpPrice').value,
                silver_price: document.getElementById('shopFormSilverPrice').value,
                honor_price: document.getElementById('shopFormHonorPrice').value,
                riding_price: document.getElementById('shopFormRidingPrice').value
            }).then(function () {
                setTimeout(loadShopsSnapshot, 1300);
            });
        });
    }

    const btnReloadShopsLive = document.getElementById('btnReloadShopsLive');
    if (btnReloadShopsLive) {
        btnReloadShopsLive.addEventListener('click', function () {
            sendStudioCommand({ type: 'shop_reload' }).then(function () {
                setTimeout(loadShopsSnapshot, 1300);
            });
        });
    }

    const btnRefreshShopsSnap = document.getElementById('btnRefreshShopsSnap');
    if (btnRefreshShopsSnap) {
        btnRefreshShopsSnap.addEventListener('click', function () {
            loadShopsSnapshot();
            showToast('تم تحديث قراءة ملف المتاجر المحلي.');
        });
    }

    async function fetchCatalogResults() {
        try {
            const resp = await fetch('./api.php?action=search_results', { cache: 'no-store' });
            if (!resp.ok) return;
            const data = await resp.json();
            if (!data || !data.ok || !data.search || !data.search.available) return;

            if (pendingCatalogTarget === 'shop') {
                const box = document.getElementById('shopSearchResultsBox');
                if (box) {
                    const items = data.search.items || [];
                    box.style.display = 'flex';
                    box.innerHTML = items.length
                        ? items.slice(0, 24).map(function (it) {
                            return '<button type="button" class="quick-chip" data-found-item="' + it.id + '" data-found-cp="' + it.cps + '" data-found-silver="' + it.silver + '">' + esc(it.name) + ' (#' + it.id + ' | CPs:' + it.cps + ')</button>';
                        }).join('')
                        : '<span class="control-hint">لا توجد أدوات مطابقة في cq_itemtype.</span>';
                }
            } else if (pendingCatalogTarget === 'boss') {
                const box = document.getElementById('bossSearchResultsBox');
                if (box) {
                    const mobs = data.search.monsters || [];
                    box.style.display = 'flex';
                    box.innerHTML = mobs.length
                        ? mobs.slice(0, 24).map(function (m) {
                            return '<button type="button" class="quick-chip" data-found-mob="' + m.id + '">' + esc(m.name) + ' (#' + m.id + ' | Lv.' + m.level + ' | HP:' + Number(m.life).toLocaleString('en-US') + ')</button>';
                        }).join('')
                        : '<span class="control-hint">لا توجد وحوش مطابقة في cq_monstertype.</span>';
                }
            }
        } catch (err) {}
    }

    const btnShopCatalogSearch = document.getElementById('btnShopCatalogSearch');
    if (btnShopCatalogSearch) {
        btnShopCatalogSearch.addEventListener('click', function () {
            const q = (document.getElementById('shopCatalogQuery').value || '').trim();
            pendingCatalogTarget = 'shop';
            sendStudioCommand({ type: 'catalog_search', query: q }, '🔍 جاري البحث في ذاكرة السيرفر...').then(function () {
                setTimeout(fetchCatalogResults, 1100);
            });
        });
    }

    const shopSearchBox = document.getElementById('shopSearchResultsBox');
    if (shopSearchBox) {
        shopSearchBox.addEventListener('click', function (e) {
            const chip = e.target.closest('[data-found-item]');
            if (!chip) return;
            document.getElementById('shopFormItemType').value = chip.getAttribute('data-found-item');
            document.getElementById('shopFormCpPrice').value = chip.getAttribute('data-found-cp');
            document.getElementById('shopFormSilverPrice').value = chip.getAttribute('data-found-silver');
            updateClientShopSnippet();
        });
    }

    // =========================================================================
    // TOOL #3: LIVE ONLINE PLAYERS RADAR & GIFT / ACTION BOX
    // =========================================================================
    function renderOnlinePlayers(players) {
        latestPlayers = Array.isArray(players) ? players : [];
        const tabCnt = document.getElementById('tabOnlineCount');
        if (tabCnt) tabCnt.textContent = String(latestPlayers.length);

        const tbody = document.getElementById('onlinePlayersTableBody');
        if (!tbody) return;

        const q = ((document.getElementById('playersSearchInput') || {}).value || '').trim().toLowerCase();
        const filtered = latestPlayers.filter(function (p) {
            if (!q) return true;
            return String(p.name || '').toLowerCase().includes(q)
                || String(p.map_name || '').toLowerCase().includes(q)
                || String(p.id || '').includes(q);
        });

        if (filtered.length === 0) {
            tbody.innerHTML = '<tr><td colspan="7" style="text-align:center; color:var(--text-muted);">لا يوجد لاعبون متصلون مطابقون حالياً.</td></tr>';
            return;
        }

        tbody.innerHTML = filtered.map(function (p) {
            const isSel = (selectedPlayerId && p.id === selectedPlayerId) || (selectedPlayerName && p.name === selectedPlayerName);
            const stateBadges = [];
            stateBadges.push(p.is_alive ? '🟢 حي' : '💀 شبح');
            if (p.is_jailed) stateBadges.push('⛓️ مسجون');
            if (p.pk >= 100) stateBadges.push('⚫ PK:' + p.pk);
            else if (p.pk >= 30) stateBadges.push('🔴 PK:' + p.pk);

            return (
                '<tr class="' + (isSel ? 'selected-row' : '') + '" style="cursor:pointer;" data-sel-pid="' + p.id + '" data-sel-pname="' + esc(p.name) + '">' +
                    '<td><strong>' + esc(p.name) + '</strong>' + (p.syndicate ? ' <small style="color:var(--gold);">[' + esc(p.syndicate) + ']</small>' : '') + '</td>' +
                    '<td>Lv.' + p.level + ' (' + p.reborn + 'RB)</td>' +
                    '<td><strong>' + p.bp + '</strong></td>' +
                    '<td>' + esc(p.map_name) + ' <small>(' + p.x + ',' + p.y + ')</small></td>' +
                    '<td>💎 ' + Number(p.cps || 0).toLocaleString('en-US') + ' | 🪙 ' + Number(p.silvers || 0).toLocaleString('en-US') + '</td>' +
                    '<td>VIP ' + p.vip + '</td>' +
                    '<td>' + stateBadges.join(' ') + ' <button type="button" class="btn btn-ghost btn-sm" data-quick-inspect="' + esc(p.name) + '" title="فحص العتاد والمخازن">🎒</button></td>' +
                '</tr>'
            );
        }).join('');
    }

    const playersSearchEl = document.getElementById('playersSearchInput');
    if (playersSearchEl) {
        playersSearchEl.addEventListener('input', function () {
            renderOnlinePlayers(latestPlayers);
        });
    }

    const onlineTbodyEl = document.getElementById('onlinePlayersTableBody');
    if (onlineTbodyEl) {
        onlineTbodyEl.addEventListener('click', function (e) {
            const inspBtn = e.target.closest('[data-quick-inspect]');
            if (inspBtn) {
                e.stopPropagation();
                openInspectorFor(inspBtn.getAttribute('data-quick-inspect'));
                return;
            }
            const row = e.target.closest('[data-sel-pid]');
            if (!row) return;
            selectedPlayerId = parseInt(row.getAttribute('data-sel-pid'), 10);
            selectedPlayerName = row.getAttribute('data-sel-pname') || '';
            const nameInput = document.getElementById('giftPlayerName');
            const idInput = document.getElementById('giftPlayerId');
            if (nameInput) nameInput.value = selectedPlayerName;
            if (idInput) idInput.value = String(selectedPlayerId);
            renderOnlinePlayers(latestPlayers);
            showToast('🎯 تم تحديد البطل: ' + selectedPlayerName);
        });
    }

    const giftNameInput = document.getElementById('giftPlayerName');
    if (giftNameInput) {
        giftNameInput.addEventListener('input', function () {
            selectedPlayerName = giftNameInput.value.trim();
            selectedPlayerId = 0;
            const idInput = document.getElementById('giftPlayerId');
            if (idInput) idInput.value = '0';
        });
    }

    function ensureTargetPlayerSelected() {
        const pname = ((document.getElementById('giftPlayerName') || {}).value || '').trim();
        const pid = parseInt(((document.getElementById('giftPlayerId') || {}).value || '0'), 10);
        if (!pid && !pname) {
            showToast('اختر لاعباً من الجدول أولاً أو اكتب اسمه في خانة اللاعب المستهدف.', true);
            return null;
        }
        return { player_id: pid, player_name: pname };
    }

    document.querySelectorAll('.js-player-act').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const target = ensureTargetPlayerSelected();
            if (!target) return;
            sendStudioCommand({
                type: 'player_action',
                player_id: target.player_id,
                player_name: target.player_name,
                sub_action: btn.getAttribute('data-act')
            });
        });
    });

    document.querySelectorAll('.js-player-tp').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const target = ensureTargetPlayerSelected();
            if (!target) return;
            sendStudioCommand({
                type: 'player_action',
                player_id: target.player_id,
                player_name: target.player_name,
                sub_action: 'teleport',
                map_id: parseInt(btn.getAttribute('data-map'), 10),
                x: parseInt(btn.getAttribute('data-x'), 10),
                y: parseInt(btn.getAttribute('data-y'), 10)
            });
        });
    });

    const btnCustomTeleport = document.getElementById('btnCustomTeleport');
    if (btnCustomTeleport) {
        btnCustomTeleport.addEventListener('click', function () {
            const target = ensureTargetPlayerSelected();
            if (!target) return;
            const mapId = parseInt(document.getElementById('tpCustomMap').value || '1002', 10);
            const x = parseInt(document.getElementById('tpCustomX').value || '300', 10);
            const y = parseInt(document.getElementById('tpCustomY').value || '278', 10);
            sendStudioCommand({
                type: 'player_action',
                player_id: target.player_id,
                player_name: target.player_name,
                sub_action: 'teleport',
                map_id: mapId,
                x: x,
                y: y
            });
        });
    }

    document.querySelectorAll('[data-gift-item]').forEach(function (chip) {
        chip.addEventListener('click', function () {
            document.getElementById('giftItemType').value = chip.getAttribute('data-gift-item');
        });
    });

    function collectGiftPayload(commandType) {
        return {
            type: commandType,
            player_id: parseInt((document.getElementById('giftPlayerId') || {}).value || '0', 10),
            player_name: ((document.getElementById('giftPlayerName') || {}).value || '').trim(),
            vip_level: parseInt((document.getElementById('giftVipLevel') || {}).value || '-1', 10),
            cp_amount: parseInt((document.getElementById('giftCpAmount') || {}).value || '0', 10),
            bound_cp_amount: parseInt((document.getElementById('giftBoundCpAmount') || {}).value || '0', 10),
            silver_amount: parseInt((document.getElementById('giftSilverAmount') || {}).value || '0', 10),
            blessing_hours: parseInt((document.getElementById('giftBlessingHours') || {}).value || '0', 10),
            item_type: parseInt((document.getElementById('giftItemType') || {}).value || '0', 10),
            item_amount: parseInt((document.getElementById('giftItemAmount') || {}).value || '1', 10),
            plus: parseInt((document.getElementById('giftItemPlus') || {}).value || '0', 10),
            bless: parseInt((document.getElementById('giftItemBless') || {}).value || '0', 10),
            enchant: parseInt((document.getElementById('giftItemEnchant') || {}).value || '0', 10),
            socket_one: parseInt((document.getElementById('giftSocketOne') || {}).value || '0', 10),
            socket_two: parseInt((document.getElementById('giftSocketTwo') || {}).value || '0', 10),
            bound: !!(document.getElementById('giftItemBound') && document.getElementById('giftItemBound').checked),
            message: ((document.getElementById('giftMessage') || {}).value || '').trim(),
            broadcast: true
        };
    }

    const btnSendGiftSingle = document.getElementById('btnSendGiftSingle');
    if (btnSendGiftSingle) {
        btnSendGiftSingle.addEventListener('click', function () {
            if (!ensureTargetPlayerSelected()) return;
            sendStudioCommand(collectGiftPayload('player_reward'));
        });
    }

    const btnSendGiftAllOnline = document.getElementById('btnSendGiftAllOnline');
    if (btnSendGiftAllOnline) {
        btnSendGiftAllOnline.addEventListener('click', function () {
            sendStudioCommand(collectGiftPayload('reward_all_online'));
        });
    }

    // =========================================================================
    // TOOL #4: LIVE BOSS & INVASION SPAWNER STUDIO
    // =========================================================================
    document.querySelectorAll('[data-boss-id]').forEach(function (chip) {
        chip.addEventListener('click', function () {
            document.getElementById('bossMonsterType').value = chip.getAttribute('data-boss-id');
        });
    });

    const bossMapChips = document.getElementById('bossMapChips');
    if (bossMapChips) {
        bossMapChips.addEventListener('click', function (e) {
            const chip = e.target.closest('[data-map]');
            if (!chip) return;
            document.getElementById('bossMapId').value = chip.getAttribute('data-map');
            document.getElementById('bossCoordX').value = chip.getAttribute('data-x');
            document.getElementById('bossCoordY').value = chip.getAttribute('data-y');
        });
    }

    const btnBossCatalogSearch = document.getElementById('btnBossCatalogSearch');
    if (btnBossCatalogSearch) {
        btnBossCatalogSearch.addEventListener('click', function () {
            const q = (document.getElementById('bossCatalogQuery').value || '').trim();
            pendingCatalogTarget = 'boss';
            sendStudioCommand({ type: 'catalog_search', query: q }, '🔍 جاري البحث في جدول الوحوش والزعماء...').then(function () {
                setTimeout(fetchCatalogResults, 1100);
            });
        });
    }

    const bossSearchBox = document.getElementById('bossSearchResultsBox');
    if (bossSearchBox) {
        bossSearchBox.addEventListener('click', function (e) {
            const chip = e.target.closest('[data-found-mob]');
            if (!chip) return;
            document.getElementById('bossMonsterType').value = chip.getAttribute('data-found-mob');
        });
    }

    const btnSpawnBossNow = document.getElementById('btnSpawnBossNow');
    if (btnSpawnBossNow) {
        btnSpawnBossNow.addEventListener('click', function () {
            sendStudioCommand({
                type: 'boss_spawn',
                monster_type: parseInt(document.getElementById('bossMonsterType').value || '20070', 10),
                count: parseInt(document.getElementById('bossSpawnCount').value || '1', 10),
                scatter_radius: parseInt(document.getElementById('bossScatterRadius').value || '4', 10),
                map_id: parseInt(document.getElementById('bossMapId').value || '1002', 10),
                x: parseInt(document.getElementById('bossCoordX').value || '300', 10),
                y: parseInt(document.getElementById('bossCoordY').value || '278', 10),
                message: (document.getElementById('bossAnnounceMsg').value || '').trim(),
                broadcast: !!(document.getElementById('bossBroadcastToggle') && document.getElementById('bossBroadcastToggle').checked)
            });
        });
    }

    const btnClearAllBosses = document.getElementById('btnClearAllBosses');
    if (btnClearAllBosses) {
        btnClearAllBosses.addEventListener('click', function () {
            sendStudioCommand({ type: 'boss_clear', role_id: 0, map_id: 0 });
        });
    }

    function renderSpawnedBosses(bosses) {
        latestBosses = Array.isArray(bosses) ? bosses : [];
        const tabCnt = document.getElementById('tabBossCount');
        if (tabCnt) tabCnt.textContent = String(latestBosses.length);

        const tbody = document.getElementById('spawnedBossesTableBody');
        if (!tbody) return;

        if (latestBosses.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-muted);">لا يوجد زعماء غزوات مستدعون حالياً.</td></tr>';
            return;
        }

        tbody.innerHTML = latestBosses.map(function (b) {
            const maxHp = Math.max(1, Number(b.max_life || 1));
            const curHp = Math.max(0, Number(b.life || 0));
            const pct = Math.min(100, Math.round((curHp * 100) / maxHp));
            return (
                '<tr>' +
                    '<td><strong>' + esc(b.name) + '</strong> <small>(#' + b.monster_type + ')</small></td>' +
                    '<td>Lv.' + b.level + '</td>' +
                    '<td>' +
                        '<div style="font-size:0.75rem;">' + curHp.toLocaleString('en-US') + ' / ' + maxHp.toLocaleString('en-US') + ' (' + pct + '%)</div>' +
                        '<div class="hp-bar-track"><div class="hp-bar-fill" style="width:' + pct + '%;"></div></div>' +
                    '</td>' +
                    '<td>' + esc(b.map_name) + ' <small>(' + b.x + ',' + b.y + ')</small></td>' +
                    '<td><button type="button" class="btn btn-crimson btn-sm" data-despawn-role="' + b.role_id + '">مسح</button></td>' +
                '</tr>'
            );
        }).join('');
    }

    const spawnedBossesBody = document.getElementById('spawnedBossesTableBody');
    if (spawnedBossesBody) {
        spawnedBossesBody.addEventListener('click', function (e) {
            const btn = e.target.closest('[data-despawn-role]');
            if (!btn) return;
            sendStudioCommand({
                type: 'boss_clear',
                role_id: parseInt(btn.getAttribute('data-despawn-role'), 10),
                map_id: 0
            });
        });
    }

    // =========================================================================
    // LIVE TELEMETRY & EXECUTION FEED RENDERER
    // =========================================================================
    function renderExecutionFeed(results) {
        const feedEl = document.getElementById('execFeedList');
        if (!Array.isArray(results) || results.length === 0) return;

        results.forEach(function (r) {
            if (!r || !r.id) return;
            if (pendingCommandIds.has(r.id)) {
                pendingCommandIds.delete(r.id);
                showToast((r.ok ? '✅ نفّذ السيرفر: ' : '⚠️ رد السيرفر: ') + r.message, !r.ok);
            }
            seenResultIds.add(r.id);
        });

        if (feedEl) {
            feedEl.innerHTML = results.slice(0, 4).map(function (r) {
                return '<span class="exec-chip ' + (r.ok ? '' : 'err') + '">[' + esc(r.executed_at) + '] ' + esc(r.message) + '</span>';
            }).join('');
        }
    }

    function renderStatus(st) {
        const pill = document.getElementById('syncPill');
        const pillText = document.getElementById('syncPillText');
        if (pill && pillText) {
            pill.classList.toggle('online', !!st.live_connected);
            pill.classList.toggle('standby', !st.live_connected);
            pillText.textContent = st.live_connected
                ? 'متصل بالسيرفر حياً (< 1 ثانية)'
                : 'وضع التجهيز المسبق (السيرفر متوقف حالياً)';
        }

        const elOnline = document.getElementById('statOnlinePlayers');
        if (elOnline) elOnline.textContent = String(st.online_players || 0);

        const elMax = document.getElementById('statMaxOnline');
        if (elMax) elMax.textContent = 'أعلى ذروة: ' + String(st.max_online || 0) + ' لاعب';

        const elReload = document.getElementById('statLastReload');
        if (elReload) {
            const newStamp = st.last_reload_at || '—';
            if (awaitingServerReload && newStamp !== baselineReloadStamp && st.live_connected) {
                awaitingServerReload = false;
                showToast('✅ تم التأكيد الفعلي! استلم MoonQueen.Game الريتات الجديدة في: ' + newStamp);
            }
            elReload.textContent = newStamp;
        }

        const elActivePreset = document.getElementById('statActivePreset');
        if (elActivePreset && st.active_rates && st.active_rates.PresetName) {
            elActivePreset.textContent = st.active_rates.PresetName;
        }

        const elPath = document.getElementById('statTargetPath');
        if (elPath && st.rates_path) {
            elPath.textContent = st.rates_path;
        }

        const elPromoCount = document.getElementById('tabPromoCount');
        if (elPromoCount && typeof st.promo_codes_count !== 'undefined') {
            elPromoCount.textContent = String(st.promo_codes_count || 0);
        }

        if (st.events) renderEventsGrid(st.events);
        if (st.online_players_list) renderOnlinePlayers(st.online_players_list);
        if (st.spawned_bosses) renderSpawnedBosses(st.spawned_bosses);
        if (st.recent_results) renderExecutionFeed(st.recent_results);
    }

    // =========================================================================
    // TOOL #5: ACCOUNTS, BAN, UNSTUCK & AUTHORITY STUDIO
    // =========================================================================
    async function loadAccountsList() {
        const tbody = document.getElementById('accountsTableBody');
        if (!tbody) return;
        const q = ((document.getElementById('accSearchInput') || {}).value || '').trim();
        const filter = ((document.getElementById('accFilterSelect') || {}).value || 'all');
        try {
            const resp = await fetch('./api.php?action=accounts_list&q=' + encodeURIComponent(q) + '&filter=' + encodeURIComponent(filter), { cache: 'no-store' });
            const data = await resp.json();
            if (!data.ok) {
                tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:#fb7185;">' + esc(data.error || 'تعذر قراءة قاعدة الحسابات') + '</td></tr>';
                return;
            }
            renderMultiClientAlert(data.multi_ip || [], data.multi_mac || []);
            const list = data.accounts || [];
            if (list.length === 0) {
                tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-muted);">لا توجد حسابات مطابقة حالياً.</td></tr>';
                return;
            }
            tbody.innerHTML = list.map(function (a) {
                const isBanned = a.flag >= 2;
                const flagCls = isBanned ? 'err' : 'ok';
                const authCls = a.authority_id >= 7 ? 'gold' : 'ok';
                const charHtml = a.char_id
                    ? ('<strong>' + esc(a.char_name) + '</strong> <small>(Lv.' + a.level + '/' + a.reborn + 'RB)</small><br>' +
                       (a.online ? '<span class="badge-pill ok">🟢 متصل الآن</span> ' : '<span class="control-hint">أوفلاين</span> ') +
                       '<small>Map:' + a.map_id + ' (' + a.x + ',' + a.y + ')</small>')
                    : '<span class="control-hint">لم ينشئ شخصية بعد</span>';

                const banBtn = isBanned
                    ? '<button type="button" class="btn btn-ghost btn-sm" data-acc-flag="1" data-acc-id="' + a.account_id + '" data-char-id="' + a.char_id + '">🔓 فك الحظر</button>'
                    : '<button type="button" class="btn btn-crimson btn-sm" data-acc-flag="3" data-acc-id="' + a.account_id + '" data-char-id="' + a.char_id + '">⛔ حظر وطرد</button>';

                const unstuckBtn = a.char_id
                    ? '<button type="button" class="btn btn-ghost btn-sm" data-acc-unstuck="' + a.char_id + '" title="نقل لتوين سيتي">🧭 فك تعليق</button>'
                    : '';

                const inspBtn = a.char_id
                    ? '<button type="button" class="btn btn-ghost btn-sm" data-quick-inspect="' + a.char_id + '" title="فحص الشنطة والمخزن">🎒 فحص</button>'
                    : '';

                return (
                    '<tr>' +
                        '<td><strong>' + esc(a.username) + '</strong><br><small style="color:var(--text-muted);">#' + a.account_id + '</small></td>' +
                        '<td>' + charHtml + '</td>' +
                        '<td>' +
                            '<span class="badge-pill ' + flagCls + '">' + esc(a.flag_label) + '</span> ' +
                            '<span class="badge-pill ' + authCls + '">' + esc(a.authority_label) + '</span>' +
                        '</td>' +
                        '<td style="direction:ltr; font-size:0.78rem;">IP: <code>' + esc(a.ip || '—') + '</code><br>MAC: <code>' + esc(a.mac || '—') + '</code></td>' +
                        '<td style="display:flex; gap:5px; flex-wrap:wrap;">' +
                            banBtn +
                            unstuckBtn +
                            inspBtn +
                            '<button type="button" class="btn btn-ghost btn-sm" data-acc-pwd="' + a.account_id + '" data-acc-user="' + esc(a.username) + '">🔑 باسورد</button>' +
                            '<button type="button" class="btn btn-ghost btn-sm" data-acc-auth="' + a.account_id + '" data-cur-auth="' + a.authority_id + '" data-char-id="' + a.char_id + '">👑 رتبة</button>' +
                        '</td>' +
                    '</tr>'
                );
            }).join('');
        } catch (err) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:#fb7185;">تعذر الاتصال بواجهة الحسابات.</td></tr>';
        }
    }

    function renderMultiClientAlert(multiIp, multiMac) {
        const box = document.getElementById('multiClientAlertBox');
        if (!box) return;
        const notes = [];
        multiIp.forEach(function (g) {
            notes.push('🌐 IP مشترك <code>' + esc(g.ip) + '</code> فاتح (' + g.count + ') شخصيات الآن: <strong>' + g.names.map(esc).join(' ، ') + '</strong>');
        });
        multiMac.forEach(function (g) {
            notes.push('💻 جهاز MAC مشترك <code>' + esc(g.mac) + '</code> فاتح (' + g.count + ') شخصيات الآن: <strong>' + g.names.map(esc).join(' ، ') + '</strong>');
        });
        if (notes.length === 0) {
            box.style.display = 'none';
            box.innerHTML = '';
        } else {
            box.style.display = 'block';
            box.innerHTML = '<strong>🕵️ رادار تعدد الحسابات الحية (Multi-Client Detector):</strong><br>' + notes.join('<br>');
        }
    }

    const btnSearchAccounts = document.getElementById('btnSearchAccounts');
    if (btnSearchAccounts) btnSearchAccounts.addEventListener('click', loadAccountsList);
    const btnRefreshAccounts = document.getElementById('btnRefreshAccounts');
    if (btnRefreshAccounts) btnRefreshAccounts.addEventListener('click', loadAccountsList);
    const accFilterSelect = document.getElementById('accFilterSelect');
    if (accFilterSelect) accFilterSelect.addEventListener('change', loadAccountsList);
    const accSearchInput = document.getElementById('accSearchInput');
    if (accSearchInput) {
        accSearchInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') loadAccountsList();
        });
    }

    const btnCreateAccount = document.getElementById('btnCreateAccount');
    if (btnCreateAccount) {
        btnCreateAccount.addEventListener('click', async function () {
            const username = ((document.getElementById('accNewUsername') || {}).value || '').trim();
            const password = ((document.getElementById('accNewPassword') || {}).value || '');
            const authorityId = parseInt(((document.getElementById('accNewAuthority') || {}).value || '1'), 10);
            const res = await apiPost('account_create', { username: username, password: password, authority_id: authorityId });
            if (res && res.ok) {
                showToast('✅ ' + res.message);
                document.getElementById('accNewUsername').value = '';
                document.getElementById('accNewPassword').value = '';
                loadAccountsList();
            } else {
                showToast('⚠️ ' + ((res && res.error) || 'تعذر إنشاء الحساب'), true);
            }
        });
    }

    const btnBanNetwork = document.getElementById('btnBanNetwork');
    if (btnBanNetwork) {
        btnBanNetwork.addEventListener('click', async function () {
            const ip = ((document.getElementById('banNetworkIp') || {}).value || '').trim();
            const mac = ((document.getElementById('banNetworkMac') || {}).value || '').trim();
            if (!ip && !mac) {
                showToast('اكتب عنوان الـ IP أو الـ MAC أولاً.', true);
                return;
            }
            if (!window.confirm('هل أنت متأكد من حظر وطرد جميع حسابات هذه الشبكة/الجهاز؟')) return;
            const res = await apiPost('account_ban_network', { ip: ip, mac: mac });
            if (res && res.ok) {
                showToast('⛔ ' + res.message);
                loadAccountsList();
            } else {
                showToast('⚠️ ' + ((res && res.error) || 'فشل الحظر'), true);
            }
        });
    }

    const accountsTableBody = document.getElementById('accountsTableBody');
    if (accountsTableBody) {
        accountsTableBody.addEventListener('click', async function (e) {
            const insp = e.target.closest('[data-quick-inspect]');
            if (insp) {
                openInspectorFor(insp.getAttribute('data-quick-inspect'));
                return;
            }

            const flagBtn = e.target.closest('[data-acc-flag]');
            if (flagBtn) {
                const accountId = parseInt(flagBtn.getAttribute('data-acc-id'), 10);
                const flag = parseInt(flagBtn.getAttribute('data-acc-flag'), 10);
                const charId = parseInt(flagBtn.getAttribute('data-char-id') || '0', 10);
                const res = await apiPost('account_set_flag', { account_id: accountId, flag: flag, char_id: charId });
                if (res && res.ok) {
                    showToast('✅ ' + res.message);
                    loadAccountsList();
                } else {
                    showToast('⚠️ ' + ((res && res.error) || 'فشل تحديث حالة الحساب'), true);
                }
                return;
            }

            const unstuckBtn = e.target.closest('[data-acc-unstuck]');
            if (unstuckBtn) {
                const charId = parseInt(unstuckBtn.getAttribute('data-acc-unstuck'), 10);
                const res = await apiPost('account_unstuck', { char_id: charId, map_id: 1002, x: 430, y: 378 });
                if (res && res.ok) {
                    showToast('🧭 ' + res.message);
                    loadAccountsList();
                } else {
                    showToast('⚠️ ' + ((res && res.error) || 'فشل فك التعليق'), true);
                }
                return;
            }

            const pwdBtn = e.target.closest('[data-acc-pwd]');
            if (pwdBtn) {
                const accountId = parseInt(pwdBtn.getAttribute('data-acc-pwd'), 10);
                const uname = pwdBtn.getAttribute('data-acc-user') || '';
                const newPassword = window.prompt('اكتب كلمة المرور الجديدة للحساب [' + uname + '] (4 إلى 16 حرف/رقم):');
                if (!newPassword) return;
                const res = await apiPost('account_reset_password', { account_id: accountId, new_password: newPassword });
                if (res && res.ok) {
                    showToast('🔑 ' + res.message);
                } else {
                    showToast('⚠️ ' + ((res && res.error) || 'فشل تغيير كلمة المرور'), true);
                }
                return;
            }

            const authBtn = e.target.closest('[data-acc-auth]');
            if (authBtn) {
                const accountId = parseInt(authBtn.getAttribute('data-acc-auth'), 10);
                const curAuth = authBtn.getAttribute('data-cur-auth') || '1';
                const charId = parseInt(authBtn.getAttribute('data-char-id') || '0', 10);
                const val = window.prompt('اختر رقم الصلاحية الجديدة:\n1 = لاعب عادي (Player)\n2 = متعاون (Cooperator)\n7 = مشرف (GM)\n8 = مدير كامل (PM)', curAuth);
                if (!val) return;
                const res = await apiPost('account_set_authority', { account_id: accountId, authority_id: parseInt(val, 10), char_id: charId });
                if (res && res.ok) {
                    showToast('👑 ' + res.message);
                    loadAccountsList();
                } else {
                    showToast('⚠️ ' + ((res && res.error) || 'فشل تعديل الرتبة'), true);
                }
            }
        });
    }

    // =========================================================================
    // TOOL #6: PROMO & GIFT CODES STUDIO (/redeem)
    // =========================================================================
    async function loadPromosList() {
        const tbody = document.getElementById('promosTableBody');
        if (!tbody) return;
        try {
            const resp = await fetch('./api.php?action=promos_list', { cache: 'no-store' });
            const data = await resp.json();
            const codes = (data.promos && data.promos.Codes) ? data.promos.Codes : [];
            const cntEl = document.getElementById('tabPromoCount');
            if (cntEl) {
                cntEl.textContent = String(codes.filter(function (c) { return !!c.Enabled; }).length);
            }
            if (codes.length === 0) {
                tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:var(--text-muted);">لا توجد أكواد مسجلة حالياً — أنشئ أول كود هدية من النموذج!</td></tr>';
                return;
            }
            tbody.innerHTML = codes.map(function (c) {
                const rewards = [];
                if (c.CpAmount > 0) rewards.push('💎 ' + Number(c.CpAmount).toLocaleString('en-US') + ' CPs');
                if (c.BoundCpAmount > 0) rewards.push('🔒 ' + Number(c.BoundCpAmount).toLocaleString('en-US') + ' Bound');
                if (c.SilverAmount > 0) rewards.push('🪙 ' + Number(c.SilverAmount).toLocaleString('en-US') + ' Silver');
                if (c.BlessingHours > 0) rewards.push('✨ بركة ' + c.BlessingHours + ' ساعة');
                if (c.VipLevel >= 0) rewards.push('👑 VIP ' + c.VipLevel);
                if (c.ItemType > 0) {
                    rewards.push('🗡️ Item #' + c.ItemType + ' x' + (c.ItemAmount || 1) + (c.Plus > 0 ? ' (+' + c.Plus + ')' : ''));
                }
                const usesTxt = (c.UsedCount || 0) + ' / ' + (c.MaxUses > 0 ? c.MaxUses : '∞');
                const claims = (c.RecentClaims || []).slice(0, 3).map(function (cl) {
                    if (typeof cl === 'string') return esc(cl);
                    return esc((cl.PlayerName || ('#' + cl.PlayerId)) + ' (' + (cl.ClaimedAt || '') + ')');
                }).join('<br>') || '<span class="control-hint">لا أحد بعد</span>';
                const statusBadge = c.Enabled
                    ? '<span class="badge-pill ok">مفعّل</span>'
                    : '<span class="badge-pill warn">موقوف</span>';

                return (
                    '<tr>' +
                        '<td><strong style="color:var(--gold); direction:ltr; display:inline-block;">' + esc(c.Code) + '</strong><br><small>' + esc(c.Title || '') + '</small></td>' +
                        '<td style="font-size:0.80rem;">' + (rewards.join(' + ') || '—') + '</td>' +
                        '<td><strong>' + usesTxt + '</strong><br><small>Lv.' + (c.MinLevel || 1) + '+</small></td>' +
                        '<td>' + statusBadge + '<br><small style="direction:ltr; display:inline-block;">' + esc(c.ExpiresAt || 'دائم') + '</small></td>' +
                        '<td style="font-size:0.75rem;">' + claims + '</td>' +
                        '<td style="display:flex; gap:5px; flex-wrap:wrap;">' +
                            '<button type="button" class="btn btn-ghost btn-sm" data-promo-copy="' + esc(c.Code) + '">📋 نسخ الأمر</button>' +
                            '<button type="button" class="btn btn-ghost btn-sm" data-promo-op="toggle" data-promo-code="' + esc(c.Code) + '">' + (c.Enabled ? '⏸️ إيقاف' : '▶️ تفعيل') + '</button>' +
                            '<button type="button" class="btn btn-crimson btn-sm" data-promo-op="delete" data-promo-code="' + esc(c.Code) + '">🗑️</button>' +
                        '</td>' +
                    '</tr>'
                );
            }).join('');
        } catch (err) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:#fb7185;">تعذر تحميل الأكواد.</td></tr>';
        }
    }

    document.querySelectorAll('[data-promo-pick]').forEach(function (chip) {
        chip.addEventListener('click', function () {
            const idEl = document.getElementById('promoItemType');
            if (idEl) idEl.value = chip.getAttribute('data-promo-pick');
            showToast('تم اختيار أداة الكود: ' + chip.getAttribute('data-promo-pick-name'));
        });
    });

    const btnRefreshPromos = document.getElementById('btnRefreshPromos');
    if (btnRefreshPromos) btnRefreshPromos.addEventListener('click', loadPromosList);

    const btnSavePromo = document.getElementById('btnSavePromo');
    if (btnSavePromo) {
        btnSavePromo.addEventListener('click', async function () {
            const socketsCnt = parseInt(((document.getElementById('promoItemSockets') || {}).value || '0'), 10);
            const payload = {
                code: ((document.getElementById('promoCode') || {}).value || '').trim().toUpperCase(),
                title: ((document.getElementById('promoTitle') || {}).value || '').trim(),
                enabled: true,
                max_uses: parseInt(((document.getElementById('promoMaxUses') || {}).value || '0'), 10),
                min_level: parseInt(((document.getElementById('promoMinLevel') || {}).value || '1'), 10),
                expires_hours: parseInt(((document.getElementById('promoValidHours') || {}).value || '0'), 10),
                blessing_hours: parseInt(((document.getElementById('promoVipDays') || {}).value || '0'), 10),
                cp_amount: parseInt(((document.getElementById('promoCps') || {}).value || '0'), 10),
                bound_cp_amount: parseInt(((document.getElementById('promoBoundCps') || {}).value || '0'), 10),
                silver_amount: parseInt(((document.getElementById('promoSilver') || {}).value || '0'), 10),
                item_type: parseInt(((document.getElementById('promoItemType') || {}).value || '0'), 10),
                item_amount: parseInt(((document.getElementById('promoItemCount') || {}).value || '1'), 10),
                plus: parseInt(((document.getElementById('promoItemPlus') || {}).value || '0'), 10),
                bless: parseInt(((document.getElementById('promoItemBless') || {}).value || '0'), 10),
                socket_one: socketsCnt >= 1 ? 255 : 0,
                socket_two: socketsCnt >= 2 ? 255 : 0,
                bound: !!((document.getElementById('promoItemBound') || {}).checked),
                one_per_ip_mac: !!((document.getElementById('promoOnePerDevice') || {}).checked)
            };
            const res = await apiPost('promo_save', payload);
            if (res && res.ok) {
                showToast('🎟️ ' + res.message);
                loadPromosList();
            } else {
                showToast('⚠️ ' + ((res && res.error) || 'تعذر حفظ الكود'), true);
            }
        });
    }

    const promosTableBody = document.getElementById('promosTableBody');
    if (promosTableBody) {
        promosTableBody.addEventListener('click', async function (e) {
            const copyBtn = e.target.closest('[data-promo-copy]');
            if (copyBtn) {
                const cmd = '/redeem ' + copyBtn.getAttribute('data-promo-copy');
                if (navigator.clipboard) navigator.clipboard.writeText(cmd);
                showToast('📋 تم نسخ الأمر: ' + cmd);
                return;
            }
            const opBtn = e.target.closest('[data-promo-op]');
            if (opBtn) {
                const code = opBtn.getAttribute('data-promo-code');
                const op = opBtn.getAttribute('data-promo-op');
                if (op === 'delete' && !window.confirm('حذف الكود [' + code + '] نهائياً؟')) return;
                const res = await apiPost('promo_action', { code: code, op: op });
                if (res && res.ok) {
                    showToast('✅ ' + res.message);
                    loadPromosList();
                }
            }
        });
    }

    // =========================================================================
    // TOOL #7: LIVE INVENTORY, EQUIPMENT & WAREHOUSE INSPECTOR
    // =========================================================================
    let inspectedPlayer = null;
    let inspectedItems = [];
    let activeInvFilterTab = 'all';

    function openInspectorFor(queryStr) {
        if (!queryStr) return;
        switchStudioTab('paneInspectorStudio');
        const inp = document.getElementById('inspectQueryInput');
        if (inp) inp.value = String(queryStr);
        runCharacterInspect(String(queryStr));
    }

    async function runCharacterInspect(queryOverride) {
        const q = (typeof queryOverride === 'string' ? queryOverride : ((document.getElementById('inspectQueryInput') || {}).value || '')).trim();
        const cont = document.getElementById('inspectGearContainer');
        const sumEl = document.getElementById('inspectPlayerSummary');
        const filterBar = document.getElementById('inspectFilterBar');
        if (!q || !cont) {
            showToast('اكتب اسم الشخصية أو الـ ID أولاً.', true);
            return;
        }
        cont.innerHTML = '<div style="text-align:center; padding:28px; color:var(--gold);">⏳ جاري فحص العتاد والحقيبة والمخازن...</div>';
        try {
            const resp = await fetch('./api.php?action=inspect_character&q=' + encodeURIComponent(q), { cache: 'no-store' });
            const data = await resp.json();
            if (!data.ok || !data.player) {
                if (sumEl) sumEl.style.display = 'none';
                if (filterBar) filterBar.style.display = 'none';
                cont.innerHTML = '<div style="text-align:center; padding:28px; color:#fb7185;">⚠️ ' + esc(data.error || 'لم يتم العثور على الشخصية') + '</div>';
                return;
            }
            inspectedPlayer = data.player;
            inspectedItems = []
                .concat(data.equipped || [])
                .concat(data.inventory || [])
                .concat(data.warehouses || []);

            if (sumEl) {
                const srcBadge = data.online
                    ? '<span class="badge-pill ok">🟢 ' + esc(data.source || 'متصل الآن') + '</span>'
                    : '<span class="badge-pill gold">🗄️ ' + esc(data.source || 'قاعدة البيانات') + '</span>';
                sumEl.style.display = 'block';
                sumEl.innerHTML =
                    '<div class="section-card" style="margin-bottom:0; padding:14px 18px; display:flex; justify-content:space-between; flex-wrap:wrap; gap:12px; align-items:center;">' +
                        '<div>' +
                            '<strong style="font-size:1.12rem; color:var(--gold);">' + esc(inspectedPlayer.name) + '</strong> ' +
                            '<small style="color:var(--text-muted);">(#' + inspectedPlayer.id + ' | Acc #' + inspectedPlayer.account_id + ')</small> ' +
                            srcBadge + '<br>' +
                            '<span style="font-size:0.84rem; color:#cbd5e1;">Lv.' + inspectedPlayer.level + ' (' + inspectedPlayer.reborn + ' RB) | BP: ' + (inspectedPlayer.bp || 0) + ' | PK: ' + inspectedPlayer.pk + '</span>' +
                        '</div>' +
                        '<div style="display:flex; gap:14px; flex-wrap:wrap; font-size:0.88rem;">' +
                            '<span>💎 CPs: <strong>' + Number(inspectedPlayer.cps || 0).toLocaleString('en-US') + '</strong></span>' +
                            '<span>🔒 Bound: <strong>' + Number(inspectedPlayer.bound_cps || 0).toLocaleString('en-US') + '</strong></span>' +
                            '<span>🪙 حقيبة: <strong>' + Number(inspectedPlayer.silvers || 0).toLocaleString('en-US') + '</strong></span>' +
                            '<span>🏦 خزنة: <strong>' + Number(inspectedPlayer.storage_money || 0).toLocaleString('en-US') + '</strong></span>' +
                        '</div>' +
                    '</div>';
            }

            if (filterBar) filterBar.style.display = 'flex';
            renderInspectedItems();
        } catch (err) {
            cont.innerHTML = '<div style="text-align:center; padding:28px; color:#fb7185;">تعذر الاتصال بمفتش العتاد.</div>';
        }
    }

    function classifyItemPos(pos) {
        if ((pos >= 1 && pos <= 32)) return 'equip';
        if (pos === 0) return 'bag';
        return 'warehouse';
    }

    function renderInspectedItems() {
        const cont = document.getElementById('inspectGearContainer');
        if (!cont) return;
        const txtFilter = (((document.getElementById('inspectItemFilter') || {}).value || '')).trim().toLowerCase();

        let eqCnt = 0, bagCnt = 0, whCnt = 0;
        inspectedItems.forEach(function (it) {
            const c = classifyItemPos(it.position);
            if (c === 'equip') eqCnt++;
            else if (c === 'bag') bagCnt++;
            else whCnt++;
        });
        const setTxt = function (id, v) { const el = document.getElementById(id); if (el) el.textContent = String(v); };
        setTxt('invCntAll', inspectedItems.length);
        setTxt('invCntEquip', eqCnt);
        setTxt('invCntBag', bagCnt);
        setTxt('invCntWh', whCnt);

        const filtered = inspectedItems.filter(function (it) {
            const cat = classifyItemPos(it.position);
            if (activeInvFilterTab !== 'all' && cat !== activeInvFilterTab) return false;
            if (!txtFilter) return true;
            const hay = (it.name + ' ' + it.type + ' +' + it.plus + ' ' + it.quality + ' ' + it.slot_label).toLowerCase();
            return hay.indexOf(txtFilter) !== -1;
        });

        if (filtered.length === 0) {
            cont.innerHTML = '<div style="text-align:center; padding:26px; color:var(--text-muted);">لا توجد أدوات في هذا القسم حالياً.</div>';
            return;
        }

        cont.innerHTML = '<div class="gear-grid">' + filtered.map(function (it) {
            const plusTxt = it.plus > 0 ? ' <span style="color:#fbbf24;">(+' + it.plus + ')</span>' : '';
            const attrs = [];
            attrs.push('<span class="gear-attr-chip">' + esc(it.quality) + '</span>');
            const socCnt = (it.gem1 > 0 ? 1 : 0) + (it.gem2 > 0 ? 1 : 0);
            if (socCnt > 0) {
                attrs.push('<span class="gear-attr-chip">🕳️ ' + socCnt + ' Soc (' + esc(it.gem1_label || '') + (socCnt > 1 ? ' / ' + esc(it.gem2_label || '') : '') + ')</span>');
            }
            if (it.bless > 0) attrs.push('<span class="gear-attr-chip">🛡️ -' + it.bless + '%</span>');
            if (it.enchant > 0) attrs.push('<span class="gear-attr-chip">❤️ +' + it.enchant + ' HP</span>');
            if (it.stack > 1) attrs.push('<span class="gear-attr-chip">x' + it.stack + '</span>');
            if (it.bound) attrs.push('<span class="gear-attr-chip">🔒 Bound</span>');

            return (
                '<div class="gear-card">' +
                    '<div class="gear-slot-title">' +
                        '<span>' + esc(it.slot_label) + '</span>' +
                        '<code>#' + it.id + '</code>' +
                    '</div>' +
                    '<div class="gear-item-title"><span class="q-' + esc(it.quality) + '">' + esc(it.name) + '</span>' + plusTxt + '</div>' +
                    '<div class="gear-attrs">' + attrs.join('') + '</div>' +
                    '<div style="display:flex; justify-content:space-between; align-items:center; margin-top:6px;">' +
                        '<small style="color:var(--text-muted);">Type: ' + it.type + '</small>' +
                        '<button type="button" class="btn btn-crimson btn-sm" data-del-inv-item="' + it.id + '" data-del-inv-name="' + esc(it.name) + '">🗑️ سحب وحذف</button>' +
                    '</div>' +
                '</div>'
            );
        }).join('') + '</div>';
    }

    const btnRunInspect = document.getElementById('btnRunInspect');
    if (btnRunInspect) btnRunInspect.addEventListener('click', function () { runCharacterInspect(); });
    const inspectQueryInput = document.getElementById('inspectQueryInput');
    if (inspectQueryInput) {
        inspectQueryInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') runCharacterInspect();
        });
    }
    const inspectItemFilter = document.getElementById('inspectItemFilter');
    if (inspectItemFilter) inspectItemFilter.addEventListener('input', renderInspectedItems);

    document.querySelectorAll('[data-inv-tab]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            activeInvFilterTab = btn.getAttribute('data-inv-tab') || 'all';
            document.querySelectorAll('[data-inv-tab]').forEach(function (b) {
                b.classList.toggle('active', b === btn);
            });
            renderInspectedItems();
        });
    });

    const inspectGearContainer = document.getElementById('inspectGearContainer');
    if (inspectGearContainer) {
        inspectGearContainer.addEventListener('click', async function (e) {
            const delBtn = e.target.closest('[data-del-inv-item]');
            if (!delBtn || !inspectedPlayer) return;
            const itemId = parseInt(delBtn.getAttribute('data-del-inv-item'), 10);
            const itemName = delBtn.getAttribute('data-del-inv-name') || '';
            if (!window.confirm('هل أنت متأكد من سحب وحذف الأداة [' + itemName + ' #' + itemId + '] نهائياً من الشخصية [' + inspectedPlayer.name + ']؟')) return;
            const res = await apiPost('inspect_delete_item', { player_id: inspectedPlayer.id, item_id: itemId });
            if (res && res.ok) {
                showToast('🗑️ ' + res.message);
                setTimeout(function () { runCharacterInspect(String(inspectedPlayer.id)); }, 900);
            } else {
                showToast('⚠️ ' + ((res && res.error) || 'فشل حذف الأداة'), true);
            }
        });
    }

    // =========================================================================
    // TOOL #8: AUTO-BROADCAST & SCHEDULE STUDIO
    // =========================================================================
    let autoBcItems = [];

    async function loadAutoBroadcast() {
        const tbody = document.getElementById('autoBcTableBody');
        if (!tbody) return;
        try {
            const resp = await fetch('./api.php?action=autobroadcast_get', { cache: 'no-store' });
            const data = await resp.json();
            const cfg = data.autobroadcast || { Enabled: true, Items: [] };
            const masterEl = document.getElementById('autoBcMasterEnabled');
            if (masterEl) masterEl.checked = !!cfg.Enabled;
            autoBcItems = Array.isArray(cfg.Items) ? cfg.Items : [];
            renderAutoBcTable();
        } catch (err) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:#fb7185;">تعذر قراءة جدول الإعلانات.</td></tr>';
        }
    }

    function renderAutoBcTable() {
        const tbody = document.getElementById('autoBcTableBody');
        if (!tbody) return;
        if (autoBcItems.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:var(--text-muted);">لا توجد رسائل مجدولة حالياً — اضغط "إضافة رسالة جديدة" أو اختر قالباً سريعاً!</td></tr>';
            return;
        }
        tbody.innerHTML = autoBcItems.map(function (it, idx) {
            const isDaily = it.Mode === 'daily';
            const timingInput = isDaily
                ? '<input type="time" class="text-input js-bc-daily" data-idx="' + idx + '" value="' + esc(it.DailyTime || '20:00') + '" style="direction:ltr;">'
                : '<div style="display:flex; align-items:center; gap:5px;"><input type="number" class="text-input js-bc-interval" data-idx="' + idx + '" min="1" max="1440" value="' + (it.IntervalMinutes || 15) + '"><small>دقيقة</small></div>';

            return (
                '<tr>' +
                    '<td style="text-align:center;"><input type="checkbox" class="js-bc-enabled" data-idx="' + idx + '" ' + (it.Enabled ? 'checked' : '') + '></td>' +
                    '<td>' +
                        '<select class="text-input js-bc-mode" data-idx="' + idx + '">' +
                            '<option value="interval" ' + (!isDaily ? 'selected' : '') + '>⏱️ كل X دقيقة</option>' +
                            '<option value="daily" ' + (isDaily ? 'selected' : '') + '>⏰ يومياً بالساعة</option>' +
                        '</select>' +
                    '</td>' +
                    '<td>' + timingInput + '</td>' +
                    '<td>' +
                        '<select class="text-input js-bc-channel" data-idx="' + idx + '">' +
                            '<option value="TopLeft" ' + (it.Channel === 'TopLeft' ? 'selected' : '') + '>أعلى الشاشة (أصفر)</option>' +
                            '<option value="Center" ' + (it.Channel === 'Center' ? 'selected' : '') + '>منتصف الشاشة (GM)</option>' +
                            '<option value="Broadcast" ' + (it.Channel === 'Broadcast' ? 'selected' : '') + '>شريط الحمام الزاجل</option>' +
                            '<option value="Talk" ' + (it.Channel === 'Talk' ? 'selected' : '') + '>شات النظام العام</option>' +
                        '</select>' +
                    '</td>' +
                    '<td><input type="text" class="text-input js-bc-msg" data-idx="' + idx + '" value="' + esc(it.Message || '') + '" placeholder="اكتب نص الإعلان هنا..."></td>' +
                    '<td style="display:flex; gap:5px;">' +
                        '<button type="button" class="btn btn-ghost btn-sm" data-bc-test="' + idx + '" title="إرسال تجريبي الآن">📣 بث</button>' +
                        '<button type="button" class="btn btn-crimson btn-sm" data-bc-del="' + idx + '">🗑️</button>' +
                    '</td>' +
                '</tr>'
            );
        }).join('');
    }

    function syncAutoBcDomToState() {
        const tbody = document.getElementById('autoBcTableBody');
        if (!tbody) return;
        tbody.querySelectorAll('.js-bc-enabled').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (autoBcItems[i]) autoBcItems[i].Enabled = !!el.checked;
        });
        tbody.querySelectorAll('.js-bc-channel').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (autoBcItems[i]) autoBcItems[i].Channel = el.value;
        });
        tbody.querySelectorAll('.js-bc-msg').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (autoBcItems[i]) autoBcItems[i].Message = el.value;
        });
        tbody.querySelectorAll('.js-bc-interval').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (autoBcItems[i]) autoBcItems[i].IntervalMinutes = parseInt(el.value || '15', 10);
        });
        tbody.querySelectorAll('.js-bc-daily').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (autoBcItems[i]) autoBcItems[i].DailyTime = el.value || '20:00';
        });
    }

    const autoBcTableBody = document.getElementById('autoBcTableBody');
    if (autoBcTableBody) {
        autoBcTableBody.addEventListener('change', function (e) {
            syncAutoBcDomToState();
            if (e.target.classList.contains('js-bc-mode')) {
                const i = parseInt(e.target.getAttribute('data-idx'), 10);
                if (autoBcItems[i]) {
                    autoBcItems[i].Mode = e.target.value;
                    renderAutoBcTable();
                }
            }
        });
        autoBcTableBody.addEventListener('click', function (e) {
            syncAutoBcDomToState();
            const delBtn = e.target.closest('[data-bc-del]');
            if (delBtn) {
                const i = parseInt(delBtn.getAttribute('data-bc-del'), 10);
                autoBcItems.splice(i, 1);
                renderAutoBcTable();
                return;
            }
            const testBtn = e.target.closest('[data-bc-test]');
            if (testBtn) {
                const i = parseInt(testBtn.getAttribute('data-bc-test'), 10);
                const row = autoBcItems[i];
                if (row && row.Message) {
                    sendStudioCommand({ type: 'broadcast_now', message: row.Message, channel: row.Channel || 'TopLeft' });
                }
            }
        });
    }

    const btnAddAutoBcRow = document.getElementById('btnAddAutoBcRow');
    if (btnAddAutoBcRow) {
        btnAddAutoBcRow.addEventListener('click', function () {
            syncAutoBcDomToState();
            autoBcItems.push({
                Id: 'bc_' + Date.now(),
                Enabled: true,
                Mode: 'interval',
                IntervalMinutes: 15,
                DailyTime: '20:00',
                Channel: 'TopLeft',
                Message: '[MoonQueen]: أهلاً بكم في عالم كونكر الأصيل — نتمنى لكم وقتاً ممتعاً!'
            });
            renderAutoBcTable();
        });
    }

    document.querySelectorAll('[data-bc-template]').forEach(function (chip) {
        chip.addEventListener('click', function () {
            syncAutoBcDomToState();
            const tpl = chip.getAttribute('data-bc-template');
            if (tpl === 'welcome') {
                autoBcItems.push({ Id: 'bc_' + Date.now(), Enabled: true, Mode: 'interval', IntervalMinutes: 20, DailyTime: '20:00', Channel: 'TopLeft', Message: '[MoonQueen]: مرحباً بأبطال السيرفر! لا تشارك كلمة مرورك مع أي شخص أبداً.' });
            } else if (tpl === 'gw') {
                autoBcItems.push({ Id: 'bc_' + Date.now(), Enabled: true, Mode: 'interval', IntervalMinutes: 30, DailyTime: '21:00', Channel: 'Center', Message: '[Guild War]: جهّز نقابتك لمعركة القلعة الكبرى — المجد والـ CPs بانتظار الأبطال!' });
            } else if (tpl === 'elitepk') {
                autoBcItems.push({ Id: 'bc_' + Date.now(), Enabled: true, Mode: 'daily', IntervalMinutes: 60, DailyTime: '19:55', Channel: 'Center', Message: '[Elite PK]: يتبقى 5 دقائق على انطلاق بطولة النخبة Elite PK في Twin City!' });
            } else if (tpl === 'promo') {
                autoBcItems.push({ Id: 'bc_' + Date.now(), Enabled: true, Mode: 'interval', IntervalMinutes: 25, DailyTime: '18:00', Channel: 'Talk', Message: '[MoonQueen]: لصرف أكواد الهدايا المجانية اكتب في الشات: /redeem CODE' });
            }
            renderAutoBcTable();
        });
    });

    const btnSaveAutoBc = document.getElementById('btnSaveAutoBc');
    if (btnSaveAutoBc) {
        btnSaveAutoBc.addEventListener('click', async function () {
            syncAutoBcDomToState();
            const enabled = !!((document.getElementById('autoBcMasterEnabled') || {}).checked);
            const res = await apiPost('autobroadcast_save', { enabled: enabled, items: autoBcItems });
            if (res && res.ok) {
                showToast('📣 ' + res.message);
                loadAutoBroadcast();
            } else {
                showToast('⚠️ ' + ((res && res.error) || 'تعذر حفظ الجدول'), true);
            }
        });
    }

    // =========================================================================
    // TOOL #9: BOSS & MONSTER CUSTOM DROP STUDIO
    // =========================================================================
    let customDropProfiles = [];
    let currentEditingRules = [];

    function renderDropRuleEditor() {
        const listEl = document.getElementById('cdRulesEditorList');
        if (!listEl) return;
        if (currentEditingRules.length === 0) {
            currentEditingRules.push({
                Id: 'dr_' + Date.now(),
                ItemType: 1088000,
                ItemName: 'DragonBall',
                ChancePercent: 100,
                MinCount: 1,
                MaxCount: 1,
                Plus: 0,
                Bless: 0,
                Sockets: 0,
                Bound: false,
                BonusSilver: 0,
                BonusCps: 0,
                BonusBoundCps: 0,
                WorldAnnounce: true
            });
        }
        listEl.innerHTML = currentEditingRules.map(function (r, idx) {
            return (
                '<div style="background:rgba(0,0,0,0.32); border:1px solid var(--border-subtle); border-radius:10px; padding:10px;">' +
                    '<div style="display:flex; justify-content:space-between; margin-bottom:6px;">' +
                        '<strong style="font-size:0.80rem; color:var(--gold);">قاعدة #' + (idx + 1) + '</strong>' +
                        '<button type="button" class="btn btn-crimson btn-sm" data-del-cd-rule="' + idx + '">✕ حذف القاعدة</button>' +
                    '</div>' +
                    '<div class="mini-form-grid" style="margin-bottom:6px;">' +
                        '<div><small>ItemType ID (0 = عملات فقط)</small><input type="number" class="text-input js-cdr-item" data-idx="' + idx + '" value="' + (r.ItemType || 0) + '"></div>' +
                        '<div><small>نسبة السقوط % (0.001–100)</small><input type="number" step="0.01" class="text-input js-cdr-chance" data-idx="' + idx + '" value="' + (r.ChancePercent ?? 100) + '"></div>' +
                    '</div>' +
                    '<div class="mini-form-grid" style="margin-bottom:6px;">' +
                        '<div><small>أقل كمية / أعلى كمية</small><div style="display:flex; gap:4px;"><input type="number" class="text-input js-cdr-min" data-idx="' + idx + '" value="' + (r.MinCount || 1) + '"><input type="number" class="text-input js-cdr-max" data-idx="' + idx + '" value="' + (r.MaxCount || 1) + '"></div></div>' +
                        '<div><small>+Plus / Sockets</small><div style="display:flex; gap:4px;"><input type="number" class="text-input js-cdr-plus" data-idx="' + idx + '" min="0" max="12" value="' + (r.Plus || 0) + '" placeholder="+Plus"><input type="number" class="text-input js-cdr-soc" data-idx="' + idx + '" min="0" max="2" value="' + (r.Sockets || 0) + '" placeholder="Soc"></div></div>' +
                    '</div>' +
                    '<div class="mini-form-grid" style="margin-bottom:6px;">' +
                        '<div><small>مكافأة CPs / Bound CPs للقاتل</small><div style="display:flex; gap:4px;"><input type="number" class="text-input js-cdr-cps" data-idx="' + idx + '" value="' + (r.BonusCps || 0) + '" placeholder="CPs"><input type="number" class="text-input js-cdr-bcps" data-idx="' + idx + '" value="' + (r.BonusBoundCps || 0) + '" placeholder="Bound"></div></div>' +
                        '<div><small>مكافأة فضة مباشرة</small><input type="number" class="text-input js-cdr-silver" data-idx="' + idx + '" value="' + (r.BonusSilver || 0) + '"></div>' +
                    '</div>' +
                    '<div style="display:flex; gap:14px; font-size:0.79rem;">' +
                        '<label><input type="checkbox" class="js-cdr-ann" data-idx="' + idx + '" ' + (r.WorldAnnounce ? 'checked' : '') + '> 📣 إعلان عالمي عند السقوط</label>' +
                        '<label><input type="checkbox" class="js-cdr-bound" data-idx="' + idx + '" ' + (r.Bound ? 'checked' : '') + '> 🔒 Bound</label>' +
                    '</div>' +
                '</div>'
            );
        }).join('');
    }

    function syncDropRulesFromDom() {
        const listEl = document.getElementById('cdRulesEditorList');
        if (!listEl) return;
        listEl.querySelectorAll('.js-cdr-item').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].ItemType = parseInt(el.value || '0', 10);
        });
        listEl.querySelectorAll('.js-cdr-chance').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].ChancePercent = parseFloat(el.value || '0');
        });
        listEl.querySelectorAll('.js-cdr-min').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].MinCount = parseInt(el.value || '1', 10);
        });
        listEl.querySelectorAll('.js-cdr-max').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].MaxCount = parseInt(el.value || '1', 10);
        });
        listEl.querySelectorAll('.js-cdr-plus').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].Plus = parseInt(el.value || '0', 10);
        });
        listEl.querySelectorAll('.js-cdr-soc').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].Sockets = parseInt(el.value || '0', 10);
        });
        listEl.querySelectorAll('.js-cdr-cps').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].BonusCps = parseInt(el.value || '0', 10);
        });
        listEl.querySelectorAll('.js-cdr-bcps').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].BonusBoundCps = parseInt(el.value || '0', 10);
        });
        listEl.querySelectorAll('.js-cdr-silver').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].BonusSilver = parseInt(el.value || '0', 10);
        });
        listEl.querySelectorAll('.js-cdr-ann').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].WorldAnnounce = !!el.checked;
        });
        listEl.querySelectorAll('.js-cdr-bound').forEach(function (el) {
            const i = parseInt(el.getAttribute('data-idx'), 10);
            if (currentEditingRules[i]) currentEditingRules[i].Bound = !!el.checked;
        });
    }

    async function loadCustomDrops() {
        renderDropRuleEditor();
        const tbody = document.getElementById('customDropsTableBody');
        if (!tbody) return;
        try {
            const resp = await fetch('./api.php?action=custom_drops_get', { cache: 'no-store' });
            const data = await resp.json();
            customDropProfiles = (data.custom_drops && data.custom_drops.Profiles) ? data.custom_drops.Profiles : [];
            if (customDropProfiles.length === 0) {
                tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-muted);">لا توجد قواعد مخصصة حالياً — الدروب يعمل بالنظام الرسمي النقي 100%.</td></tr>';
                return;
            }
            tbody.innerHTML = customDropProfiles.map(function (p, idx) {
                const summary = (p.Rules || []).map(function (r) {
                    const parts = [];
                    if (r.ItemType > 0) parts.push(esc(r.ItemName || ('#' + r.ItemType)) + ' (' + r.ChancePercent + '%)');
                    if (r.BonusCps > 0) parts.push(r.BonusCps + ' CPs (' + r.ChancePercent + '%)');
                    if (r.BonusSilver > 0) parts.push(Number(r.BonusSilver).toLocaleString('en-US') + ' Silver');
                    return parts.join(' + ');
                }).join('<br>');
                return (
                    '<tr>' +
                        '<td><strong>' + esc(p.MonsterName) + '</strong><br><code>#' + p.MonsterType + '</code></td>' +
                        '<td>' + (p.Mode === 'override' ? '<span class="badge-pill warn">استبدال</span>' : '<span class="badge-pill gold">إضافي</span>') + '</td>' +
                        '<td style="font-size:0.79rem;">' + (summary || '—') + '</td>' +
                        '<td>' + (p.Enabled ? '<span class="badge-pill ok">نشط</span>' : '<span class="badge-pill err">موقوف</span>') + '</td>' +
                        '<td style="display:flex; gap:5px;">' +
                            '<button type="button" class="btn btn-ghost btn-sm" data-edit-cd-idx="' + idx + '">✏️ تعديل</button>' +
                            '<button type="button" class="btn btn-crimson btn-sm" data-del-cd-mon="' + p.MonsterType + '">🗑️</button>' +
                        '</td>' +
                    '</tr>'
                );
            }).join('');
        } catch (err) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:#fb7185;">تعذر تحميل ملفات الدروب المخصص.</td></tr>';
        }
    }

    document.querySelectorAll('[data-drop-boss]').forEach(function (chip) {
        chip.addEventListener('click', function () {
            document.getElementById('cdMonsterType').value = chip.getAttribute('data-drop-boss');
            document.getElementById('cdMonsterName').value = chip.getAttribute('data-drop-boss-name');
        });
    });

    const btnAddDropRuleRow = document.getElementById('btnAddDropRuleRow');
    if (btnAddDropRuleRow) {
        btnAddDropRuleRow.addEventListener('click', function () {
            syncDropRulesFromDom();
            currentEditingRules.push({ Id: 'dr_' + Date.now(), ItemType: 1088000, ItemName: 'DragonBall', ChancePercent: 25, MinCount: 1, MaxCount: 1, Plus: 0, Sockets: 0, BonusCps: 0, BonusBoundCps: 0, BonusSilver: 0, WorldAnnounce: false, Bound: false });
            renderDropRuleEditor();
        });
    }

    const cdRulesEditorList = document.getElementById('cdRulesEditorList');
    if (cdRulesEditorList) {
        cdRulesEditorList.addEventListener('click', function (e) {
            const del = e.target.closest('[data-del-cd-rule]');
            if (!del) return;
            syncDropRulesFromDom();
            const i = parseInt(del.getAttribute('data-del-cd-rule'), 10);
            currentEditingRules.splice(i, 1);
            renderDropRuleEditor();
        });
    }

    const btnDropCatalogSearch = document.getElementById('btnDropCatalogSearch');
    if (btnDropCatalogSearch) {
        btnDropCatalogSearch.addEventListener('click', async function () {
            const q = ((document.getElementById('dropCatalogQuery') || {}).value || '').trim();
            const box = document.getElementById('dropCatalogResults');
            if (!q || !box) return;
            const resp = await fetch('./api.php?action=db_catalog_search&q=' + encodeURIComponent(q), { cache: 'no-store' });
            const data = await resp.json();
            box.style.display = 'block';
            const mons = (data.monsters || []).slice(0, 8).map(function (m) {
                return '<div>👹 <a href="#" data-pick-cd-mon="' + m.id + '" data-pick-cd-mname="' + esc(m.name) + '" style="color:var(--gold);">' + esc(m.name) + ' (#' + m.id + ' Lv.' + m.level + ')</a></div>';
            }).join('');
            const items = (data.items || []).slice(0, 10).map(function (it) {
                return '<div>🗡️ <a href="#" data-pick-cd-item="' + it.id + '" style="color:#60a5fa;">' + esc(it.name) + ' (#' + it.id + ')</a></div>';
            }).join('');
            box.innerHTML = (mons + items) || '<span class="control-hint">لا توجد نتائج مطابقة.</span>';
        });
    }

    const dropCatalogResults = document.getElementById('dropCatalogResults');
    if (dropCatalogResults) {
        dropCatalogResults.addEventListener('click', function (e) {
            const monLink = e.target.closest('[data-pick-cd-mon]');
            if (monLink) {
                e.preventDefault();
                document.getElementById('cdMonsterType').value = monLink.getAttribute('data-pick-cd-mon');
                document.getElementById('cdMonsterName').value = monLink.getAttribute('data-pick-cd-mname');
                return;
            }
            const itemLink = e.target.closest('[data-pick-cd-item]');
            if (itemLink) {
                e.preventDefault();
                syncDropRulesFromDom();
                if (currentEditingRules.length > 0) {
                    currentEditingRules[currentEditingRules.length - 1].ItemType = parseInt(itemLink.getAttribute('data-pick-cd-item'), 10);
                    renderDropRuleEditor();
                }
            }
        });
    }

    const btnSaveCustomDropProfile = document.getElementById('btnSaveCustomDropProfile');
    if (btnSaveCustomDropProfile) {
        btnSaveCustomDropProfile.addEventListener('click', async function () {
            syncDropRulesFromDom();
            const payload = {
                monster_type: parseInt(((document.getElementById('cdMonsterType') || {}).value || '0'), 10),
                monster_name: ((document.getElementById('cdMonsterName') || {}).value || '').trim(),
                mode: ((document.getElementById('cdDropMode') || {}).value || 'additional'),
                enabled: !!((document.getElementById('cdProfileEnabled') || {}).checked),
                rules: currentEditingRules.map(function (r) {
                    return {
                        id: r.Id,
                        item_type: r.ItemType,
                        chance_percent: r.ChancePercent,
                        min_count: r.MinCount,
                        max_count: r.MaxCount,
                        plus: r.Plus,
                        sockets: r.Sockets,
                        bound: r.Bound,
                        bonus_silver: r.BonusSilver,
                        bonus_cps: r.BonusCps,
                        bonus_bound_cps: r.BonusBoundCps,
                        world_announce: r.WorldAnnounce
                    };
                })
            };
            const res = await apiPost('custom_drop_save', payload);
            if (res && res.ok) {
                showToast('💎 ' + res.message);
                loadCustomDrops();
            } else {
                showToast('⚠️ ' + ((res && res.error) || 'تعذر حفظ الدروب'), true);
            }
        });
    }

    const btnRefreshCustomDrops = document.getElementById('btnRefreshCustomDrops');
    if (btnRefreshCustomDrops) btnRefreshCustomDrops.addEventListener('click', loadCustomDrops);

    const customDropsTableBody = document.getElementById('customDropsTableBody');
    if (customDropsTableBody) {
        customDropsTableBody.addEventListener('click', async function (e) {
            const editBtn = e.target.closest('[data-edit-cd-idx]');
            if (editBtn) {
                const p = customDropProfiles[parseInt(editBtn.getAttribute('data-edit-cd-idx'), 10)];
                if (!p) return;
                document.getElementById('cdMonsterType').value = p.MonsterType;
                document.getElementById('cdMonsterName').value = p.MonsterName;
                document.getElementById('cdDropMode').value = p.Mode || 'additional';
                document.getElementById('cdProfileEnabled').checked = !!p.Enabled;
                currentEditingRules = JSON.parse(JSON.stringify(p.Rules || []));
                renderDropRuleEditor();
                showToast('تم تحميل قواعد [' + p.MonsterName + '] للتعديل.');
                return;
            }
            const delBtn = e.target.closest('[data-del-cd-mon]');
            if (delBtn) {
                const monType = parseInt(delBtn.getAttribute('data-del-cd-mon'), 10);
                if (!window.confirm('حذف الدروب المخصص للوحش #' + monType + ' والعودة للدروب الرسمي؟')) return;
                const res = await apiPost('custom_drop_delete', { monster_type: monType });
                if (res && res.ok) {
                    showToast('✅ ' + res.message);
                    loadCustomDrops();
                }
            }
        });
    }

    // =========================================================================
    // TOOL #10: 1-CLICK BACKUP VAULT & LIVE ECONOMY RADAR
    // =========================================================================
    async function loadEconomyRadar() {
        try {
            const resp = await fetch('./api.php?action=economy_radar', { cache: 'no-store' });
            const data = await resp.json();
            if (!data.ok) {
                showToast('⚠️ ' + (data.error || 'تعذر قراءة رادار الاقتصاد'), true);
                return;
            }
            const s = data.metrics || {};
            const setTxt = function (id, v) { const el = document.getElementById(id); if (el) el.textContent = v; };
            setTxt('econAccountsChars', Number(s.total_accounts || 0).toLocaleString('en-US') + ' / ' + Number(s.total_chars || 0).toLocaleString('en-US'));
            setTxt('econBannedSub', 'المحظورون: ' + Number(s.banned_accounts || 0).toLocaleString('en-US') + ' | المتصلون: ' + Number(s.online_now || 0));
            setTxt('econTotalCps', Number(s.total_cps || 0).toLocaleString('en-US'));
            setTxt('econTotalBoundCps', 'Bound CPs: ' + Number(s.total_bound_cps || 0).toLocaleString('en-US'));
            setTxt('econTotalSilver', Number(s.total_silver || 0).toLocaleString('en-US'));
            setTxt('econDbsMeteors', Number(s.dragonballs || 0).toLocaleString('en-US') + ' DB');
            setTxt('econMeteorsSub', 'Meteors: ' + Number(s.meteors || 0).toLocaleString('en-US'));
            setTxt('econRareGear', Number(s.plus12_items || 0) + ' (+12)');
            setTxt('econSuperSub', '2-Soc: ' + (s.two_socket_items || 0) + ' | إجمالي القطع: ' + Number(s.total_items || 0).toLocaleString('en-US'));

            renderBackupsTable(data.backups || []);
            renderRichestPlayersTable(data.richest || []);
        } catch (err) {}
    }

    function renderBackupsTable(files) {
        const tbody = document.getElementById('backupsTableBody');
        if (!tbody) return;
        if (!files.length) {
            tbody.innerHTML = '<tr><td colspan="4" style="text-align:center; color:var(--text-muted);">لا توجد نسخ محفوظة بعد — اضغط "نسخ احتياطي شامل الآن".</td></tr>';
            return;
        }
        tbody.innerHTML = files.map(function (f) {
            const fname = f.file || f.name || '';
            return (
                '<tr>' +
                    '<td style="direction:ltr; font-size:0.80rem;"><code>' + esc(fname) + '</code></td>' +
                    '<td><strong>' + esc(f.size_human || ((f.size_kb || 0) + ' KB')) + '</strong></td>' +
                    '<td style="direction:ltr; font-size:0.78rem;">' + esc(f.created_at) + '</td>' +
                    '<td style="display:flex; gap:5px;">' +
                        '<a class="btn btn-ghost btn-sm" href="./api.php?action=backup_download&file=' + encodeURIComponent(fname) + '">⬇️ تحميل</a>' +
                        '<button type="button" class="btn btn-crimson btn-sm" data-del-backup="' + esc(fname) + '">🗑️</button>' +
                    '</td>' +
                '</tr>'
            );
        }).join('');
    }

    function renderRichestPlayersTable(rows) {
        const tbody = document.getElementById('richestPlayersTableBody');
        if (!tbody) return;
        if (!rows.length) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:var(--text-muted);">لا توجد شخصيات مسجلة بعد.</td></tr>';
            return;
        }
        tbody.innerHTML = rows.map(function (r) {
            const totalSilv = Number(r.total_silver ?? (Number(r.money || 0) + Number(r.money_saved || 0)));
            const pid = r.id || r.identity;
            return (
                '<tr>' +
                    '<td><strong>' + esc(r.name) + '</strong></td>' +
                    '<td>Lv.' + r.level + ' (' + r.metempsychosis + 'RB)</td>' +
                    '<td><strong>' + Number(r.emoney || 0).toLocaleString('en-US') + '</strong></td>' +
                    '<td>' + Number(r.emoney_mono || 0).toLocaleString('en-US') + '</td>' +
                    '<td>' + totalSilv.toLocaleString('en-US') + '</td>' +
                    '<td><button type="button" class="btn btn-ghost btn-sm" data-quick-inspect="' + pid + '">🎒 فحص</button></td>' +
                '</tr>'
            );
        }).join('');
    }

    document.querySelectorAll('[data-run-backup]').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            const scope = btn.getAttribute('data-run-backup') || 'both';
            btn.disabled = true;
            showToast('⏳ جاري أخذ وضغط النسخة الاحتياطية لـ MariaDB...');
            try {
                const res = await apiPost('backup_create', { scope: scope });
                if (res && res.ok) {
                    showToast('💾 ' + res.message);
                    loadEconomyRadar();
                } else {
                    showToast('⚠️ ' + ((res && res.error) || 'تعذر إنشاء النسخة الاحتياطية'), true);
                }
            } finally {
                btn.disabled = false;
            }
        });
    });

    const btnRefreshEconomy = document.getElementById('btnRefreshEconomy');
    if (btnRefreshEconomy) btnRefreshEconomy.addEventListener('click', loadEconomyRadar);

    const backupsTableBody = document.getElementById('backupsTableBody');
    if (backupsTableBody) {
        backupsTableBody.addEventListener('click', async function (e) {
            const delBtn = e.target.closest('[data-del-backup]');
            if (!delBtn) return;
            const fname = delBtn.getAttribute('data-del-backup');
            if (!window.confirm('حذف النسخة الاحتياطية [' + fname + ']؟')) return;
            const res = await apiPost('backup_delete', { file: fname });
            if (res && res.ok) {
                showToast('🗑️ ' + res.message);
                loadEconomyRadar();
            }
        });
    }

    const richestPlayersTableBody = document.getElementById('richestPlayersTableBody');
    if (richestPlayersTableBody) {
        richestPlayersTableBody.addEventListener('click', function (e) {
            const insp = e.target.closest('[data-quick-inspect]');
            if (insp) openInspectorFor(insp.getAttribute('data-quick-inspect'));
        });
    }

    async function pollStatus() {
        try {
            const resp = await fetch('./api.php?action=status', { cache: 'no-store' });
            if (!resp.ok) return;
            const data = await resp.json();
            if (data && data.ok && data.status) {
                renderStatus(data.status);
            }
        } catch (e) {
            // Ignore transient polling hiccup
        }
    }

    updateLiveMathPreviews();
    pollStatus();
    setInterval(pollStatus, 1800);
})();
