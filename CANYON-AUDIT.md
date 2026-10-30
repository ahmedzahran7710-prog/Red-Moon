# تقرير الفحص والتأمين الشامل لسورس CanyonV6192

> **مستوى الدليل:** فحص ثابت شامل للكود وجميع ملفات الأرشيف الأصلية الـ 863 بعد تنزيلها وفكها بالكامل، متبوعًا **بإصلاح جميع الأخطاء الخمسة الأولية (10 ملفات C#) ثم تطبيق 3 حزم تأمين شاملة للتشفير والشبكة والـ Anti-Cheat (25 ملف C#؛ إجمالي 35 ملفًا مُرقّعًا من أصل 845 ملفًا)** ومراجعتها مراجعة ثابتة ثانية سطرًا بسطر. لم يُنفذ Build أو اختبارات تشغيل أو استيراد SQL أو تشغيل عميل اللعبة التزامًا بسياسة المشروع الحالية.

## 1. هوية النسخة وتنظيمها في جذر المستودع

- المستودع الأصلي: `https://github.com/darkfoxdeveloper/CanyonV6192`
- المرجع المثبت (Commit SHA): `d08d13187599c2275972b83b81ff8e66296da2b2`
- بصمة الأرشيف الكامل (SHA-256): `2d44a9cf25574c11d815541fbf260e33260e797e7bea1aff1ddb8f56985d8158`
- الترخيص الأصلي: MIT (`LICENSE` محفوظ بالبايت كما هو في جذر المستودع).
- إجمالي ملفات الأرشيف الأصلي: 863 ملفًا بحجم 53,090,052 بايت.
- ما دُمج في جذر المستودع مباشرة: **845 ملفًا** (يشمل كل ملفات C# الـ 786، وكل ملفات المشاريع التسعة `.csproj`، وملف الحل `Canyon.sln`، والترخيص `LICENSE`، وجميع ملفات سكربتات Lua الـ 33 داخل `RequiredAssetsForRun/lua/`)، منها **810 ملفات مطابقة للأصل بالبايت** و**35 ملف C# مُرقّعة ومُؤمّنة** مع الاحتفاظ ببصمة الأصل (`upstream_sha256`) وبصمة النسخة المُصححة (`sha256`) في `canyon-manifest.json`.
- الملفات الـ 18 المستبعدة من Git (موثقة بالاسم والحجم وبصمة SHA-256 وسبب الاستبعاد في `canyon-manifest.json` ومحجوبة في `.gitignore`):
  - أرشيفات كبيرة ونسخ داتابيز تحتوي حسابات وسجلات لاعبين وعناوين IP/MAC قديمة وكلمات مرور افتراضية (`Lua_7117.rar`، و`sql - backup.rar`، والملفات الثلاثة داخل `sql/*.sql`).
  - ملفات إعدادات التطوير التي تحتوي كلمات مرور ومفاتيح تشفير وتوكنات خاصة بالناشر الأصلي (`*.Config.json`، `*.Config.local.json`، `launchSettings.json`، `compose.debug*.yml`، وملف `README.md` الأصلي لاحتوائه على توكن SonarQube بصيغة `squ_...` في رابط الشارة).
  - ملف `.gitignore` الأصلي حتى لا يتداخل مع قواعد حماية الأسرار والتتبع الخاصة بالمستودع.

---

## 2. نقاط القوة الحقيقية التي ثبتت من فحص الكود

1. **معمارية نظيفة جدًا وحديثة (`.NET 7` + تقسيم احترافي):**
   - السورس مبني فوق معمارية Comet بدون أي كود قديم موروث من سورسات LOTF أو MsgServer الفوضوية، ولا يعتمد على أي مكتبات خارجية مغلقة المصدر (`DLLs` مجهولة)؛ حتى خوارزميات التشفير (`Cast5Engine`, `Blowfish`, `DiffieHellman`, `RC5`) مكتوبة ككود C# مفتوح ونظيف داخل `src/Canyon.Network/Security`.
   - مقسّم إلى 9 مشاريع مستقلة واضحة المسؤوليات: `Canyon.Login` (حسابات ودخول)، `Canyon.Game` (عالم اللعبة)، `Canyon.Ai` (سيرفر مستقل تمامًا لحركة وذكاء الوحوش والـ Generators)، `Canyon.World` (بيانات الخرائط)، `Canyon.Network` (الشبكة والتشفير)، `Canyon.Database` (Entity Framework Core + Pomelo MySQL)، `Canyon.Shared`، `Canyon.GM.Server`، ومشروع اختبارات `Canyon.Network.Tests`.

2. **اكتمال فعلي لأنظمة ظنّ البعض أنها ناقصة بسبب قِدم الـ README الأصلي:**
   - رغم أن ملف README القديم يذكر أن `Lottery` و `Offline TG` و `Enemy List` في قائمة الـ TODO، إلا أن فحص الكود أثبت أنها **مكتوبة ومربوطة فعليًا**:
     - اللوتري موجود في `MsgLottery.cs` و `LotteryManager` وجدول `cq_lottery`.
     - التدريب الأوفلاين (Offline TG) موجود في `MsgTraining.cs` بحالاته الأساسية (استعلام الوقت، الدخول، استلام المكافأة).
     - قائمة الأعداء والأصدقاء وشركاء التجارة والـ Mentor موجودة في `States/Relationship` و `Tutor.cs`.
   - **أنظمة متوافقة مع أهداف Classic+:** الخيل وسباق الخيل (`HorseRacing.cs` + `CompositionSteed`), الفان والتاور والـ Gem Embedding، الجيلد أرسنال الكامل (`Totem.cs`, `TotemPole.cs`, `MsgTotemsRegister.cs`), العائلات وحرب العائلات (`Family.cs`, `FamilyWar.cs`), الأرينا الفردية (`ArenaQualifier.cs`), التطوير والـ Refinery (`ItemQuench.cs`), الـ SubClasses، الـ Chi (`Fate.cs`)، الـ JiangHu، والـ Inner Strength، وأكثر من 156 معالج باكت (`Msg*.cs`).

3. **نظام المهام والـ NPCs مزدوج ومريح للتعديل:**
   - يدعم محرك أوامر قاعدة البيانات الرسمي (`GameAction.cs` لجدولَي `cq_action` و `cq_task`) جنبًا إلى جنب مع محرك سكربتات **Lua** (`LuaScriptManager.cs` و `LuaProcessor.cs`) مع قفل تزامن (`lock (syncObject)`) يمنع تداخل استدعاءات Lua من الخيوط المختلفة.

---

## 3. الأخطاء البرمجية الخمسة المكتشفة وحالة إصلاحها (`PR #12` — 10 ملفات C#)

كشف التدقيق السطري للكود الأصلي عن **5 أخطاء حقيقية ومهمة جدًا**، وقد **تم إصلاحها جميعًا بعناية داخل `src/` ومراجعتها مراجعة ثابتة ثانية سطرًا بسطر**:

1. **[تم الإصلاح] ثغرة تجاوز تسجيل الدخول في وضع Release وحالات الحظر والتوكن (`MsgAccount.cs`, `GameAccount.cs`, `MsgConnect.cs`):**
   - **المشكلة الأصلية:** في مسار `USE_MYSQL_DB` داخل `MsgAccount.cs`، كانت `response.Success` تبدأ بـ `true` ولا تتحول إلى `false` عند عدم وجود الحساب (`gameAccount == null`)، مما كان يسمح في بناء `Release` بتجاوز تسجيل الدخول بأي اسم غير موجود على `AccountId = 0`. كما كانت أعلام `IsBanned` و`IsPermanentlyBanned` و`IsLocked` ثابتة على `false`، وفي `MsgConnect.cs` كان التوكن البديل `((uint)Token).ToString()` لا يُحذف عند المطابقة.
   - **الإصلاح المطبق:** جعل الحالة الافتراضية `Success = false`، واشتراط وجود الحساب وصلاحية `gameAccount.Id > 0` ومطابقة كلمة السر عبر `GameAccount.CheckPassword` المحصّنة ضد القيم الفارغة واختلاف حالة أحرف الـ Hex، وربط `GameAccount.Flag` بـ `AccountFlag` (`0/1` عادي متوافق مع قواعد البيانات الأصلية، `2` مقفول، `3` محظور، `4` حظر نهائي)، وتوحيد شرط `AccountAuthority < 2` في وضع `DEBUG`، واستخدام الحذف الذري `Kernel.Logins.Remove(...)` على المفتاح المطابق في الحالتين مع رفض `AccountID == 0`.

2. **[تم الإصلاح] ثغرة انتقال VIP مجاني وترتيب فحص انتقال عضو الفريق (`MsgVipUserHandle.cs`):**
   - **المشكلة الأصلية:** غياب `else return;` بعد فروع `Mode == 0` و`Mode == 1` و`Mode == 3` كان يسمح بتمرير أي `Mode` آخر لتنفيذ `FlyMapAsync` مجانًا بلا صلاحية VIP ولا Cooldown، كما كان فحص `VipTransPointRepository.GetAsync((uint)Location)` يسبق فرع عضو الفريق (`Mode == 3`) فيفشل قبول الانتقال عند إرسال `Location = 0`.
   - **الإصلاح المطبق:** التحقق أولًا من حياة اللاعب وخريطته ومنع الخرائط المحظورة، ثم فصل استعلام `VipTransPointRepository` داخل فرعَي `Mode == 0` و`Mode == 1` فقط، وترك `Mode == 3` يقرأ الوجهة مباشرة من `user.Team.GetTransPoint()`، وإغلاق أي قيمة أخرى لـ `Mode` بـ `else return;`.

3. **[تم الإصلاح] توقف رسبون الوحوش وتسرب هويات `IdentityManager.Monster` (`Monster.cs`, `Generator.cs`, `MsgAiSpawnNpc.cs`):**
   - **المشكلة الأصلية:** في `Canyon.Ai`، كانت `Monster.LeaveMapAsync` تعيد رقم الوحش فورًا إلى `IdentityManager.Monster` بينما الرقم ما زال محتجزًا داخل `generator.monsters` طوال فترة `awaitingReborn`، وعند فشل `monsters.TryAdd` أو غياب `monsterType` في الكلاستر كان الرقم يُفقد للأبد دون `ReturnIdentity` حتى ينفد الـ Pool ويتوقف الرسبون.
   - **الإصلاح المطبق:** ربط دورة حياة رقم الوحش بقاموس `monsters` بدقة بحيث لا يُعاد الرقم إلى `IdentityManager.Monster` إلا لحظة حذفه الفعلي من `monsters` بعد انتهاء عداد `awaitingReborn` (وتنظيف العدادات المنتهية في بداية `GenerateAsync` حتى لو غادر اللاعبون الخريطة)، وسحب الهوية فقط بعد نجاح اختيار نوع الوحش والإحداثيات، وإرجاع الهوية فورًا في جميع مسارات الفشل في `Generator.cs` و`MsgAiSpawnNpc.cs`، واشتراط `x.Map != null` في `OnTimerAsync` و`SendAll`.

4. **[تم الإصلاح] التزامن وتعديل العملات وفحص الترسانة في التجارة (`Trade.cs`):**
   - **المشكلة الأصلية:** غياب قفل تزامن حول دوال التجارة وتأخر تصفير `User1.Trade = null` لما بعد `await`، وعدم تصفير `accept1` و`accept2` عند تعديل الفضة أو الـ CPs، وعدم إعادة فحص تسجيل القطعة في الجيلد أرسنال (`SyndicateIdentity`) أو البوث أو سقف الفضة والـ CPs قبل النقل.
   - **الإصلاح المطبق:** تأمين جميع عمليات `Trade` بقفل `SemaphoreSlim(1, 1)` مع فصل `SendCloseInternalAsync` لمنع أي Deadlock عاكس، وتصفير `accept1 = false; accept2 = false;` عند أي إضافة أداة أو تعديل فضة أو CPs، وتصفير `isClosed = true` و`Trade = null` فور اكتمال التحقق وقبل أي `await`، وإعادة التحقق الكامل عبر `IsValidTradeItem` (بما فيها `SyndicateIdentity == 0` وعدم وجود القطعة في `Booth`) والتحقق من عدم تجاوز سقف الفضة (`10_000_000_000`) أو حد الـ CPs قبل الخصم والإضافة.

5. **[تم الإصلاح] حاجز IP في وضع Debug مع Hamachi وحماية `MsgDataArray` من الانهيار والتلاعب (`MsgAccServerAuth.cs`, `MsgDataArray.cs`):**
   - **المشكلة الأصلية:** رفض `MsgAccServerAuth` في وضع `DEBUG` لأي عنوان غير `192.168.*` و`127.*` (مما يحجب Hamachi `25.*` وRadmin `26.*` وشبكات LAN الخاصة `10.*` و`172.16-31.*`)، وقراءة `Items[0]` و`Items[2]` في `MsgDataArray.cs` دون فحص الحدود أو منع تكرار نفس الـ ID أو تمرير أداة غير DragonBall في ترقية المستوى.
   - **الإصلاح المطبق:** إضافة `IsAllowedDebugAddress` في `MsgAccServerAuth.cs` لدعم `localhost` والـ Loopback وشبكات RFC1918 الخاصة وHamachi (`25.0.0.0/8`) وRadmin (`26.0.0.0/8`) وTailscale (`100.64.0.0/10`) مع إبقاء حظر العناوين العامة الخارجية في وضع `DEBUG` وتحويل السجل إلى `LogWarning`. وفي `MsgDataArray.cs` تم تأمين حدود القراءة في `Decode`، وفحص `user == null || Items.Count == 0`، ومنع تكرار نفس هوية الأداة أو دمج الأداة مع نفسها، وإصلاح قراءة `Items[2]` واشتراط أن تكون أداة الترقية `DragonBall` أو `DragonBallScroll` فعلية داخل الحقيبة.
   - *ملحوظة معمارية غير حرجة باقية كما هي:* طوابير `ServerProcessor` و`PacketProcessor` تستخدم `Channel.CreateUnbounded` كسائر مشاريع Comet/Canyon.

---

## 3-ب. حزم التأمين الشاملة للتشفير والشبكة ومكافحة الغش (`PR #13` — 25 ملف C#)

عقب مراجعة متخصصة لطبقات التشفير والشبكة ومصادر مطوري كونكر (بما فيها أبحاث حقن الباكتات داخل الكلاينت قبل التشفير على `Cooldown.dev` وويكي `Spirited`)، طُبّقت **3 حزم تأمين متكاملة عبر 25 ملف C# داخل `src/`**:

1. **الحزمة الأولى — التشفير الديناميكي، مصادقة الربط الداخلي، وحماية SQL والـ Actions:**
   - **إصلاح `AesCipher.GenerateKeys` وتأمين تبادل المفاتيح (`AesCipher.cs`, `DiffieHellman.cs`, `NDDiffieHellman.cs`):** كان السطر الأصلي `key.CopyTo(new Memory<byte>(aes.Key))` ينسخ مفتاح Diffie-Hellman إلى نسخة مؤقتة مهملة دون تحديث `aes.Key` الفعلي، كما كان `ToByteArrayUnsigned()` قد يُنتج مصفوفة أقل من 32 بايت. تم اشتقاق مفتاح الجلسة بحجم ثابت 256-bit عبر `SHA256.HashData(rawKey)` وتعيينه فعليًا في `aes.Key` مع تحرير وإعادة إنشاء `ICryptoTransform`، ورفض المفاتيح التافهة أو خارج النطاق (`<= 1` أو `>= P - 1`) وسلاسل الـ Hex التالفة في `DiffieHellman` و`NDDiffieHellman`.
   - **قفل بورتات الربط الداخلي قبل المصادقة (`NpcServer.cs`, `MsgAiLoginExchange.cs`, `Canyon.Ai/Sockets/GameServerHandler.cs`, `Canyon.Login/Sockets/Game/GameServer.cs`, `MsgAccServerLoginExchangeEx.cs`, `RealmRepository.cs`, `Canyon.GM.Server/Sockets/Game/GameServer.cs`):** منع تعيين `NpcServer.NpcClient` قبل نجاح مصادقة الـ AI، وحجب وقطع أي باكت على بورتات `NpcServer` و`Ai` و`Login` و`GM` قبل اكتمال المصادقة، واستخدام المقارنة الزمنية الثابتة `CryptographicOperations.FixedTimeEquals` و`Guid.TryParse` مع الحفاظ التام على توافق تشفير `RealmRepository` مع ملفات SQL الأصلية.
   - **منع حقن SQL في `GameAction.cs` وتحصين `MsgTaskDialog.cs`:** تعقيم `%accept0` وجميع المتغيرات النصية عبر `EscapeSqlLiteral` عند تنفيذ `ActionExecutequery` و`ActionUserSelectToData`، ومنع تعدد الأوامر والتعليقات والكلمات الخطرة عبر `IsSafeActionSql`، وإغلاق تسريب `ServerDbContext` وفحص حدود `VarData`، وتقييد `TaskInteraction.TextInput` (`20000001..20099998`) بمنع الموتى والسجون والتجارة وفرض Cooldown والتحقق من وجود الـ Action.

2. **الحزمة الثانية — حماية الشبكة والـ Anti-DDoS وتقسيم الباكتات وما قبل الدخول:**
   - **تفعيل حدود `TcpServerRegistry` وتحصين `TcpServerListener` و`TcpClientWrapper`:** تفعيل سقف الاتصالات النشطة والمحاولات في الدقيقة لكل IP خارجي (`16` اتصالًا نشطًا و`35` محاولة/دقيقة، مع استثناء `127.0.0.1` للخدمات المحلية)، وإغلاق ثغرة تجميد البورت في `ExchangingAsync` عند إرسال طول هاندشيك أقل من 9 بايت، وضمان استرجاع البافر والـ Semaphore داخل `finally` في جميع مسارات الفشل، وتحديد سقف الباكت (`1024` بايت للكلاينت) والحد الأدنى (`4` بايت) والتحقق الصارم من ختم `"TQClient"` وأختام السيرفرات الداخلية.
   - **حراسة الباكتات قبل الدخول والمدخلات الفارغة (`Canyon.Game/Sockets/Game/GameServer.cs`, `MsgTalk.cs`, `RoleManager.cs`, `MsgRegister.cs`):** قصر باكتات ما قبل الخريطة على المرحلة الصحيحة لكل عميل، ومنع الرسائل الفارغة في `MsgTalk` من إغراق جدول `DbMessageLog` أو رمي `IndexOutOfRangeException`، واشتراط طول اسم الشخصية بين 3 و15 حرفًا في `RoleManager.IsValidName`، والحذف الذري لتوكن التسجيل في `MsgRegister`.

3. **الحزمة الثالثة — مكافحة الغش داخل العالم (Anti-Cheat & Anti-Spoofing):**
   - **نظام Anti-Speedhack بنموذج Leaky-Bucket (`Character.cs`, `MsgWalk.cs`, `MsgAction.cs`):** إضافة `ValidateMovementRateAsync` لحساب ميزانية الحركة والقفز مع مراعاة كل حالات السرعة الشرعية (`Cyclone`, `SuperCyclone`, `DragonCyclone`, `Oblivion`, `Riding`, `Accelerated`, `IncreaseMoveSpeed`, `Fly`, `FatalStrike`) وسماحية تذبذب الشبكة (`650ms`)، وإرجاع المخالف بـ `KickbackAsync` ثم طرده عند تكرار المخالفات، وتقييد مسافة القفز بسقف `Screen.VIEW_SIZE`.
   - **منع انتحال الهوية وتحريك أو توريط اللاعبين الآخرين (`MsgWalk.cs`, `MsgAction.cs`, `MsgInteract.cs`):** قصر التحريك والقفز وتغيير الاتجاه والهجوم على اللاعب نفسه أو وحش الاستدعاء المملوك له (`IsCallPet() && OwnerIdentity == user.Identity`)، مما يغلق نهائيًا ثغرات تحريك الخصوم أو إجبارهم على ضرب الحراس واللاعبين بالباكتات المحقونة.
   - **حماية البوابات والمخازن عن بُعد (`MsgAction.cs`, `GameMap.cs`, `MsgPackage.cs`):** التحقق من وجود البوابة فعليًا قرب اللاعب ومنع الهروب من السجن أو الانتقال المجاني إلى `RebornMap` عند فشل البوابة، وإصلاح `GetRebornMap` و`Passway` على الخرائط الديناميكية، وإضافة `default: return;` في `MsgPackage.cs` لمنع فتح مخازن الخرائط الأخرى عن بُعد بدون VIP.

---

## 4. متطلبات التشغيل الخارجية عند التجهيز على جهاز المالك

لكي يشتغل السورس فعليًا على Windows مع عميل `6192`، يحتاج التجهيزات المحلية التالية (جميعها محجوبة في `.gitignore` لضمان بقاء المستودع نظيفًا وآمنًا):

1. **ملفات الخرائط والـ `ini` من عميل `6192`:**
   - كل من `Canyon.Game` و `Canyon.Ai` يطلب عند الإقلاع قراءة `ini/GameMap.dat` وملفات الخرائط المضغوطة أو `.dmap` المشار إليها داخله (`MapDataManager.cs`)، وكذلك `ini/action_Res.ini`؛ تُنسخ مجلدات `ini/` و `map/` من عميل `6192` المتوافق إلى مجلد البناء المحلي (`src/Bin/net7.0/`).
2. **بيئة التشغيل (`.NET SDK / Runtime`):**
   - المشاريع تستهدف حاليًا `net7.0` (يمكن تثبيت `.NET 7.0 SDK` على Windows، أو ترقية ملفات المشاريع لاحقًا إلى `.NET 8.0` أو `.NET 10.0` عند رغبة المالك).
3. **قواعد البيانات وملفات الإعداد المحلية (`*.Config.local.json`):**
   - إنشاء قاعدتي بيانات نظيفتين على MariaDB (`127.0.0.1:3306`) مثل `canyon_account` و`canyon_game` دون المساس بأي قواعد بيانات أخرى على الجهاز، ووضع بيانات الاتصال محليًا فقط داخل `Canyon.Login.Config.local.json` و`Canyon.Game.Config.local.json` و`Canyon.Ai.Config.local.json`.
4. **تشغيل السيرفرات الثلاثة معًا:**
   - تشغيل `Canyon.Login` + `Canyon.Game` + `Canyon.Ai` بالتوازي (`Canyon.Ai` مسؤول بالكامل عن رسبون وحركة الوحوش).
5. **أجزاء فرعية مؤجلة في الكود الأصلي:**
   - المشاهدة والتصويت في أرينا الفرق (`TeamArenaQualifier.WatchAsync / WitnessExitAsync / WitnessVoteAsync`) وانسحاب `ElitePkGroupTournament.UnsubscribeAsync` ما زالت ترمي `NotImplementedException`.
   - دروب الوحوش يعمل حاليًا بالمنطق الثابت مع تعليق `TODO: Use Lua Script for item drops`.

---

## 5. الخلاصة النهائية

- المستودع الآن مخصص بالكامل لسورس `CanyonV6192` في الجذر مباشرة بتنظيم هندسي نظيف، ومحصّن عبر **35 ملف C#** ضد الأخطاء الخمسة الأولية وضد ثغرات التشفير الداخلي، وبورتات الربط غير المصادق عليها، وحقن SQL في الـ Actions، وهجمات استنزاف الاتصالات وتجميد البورت وغياب ختم `TQClient`، وهاكات السرعة (Speedhack) وحقن باكتات تحريك أو توريط اللاعبين والبوابات والمخازن البعيدة.
