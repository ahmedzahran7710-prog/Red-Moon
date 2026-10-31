(function () {
  "use strict";

  // 1. رسم جزيئات الجمر الأحمر المتطايرة في الخلفية بخفة عالية
  const canvas = document.getElementById("emberCanvas");
  if (canvas && canvas.getContext) {
    const ctx = canvas.getContext("2d");
    let w = (canvas.width = window.innerWidth);
    let h = (canvas.height = window.innerHeight);
    const particles = Array.from({ length: 38 }, () => ({
      x: Math.random() * w,
      y: Math.random() * h,
      r: Math.random() * 2.2 + 0.7,
      vy: -(Math.random() * 0.55 + 0.2),
      vx: (Math.random() - 0.5) * 0.35,
      a: Math.random() * 0.65 + 0.2,
    }));

    window.addEventListener("resize", () => {
      w = canvas.width = window.innerWidth;
      h = canvas.height = window.innerHeight;
    });

    function drawEmbers() {
      ctx.clearRect(0, 0, w, h);
      for (const p of particles) {
        p.x += p.vx;
        p.y += p.vy;
        if (p.y < -10) {
          p.y = h + 10;
          p.x = Math.random() * w;
        }
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(255, 58, 92, ${p.a})`;
        ctx.fill();
      }
      requestAnimationFrame(drawEmbers);
    }
    requestAnimationFrame(drawEmbers);
  }

  // 2. تنبيه منبثق أنيق (Toast)
  const toastEl = document.getElementById("mqToast");
  let toastTimer = null;
  function showToast(msg) {
    if (!toastEl) return;
    toastEl.textContent = msg;
    toastEl.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toastEl.classList.remove("show"), 4200);
  }

  // 3. التبديل بين تبويبي (إنشاء حساب جديد / تغيير كلمة المرور)
  const authTabBtns = document.querySelectorAll("[data-auth-tab]");
  const authPanes = document.querySelectorAll("[data-auth-pane]");

  function activateAuthTab(target) {
    authTabBtns.forEach((b) => b.classList.toggle("active", b.dataset.authTab === target));
    authPanes.forEach((p) => p.classList.toggle("active", p.dataset.authPane === target));
  }

  authTabBtns.forEach((btn) => {
    btn.addEventListener("click", () => activateAuthTab(btn.dataset.authTab));
  });

  document.querySelectorAll("[data-jump-auth]").forEach((link) => {
    link.addEventListener("click", () => {
      activateAuthTab(link.dataset.jumpAuth);
    });
  });

  // 4. إظهار / إخفاء كلمة المرور
  document.querySelectorAll(".pw-toggle").forEach((btn) => {
    btn.addEventListener("click", () => {
      const input = btn.parentElement.querySelector("input");
      if (!input) return;
      const isPw = input.type === "password";
      input.type = isPw ? "text" : "password";
      btn.textContent = isPw ? "إخفاء" : "إظهار";
    });
  });

  // 5. أزرار التحميل اللي لسه ملهاش لينك تعرض تنبيه شيك للاعب
  document.querySelectorAll("[data-coming-soon]").forEach((btn) => {
    btn.addEventListener("click", (e) => {
      e.preventDefault();
      showToast("جاري تجهيز ورفع رابط التحميل الرسمي لسيرفر MoonQueen — تابعنا وهينزل قريبًا جدًا!");
    });
  });

  // 6. إرسال فورم إنشاء الحساب وتغيير الباسورد عبر AJAX
  function updateCaptchaLabels(newLabel) {
    if (!newLabel) return;
    document.querySelectorAll(".js-captcha-label").forEach((el) => {
      el.textContent = newLabel;
    });
  }

  document.querySelectorAll(".js-auth-form").forEach((form) => {
    form.addEventListener("submit", async (e) => {
      e.preventDefault();
      const alertBox = form.querySelector(".alert-box");
      const submitBtn = form.querySelector("button[type='submit']");
      const origText = submitBtn ? submitBtn.textContent : "";

      if (alertBox) {
        alertBox.className = "alert-box";
        alertBox.textContent = "";
      }
      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.textContent = "جاري التنفيذ...";
      }

      try {
        const fd = new FormData(form);
        const res = await fetch("api.php", {
          method: "POST",
          body: fd,
          headers: { Accept: "application/json" },
        });
        const data = await res.json();
        updateCaptchaLabels(data.captcha_label);

        if (alertBox) {
          alertBox.textContent = data.message || "تمت العملية.";
          alertBox.classList.add("show", data.ok ? "success" : "error");
        }
        if (data.ok) {
          form.reset();
          refreshLiveStats();
        } else {
          const capInput = form.querySelector("input[name='captcha']");
          if (capInput) capInput.value = "";
        }
      } catch (err) {
        if (alertBox) {
          alertBox.textContent = "تعذر الاتصال بالسيرفر حاليًا، تأكد من تشغيل Apache و MySQL في XAMPP.";
          alertBox.classList.add("show", "error");
        }
      } finally {
        if (submitBtn) {
          submitBtn.disabled = false;
          submitBtn.textContent = origText;
        }
      }
    });
  });

  // 7. التبديل بين اللاعبين الأونلاين وأقوى أبطال السيرفر وتحديثهم تلقائيًا
  const rosterTabBtns = document.querySelectorAll("[data-roster-tab]");
  const rosterPanes = document.querySelectorAll("[data-roster-pane]");

  rosterTabBtns.forEach((btn) => {
    btn.addEventListener("click", () => {
      const target = btn.dataset.rosterTab;
      rosterTabBtns.forEach((b) => b.classList.toggle("active", b.dataset.rosterTab === target));
      rosterPanes.forEach((p) => p.classList.toggle("active", p.dataset.rosterPane === target));
    });
  });

  function escHtml(str) {
    const d = document.createElement("div");
    d.textContent = String(str ?? "");
    return d.innerHTML;
  }

  function rebornText(rb) {
    if (rb >= 2) return "2nd Reborn ✦✦";
    if (rb === 1) return "1st Reborn ✦";
    return "Non-Reborn";
  }

  function renderPlayersList(containerId, list, emptyMsg, isLeaderboard) {
    const el = document.getElementById(containerId);
    if (!el) return;
    if (!Array.isArray(list) || list.length === 0) {
      el.innerHTML = `<div class="empty-roster">${escHtml(emptyMsg)}</div>`;
      return;
    }
    el.innerHTML = list
      .map(
        (p, idx) => `
      <div class="player-card">
        <div class="player-top">
          <span class="player-name">${escHtml(p.name)}</span>
          <span class="rank-chip">${isLeaderboard ? "#" + (idx + 1) : "متصل الآن"}</span>
        </div>
        <div class="player-meta">
          <span class="meta-pill">${escHtml(p.profession)}</span>
          <span class="meta-pill reborn">${rebornText(Number(p.reborn || 0))}</span>
          <span class="meta-pill">Lv. ${Number(p.level || 1)}</span>
          <span class="meta-pill">PK: ${Number(p.pk || 0)}</span>
        </div>
      </div>`
      )
      .join("");
  }

  async function refreshLiveStats(showFeedback) {
    try {
      const res = await fetch("api.php?action=stats", {
        headers: { Accept: "application/json" },
      });
      if (!res.ok) return;
      const payload = await res.json();
      if (!payload.ok || !payload.stats) return;
      const st = payload.stats;

      const dot = document.getElementById("navStatusDot");
      const label = document.getElementById("navStatusText");
      const statStatus = document.getElementById("statServerStatus");
      if (dot) dot.classList.toggle("offline", !st.server_online);
      if (label) label.textContent = st.server_online ? `أونلاين (${st.online_count})` : "مغلق مؤقتًا";
      if (statStatus) {
        statStatus.textContent = st.server_online ? "متصل وشغال (ONLINE)" : "تحت الصيانة / أوفلاين";
        statStatus.className = "stat-value " + (st.server_online ? "online" : "crimson");
      }

      const onlineCountEl = document.getElementById("statOnlineCount");
      const accCountEl = document.getElementById("statTotalAccounts");
      if (onlineCountEl) onlineCountEl.textContent = String(st.online_count ?? 0);
      if (accCountEl) accCountEl.textContent = String(st.total_accounts ?? 0);

      renderPlayersList(
        "onlinePlayersGrid",
        st.online_players,
        st.server_online
          ? "لا يوجد لاعبون متصلون في هذه اللحظة — ادخل دلوقتي وكن أول أبطال الخريطة!"
          : "السيرفر غير متصل حاليًا — بمجرد تشغيل السيرفر هيظهر كل اللاعبين الأونلاين هنا تلقائيًا.",
        false
      );
      renderPlayersList(
        "topHeroesGrid",
        st.top_heroes,
        "قائمة الأبطال هتتملي تلقائيًا مع أول الشخصيات اللي تنزل عالم MoonQueen!",
        true
      );

      if (showFeedback) {
        showToast("تم تحديث حالة السيرفر وقائمة اللاعبين بنجاح!");
      }
    } catch (_) {
      // تجاهل بهدوء في حالة عدم الاتصال
    }
  }

  const refreshBtn = document.getElementById("btnRefreshStats");
  if (refreshBtn) {
    refreshBtn.addEventListener("click", () => refreshLiveStats(true));
  }

  setInterval(() => refreshLiveStats(false), 30000);
})();
