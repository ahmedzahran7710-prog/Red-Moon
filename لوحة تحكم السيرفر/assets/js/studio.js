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

    function showToast(msg, isError) {
        const toast = document.getElementById('studioToast');
        if (!toast) return;
        toast.textContent = msg;
        toast.classList.toggle('error', !!isError);
        toast.classList.add('show');
        clearTimeout(showToast._timer);
        showToast._timer = setTimeout(function () {
            toast.classList.remove('show');
        }, 4200);
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
                cpTag.textContent = 'مغلق حالياً (Classic+ نقي بدون CPs عشوائية من الوحوش)';
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

    // Bind preset cards
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

    function collectRatesPayload() {
        const rates = {
            PresetName: presetNameInput ? presetNameInput.value : 'Classic+ Custom'
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
    setInterval(pollStatus, 1800);
})();
