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

    // Tabs switching
    document.querySelectorAll('[data-tab-target]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const target = btn.getAttribute('data-tab-target');
            document.querySelectorAll('[data-tab-target]').forEach(function (b) {
                b.classList.toggle('active', b === btn);
            });
            document.querySelectorAll('.tab-pane').forEach(function (pane) {
                pane.classList.toggle('active', pane.id === target);
            });
            if (target === 'paneShopsStudio' && (!shopsSnapshot.shops || shopsSnapshot.shops.length === 0)) {
                loadShopsSnapshot();
            }
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
                    '<td>' + stateBadges.join(' ') + '</td>' +
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

        if (st.events) renderEventsGrid(st.events);
        if (st.online_players_list) renderOnlinePlayers(st.online_players_list);
        if (st.spawned_bosses) renderSpawnedBosses(st.spawned_bosses);
        if (st.recent_results) renderExecutionFeed(st.recent_results);
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
