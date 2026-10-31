# تقرير الفحص والتأمين وتخصيص سورس MoonQueen (`V6192` — المطور `ahmedzahran`)

> **مستوى الدليل:** فحص ثابت شامل للكود وجميع ملفات الأرشيف الأصلية الـ 863 بعد تنزيلها وفكها بالكامل، متبوعًا **بإصلاح جميع الأخطاء الخمسة الأولية (10 ملفات C#) ثم تطبيق 3 حزم تأمين شاملة للتشفير والشبكة والـ Anti-Cheat (25 ملف C#)، ثم تحويل هوية السورس واللعبة ورسائل الترحيب إلى `MoonQueen` والمطور `ahmedzahran` وتأمين حقوق المطور بخزنة وبصمات مشفرة `SHA-256`، ثم تنفيذ المرحلة الأولى لسد ثغرات الباسورد (`'z'`) وفخاخ الانهيار (`NotImplementedException`) وثغرات الأرينا والـ Elite PK، ثم بناء محرك الريتات والأدوات الإدارية الحية وتصفية جميع المعادلات على كونكر الأصلية النقية (`Pure Official V6192`) (إجمالي 84 ملفًا مُرقّعًا ومُخصصًا من أصل 845 ملفًا بحجم 6,393,292 بايت)** ومراجعتها مراجعة ثابتة مزدوجة سطرًا بسطر. لم يُنفذ Build أو اختبارات تشغيل أو استيراد SQL أو تشغيل عميل اللعبة التزامًا بسياسة المشروع الحالية.

## 1. هوية النسخة وتنظيمها في جذر المستودع

- اسم السيرفر والمشروع المعتمد: **`MoonQueen`** — المطور الرسمي: **`ahmedzahran`**.
- المستودع الأصلي المشتق منه: `https://github.com/darkfoxdeveloper/CanyonV6192`
- المرجع المثبت (Commit SHA): `d08d13187599c2275972b83b81ff8e66296da2b2`
- بصمة الأرشيف الكامل (SHA-256): `2d44a9cf25574c11d815541fbf260e33260e797e7bea1aff1ddb8f56985d8158`
- الترخيص الأصلي: MIT (`LICENSE` محفوظ بالبايت كما هو في جذر المستودع).
- إجمالي ملفات الأرشيف الأصلي: 863 ملفًا بحجم 53,090,052 بايت.
- ما دُمج في جذر المستودع مباشرة: **845 ملفًا** بحجم **`6,393,292` بايت** (يشمل كل ملفات C# الـ 786، وكل ملفات المشاريع التسعة `.csproj`، وملف الحل الرئيسي `MoonQueen.sln`، والترخيص `LICENSE`، وجميع ملفات سكربتات Lua الـ 33 داخل `RequiredAssetsForRun/lua/`)، منها **761 ملفًا مطابقة للأصل بالبايت** و**84 ملفًا مُرقّعًا ومُؤمّنًا ومُخصصًا لهوية وحماية واستقرار `MoonQueen`** مع الاحتفاظ ببصمة الأصل (`upstream_sha256` و`upstream_path`) وبصمة النسخة الحالية (`sha256`) في `canyon-manifest.json`.
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

## 3-ج. تحويل هوية السورس واللعبة واللودر إلى `MoonQueen` والمطور `ahmedzahran` ورسائل الترحيب

بناءً على قرار المالك، أُعيدت تسمية وتخصيص السورس واللعبة واللودر بالكامل إلى **`MoonQueen`** والمطور **`ahmedzahran`** مع الحفاظ الهندسي الكامل على استقرار البناء والموارد وبروتوكول الشبكة:

1. **إعادة تسمية الحل والمخرجات التنفيذية مع الحفاظ على `<RootNamespace>`:**
   - تحويل `Canyon.sln` إلى **`MoonQueen.sln`** وحذف إشارات ملفات الـ SQL المفقودة من Solution Items، وضبط `<AssemblyName>MoonQueen.*</AssemblyName>` وبيانات الملكية (`Authors`, `Company`, `Copyright = ahmedzahran`, `Product = MoonQueen`) في المشاريع الثمانية (`Login`, `Game`, `Ai`, `GM.Server`, `Database`, `Network`, `Shared`, `World`) بحيث تنتج ملفات `MoonQueen.Login.exe` و`MoonQueen.Game.exe` و`MoonQueen.Ai.exe` مع إبقاء `<RootNamespace>Canyon.*</RootNamespace>` ثابتًا لضمان عدم تأثر أي `using` أو مورد مضمّن مثل `Canyon.Game.Language.resources`.
   - دعم تحميل ملفات الإعداد `MoonQueen.{Login,Game,Ai,GM}.Config.local.json` مع إبقاء التوافق الرجعي التلقائي مع `Canyon.*.Config.local.json`، وتحديث أسماء ملفات التشغيل في `RealmService.cs` وعناوين نوافذ الكونسول وملفات اللوج (`MoonQueen.Login`, `MoonQueen.Game`, `MoonQueen.Ai`, `MoonQueen.GM`).

2. **رسائل الترحيب داخل اللعبة وتطهير نصوص `cq_action` القديمة تلقائيًا:**
   - إضافة رسائل الترحيب الرسمية (`StrWelcomeLoginTalk`, `StrWelcomeLoginTopLeft`, `StrWelcomeServerInfo`) في `Language.resx` و`Language.Designer.cs`، وإرسالها للاعب فور اكتمال الدخول (`ActionType.LoginComplete = 251` في `MsgAction.cs`) في أعلى الشاشة (`TopLeft` باللون الذهبي) وفي الشات، بالإضافة إلى أوامر اللاعبين `/server` و`/about` في `MsgTalk.cs`.
   - فحص وتطهير نصوص جدول `cq_action` تلقائيًا في الذاكرة داخل `EventManager.LoadActionsAsync()` لتحويل رسالة `1000000` (`Welcome to World Conquer Online!...`) وعبارات `World Conquer` والروابط الإعلانية القديمة (`worldconquer.online`) إلى هوية **`MoonQueen`** والمطور **`ahmedzahran`**، وتجاهل فتح المتصفح عند فراغ الرابط في `GameAction.ExecuteActionUserWebpageAsync`.

3. **تخصيص لودر الكلينت الرسمي (`لورد الكلينت/`):**
   - تحويل ملف حل اللودر إلى `لورد الكلينت/ConquerLoader/MoonQueen.sln` واسم الملف التنفيذي إلى **`MoonQueen.exe`**، وتحديث جميع مسارات موارد الـ WPF العشرة (`/MoonQueen;component/...`) في ملفات `.xaml` السبعة حتى تعمل واجهة الـ WPF بدون أي خطأ موارد (`XamlParseException`).
   - تحديث شاشات الواجهة ونافذة `About` وملفات اللغات وجميع ملفات `AssemblyInfo.cs` الخمسة وملف `config.json` باسم **`MoonQueen`** والمطور **`ahmedzahran`**، وإلغاء نافذة فتح المتصفح الإعلانية عند الخروج من الكلينت داخل `ConquerCipherHook.cpp`، مع الحفاظ التام على أختام بروتوكول كونكر الثابتة (`TQServer`, `TQClient`, `ANSWER_OK`, `NEW_ROLE`).

4. **نظام الحماية والتأمين المشفر لحقوق المطور `ahmedzahran` (`DeveloperRightsGuard`):**
   - **خزنة بايتات مموهة (`Rolling-XOR`) + بصمات `SHA-256` ثابتة الزمن:** تُركّب هوية المطور (`ahmedzahran`) واسم المشروع (`MoonQueen`) ونصوص الترحيب ونافذة `About` ديناميكيًا وقت التشغيل من مصفوفات بايت مشفرة دون الاعتماد على سلاسل نصية صريحة يسهل تعديلها بمحرر ستيني (`Hex Editor`) أو استبدال سريع، وتُفحص البصمة الرقمية `SHA-256` (`256-bit`) بمقارنة زمنية ثابتة عند الإقلاع وأثناء التشغيل.
   - **فحص خصائص الملفات المترجمة (`PE Assembly Metadata`) والموارد المضمّنة (`Language.resx`):** تتحقق السيرفرات الأربعة وبوابة الشبكة (`TcpServerListener`) واللودر (`MoonQueen.exe` <-> `CLCore.dll` بتحقق متبادل مزدوج متوافق مع `Costura.Fody`) من تطابق `AssemblyCompanyAttribute` و`AssemblyCopyrightAttribute` و`AssemblyProductAttribute` ونصوص `Language.resx` مع البصمة المشفرة للمطور `ahmedzahran`، وتوقف التشغيل فورًا (`Exit 77`) عند رصد أي عبث ببرامج مثل `Resource Hacker` أو `CFF Explorer` أو `dnSpy`.
   - **منع انتحال الحقوق عبر الداتابيز أو `config.json` أو ملفات اللغات + ختم C++ أصيل:** تثبيت السطر `1000000` في `cq_action` وتطهير أي عبارة انتحال تطوير غريبة (`Developed by / Created by / Coded by ...`) تلقائيًا لصالح `ahmedzahran`، وقفل مفاتيح الترجمة وواجهة اللودر وأوامر `/server`, `/about`, `/credits`, `/developer` على الخزنة المشفرة، واستبدال مفتاح `config.json.lock` العام بمفتاح `AES-256` خاص بالمطور (`--lock-config`)، وإضافة ختم `FNV-1a 64-bit` بالـ C++ داخل `ConquerCipherHook.cpp`.

---

## 3-د. المرحلة الأولى لسد ثغرات تشفير الباسورد وفخاخ الانهيار وثغرات الأرينا والـ Elite PK

1. **إصلاح جذري بدون فقد لتشفير/فك تشفير حرف `'z'` الصغير (`0x7A`) في باكت الدخول (`MsgAccount = 1542`):**
   - **السبب الجذري:** في خوارزمية `buffer::encrypt` ومصفوفة `pSeed` (سواء في `ConquerCipherHook` أو `MsgAccount.cs`)، يساوي ناتج الـ XOR التراكمي للأربع خطوات `0x00` في الخانات الزوجية (`0, 2, 4, ...`) و`0x7A` (`'z'`) في الخانات الفردية (`1, 3, 5, ...` مثل الخانة الخامسة في `ahmedzahran1`). وجود حرف `'z'` في خانة فردية ينتج بايت مشفرًا قيمته `0x00`، فكان شرط `if (buffer[x] != 0)` في `MsgAccount.DecryptPassword` يتخطاه ويتركه `\0` فيُفسد كلمة المرور!
   - **الإصلاح المطبق:** حفظ علامة الطول الدقيقة غير القابلة للتصادم `(0xA0 | (pwLen & 0x1F))` في `buf[91]` (`buffer[31]` المتروكة دائمًا `0x00` في كل الباكتات القديمة بطول `0..31` حرفًا) داخل `ConquerCipherHook.cpp` مع حراسة `len >= 92` وحراسة `password != NULL` في `csv3_snprintf`، وفك التشفير في `MsgAccount.cs` لجميع البايتات `0 .. pwLen - 1` بلا شروط على مصفوفة مستقلة دون تشويه البافر الأصلي، مع توافق رجعي كامل مع أي نسخة `ConquerCipherHook.dll` قديمة (`buffer[31] == 0x00`) عبر مسح آخر بايت غير صفري في `0..30`.

2. **القضاء الكامل على جميع فخاخ `NotImplementedException` القابلة للوصول وإغلاق ثغرات الأرينا والـ Elite PK:**
   - **مشاهدة وتصويت أرينا الفرق (`TeamArenaQualifier.cs`, `TeamArenaQualifierMatch.cs`, `MsgArenicWitness.cs`):** استكمال دوال `WatchAsync` و`WitnessExitAsync` و`WitnessVoteAsync` و`FindMatchByTarget` و`Wave` بالكامل، وربط باكت `MsgArenicWitness` ليوجه طلبات المشاهدة والتشجيع والخروج تلقائيًا بين الأرينا الفردية وأرينا الفرق، وإصلاح استعادة وضع الـ PK الأصلي للفريق الثاني (`company.PreviousPkModes` بدل خطأ النسخ `Company1.PreviousPkModes`).
   - **إغلاق ثغرة الهروب من السجن (`BotJail`) عبر مشاهدة الأرينا (`ArenaQualifier.cs`, `TeamArenaQualifier.cs`):** منع استخدام باكت `MsgArenicWitness(RequestView)` للهروب من السجن (`IsPrisionMap()`) أو الخرائط الممنوعة من الانتقال (`IsTeleportDisable()`) أو الخرائط الديناميكية (`IsDynamicMap()`) أو أثناء التجارة (`Trade != null`) أو التسجيل النشط في الأرينا.
   - **استكمال انسحاب `ElitePkGroupTournament` وإصلاح تصادم خرائط القوالب (`ElitePkGroupTournament.cs`, `ElitePkTournament.cs`, `TeamArenaQualifier.cs`):** استكمال نسختَي `UnsubscribeAsync` وربط `ElitePkTournament.OnExitAsync` وإرجاع الفائز لإحداثيات صالحة على `awaitingMap` بدل `match.Map`، وتعديل بداية عداد الخرائط الديناميكية في كلٍّ من `TeamArenaQualifier` (`940001..949999`) و`ElitePkTournament` (`910001..919999`) لمنع حذف خريطة القالب الأساسية (`940000` و`910000`) بعد انتهاء أول مباراة!

3. **المرحلة 3 — محرك الريتات والدروب الحي (`ServerRatesManager`) واستوديو التحكم الفوري (`لوحة تحكم السيرفر/`) وإصلاح علة `Datastr0`:**
   - **إصلاح خطأ نسخ في `DynamicGlobalDataManager.ChangeStringData`:** كان `case 0:` يكتب بالخطأ في `data.Datastr1` بدل `data.Datastr0`؛ تم تصحيحه إلى `data.Datastr0 = value;`.
   - **محرك الريتات والريلود اللحظي بدون قفل السيرفر (`ServerConfiguration.cs`, `DynamicGlobalDataManager.cs`, `BasicThread.cs`, `Character.cs`, `MagicData.cs`, `Monster.cs`, `Item.cs`, `Program.cs`, `MsgTalk.cs`):** ربط مضاعفات الخبرة (`ExperienceRate`)، وخبرة السحر (`MagicExpRate`)، وبروف السلاح (`WeaponSkillExpRate`)، وكمية ونسبة الفضة (`SilverDropRate`, `SilverDropChancePercent`)، وكثافة وجودة الدروب (`EquipmentDropRate`, `QualityItemRate`)، وسوكت الأسلحة وقطع `+1` من الوحوش (`SocketDropRate`, `PlusOneDropRate`)، والدراجون بول (`DragonBallDropRate`)، والميتيور الإضافي (`MeteorDropPer100k`)، وإيفنت CPs الوحوش (`MonsterCpChancePercent`, `MonsterCpMin..Max`) بلقطة ذرية محمية (`volatile ServerRatesConfiguration` مع `CloneNormalized()` لمنع أي Overflow أو قيم سالبة)، تُفحص تلقائيًا كل ثانية من `MoonQueen.Rates.json` مع كتابة نبض الحالة في `MoonQueen.Rates.status.json` ودعم الإعلان العالمي الفوري وريلود `cq_action` + `LuaScriptManager.Reload()` وأوامر `/rates` و`/reloadrates`.
   - **استوديو `MoonQueen Control Studio` (`لوحة تحكم السيرفر/`):** واجهة رسومية محلية محمية (`127.0.0.1` فقط + `Bcrypt` + `CSRF` + ختم حقوق `ahmedzahran`) مع مشغّل ضغطة زر `تشغيل_لوحة_التحكم.bat` ومزامنة تلقائية لمجلدَي `Debug` و`Release`.

4. **تصفية وضبط جميع معادلات الدروب والقتال والخبرة والمهارات والترقيات على أساس كونكر الأصلية النقية 100% (`Pure Official Conquer V6192 — 1.0x`):**
   - **الدراجون بول والفضة من الوحوش (`Monster.cs`):** إزالة المضاعف الخاص المخبأ `200x` من دروب الدراجون بول ليصبح على النسبة الرسمية النقية لكونكر أونلاين `625 / 54,000,000` (`~1` كل `86,400` وحش عادي على الأرض، مع منع سقوطه من الحراس أو الوحوش ذات الاسم الأخضر وإلغاء التجميع الخفي التلقائي في Scroll)، وإصلاح خطأ مضاعفة الحد الأدنى للفضة (`moneyMin + NextAsync(moneyMin, moneyMax)`) ليعطي النطاق الرسمي الصحيح `85%..115%` من `monsterType.DropMoney`.
   - **نسب سقوط السوكت والـ `+1` والـ Bless من الوحوش (`Item.cs`):** إلغاء النسب الخاصة المضخمة (`5%` لـ 2-Socket و`15%` لـ 1-Socket و`2%` لـ `-5%` Bless و`3%` لـ `-3%` Bless)، واستعادة النسب الرسمية النقية لكونكر أونلاين عند `1.0x`: الأسلحة الساقطة `100 / 100,000` (`0.10%` لـ 1-Socket) و`5 / 100,000` (`0.005%` لـ 2-Socket)، وقطع `+1` بنسبة `10 / 1,000` (`1.0%`)، والـ Bless النادر `-1%` فقط بنسبة `1 / 1,000` (`0.1%`).
   - **خبرة التلفيل وبروف السلاح وخبرة المهارات (`Character.cs`, `BattleSystem.cs`, `MagicData.cs`, `MagicDataProcessing.cs`):** حذف مضاعف `battleExpTax = 5` والـ `+1` الزائد من معادلة `BattlePower / 100d` في `Character.AwardBattleExpAsync` (كانت تضخم الخبرة `10x+`)، واستعادة معادلة بروف السلاح الرسمية `nExp / 10` بدل `nExp / 3` في الضرب العادي و`TripleAttack`، وإصلاح قسمة الأعداد الصحيحة الصفرية `5 / 100` في `MagicData.AwardExpOfLifeAsync`، وتوحيد بونص قتل الوحش بالمهارات على `5%` من `MaxLife` وإصلاح خطأ إضافة بونص القتل إلى خبرة المهارة بدل خبرة الشخصية في 3 مهارات داخل `MagicDataProcessing.cs`.
   - **معادلات الضرب الفيزيائي والسحري والـ Magic Defense والمهارات (`BattleSystem.cs`, `Item.cs`, `CharacterBattle.cs`, `MagicDataProcessing.cs`):** إصلاح خطأ الطرح `MinAttack - NextAsync(...)` إلى الجمع `MinAttack + NextAsync(...)` في `CalcAttackPowerAsync`، وإصلاح قسمة `MagicDefenseBonus` على `100d` بدل `200d` مع معادلة الـ `Penetration` النسبية الصحيحة واستعادة الـ `MagicDef` الأساسي لشنط التاويست (`Necklace`)، وإزالة تعديلات `TripleAttack` المخصصة (`1000ms` الثابتة ونسبة `33.33%` في الـ PvP) للرجوع لقيم `cq_magictype` الرسمية، وإصلاح تمرير الضرر الفعلي (`damage` / `displayDamage`) في `FastBlade` / `ScentSword` ومهارتين إضافيتين.
   - **سوكت الترقية بالميتيور والدراجون بول وبداية الشخصية الجديدة (`Item.cs`, `MsgItem.cs`, `MsgDataArray.cs`, `MsgRegister.cs`):** توحيد فرصة فتح السوكت عبر `TryRollUpgradeSocketAsync` عند نجاح الترقية فقط (`0.5%` للسوكت الأول و`0.15%` للسوكت الثاني) وإصلاح خطأ خصم باقي الـ DragonBall Scroll (`gemCost` بدل `totalGems`) وإزالة رسائل الديباج من الشات في `MsgDataArray.cs`، وضبط إنشاء الشخصية الجديدة لفل 1 على البداية الرسمية النقية (`Silver = 100` و`HeavenBlessing = 0` بدل `10,000` فضة و30 يوم بليسينج مجاني).

5. **حزمة الأدوات الإدارية الفورية الأربع بالترتيب داخل `MoonQueen Control Studio` (`C-012` — 11 ملف C# + `لوحة تحكم السيرفر/`):**
   - **الأداة #1 — استوديو المسابقات والحروب الحية (`EventManager.cs`, `ElitePkTournament.cs`, `DynamicNpc.cs`, `CaptureTheFlag.cs`, `FamilyWar.cs`, `LineSkillPK.cs`, `QuizShow.cs`, `HorseRacing.cs`):** إزالة حواجز `#if DEBUG` عن `LineSkillPK` و`ArenaQualifier` و`ElitePkTournament` و`TeamArenaQualifier` في `EventManager.cs`، وإخراج `StartAsync`/`StopAsync` من `#if DEBUG` في `ElitePkTournament.cs` ليعمل البناء النهائي `Release` بكامل بطولاته، مع إضافة دوال تشغيل وإنهاء فوري محمية من إلغاء المؤقت الدوري لعمود `Guild War` (`1038`) والبطولات الست، وإصلاح `NullReferenceException` كامنة في `LineSkillPK.RewardsAsync()` عند انتهاء البطولة بلا ضربات.
   - **الأداة #2 — محرر المتاجر والـ Mall والشوب الفوري (`ItemManager.cs`, `DynamicGlobalDataManager.cs`):** إضافة `ReloadShopsAndPricesAsync()` و`BuildShopsSnapshot()` لتعديل وإضافة وحذف سلع `cq_goods` وأسعار `cq_itemtype` (`Price`, `EmoneyPrice`, `BoundEmoneyPrice`, `HonorPrice`, `RidingPrice`) وعمل Hot-Reload للذاكرة الحية في أقل من ثانية دون ريستارت، مع مولد تلقائي لسطور `ini/emoneyShopV2.ini` و`ini/Shop.dat` للسلع الجديدة كليًا.
   - **الأداة #3 — رادار اللاعبين المتصلين وصندوق الهدايا والأوامر الفوري (`DynamicGlobalDataManager.cs`):** بث حي لقائمة المتصلين مع أوامر فورية لإرسال العملات والـ VIP وبركة السماء وقطع العتاد المخصصة (`+12 / -7 / 255 / 2-Soc`) للاعب محدد أو لجميع المتصلين بضغطة واحدة، بالإضافة إلى أوامر الإنعاش الفوري وتصفير الـ PK والنقل وسجن البوتات (`6002`) والطرد الآمن.
   - **الأداة #4 — صانع غزوات الزعماء والوحوش الفوري (`RoleManager.cs`, `DynamicGlobalDataManager.cs`):** استدعاء أي زعيم أو موجة وحوش عبر باكت `MsgAiSpawnNpc` إلى `MoonQueen.Ai` مع انتشار دائري اختياري وإعلان ذهبي عالمي ورادار حي لدم الزعماء الأحياء ومسح فوري عبر `LeaveMapAsync()`.

---

## 4. متطلبات التشغيل الخارجية عند التجهيز على جهاز المالك

لكي يشتغل السورس فعليًا على Windows مع عميل `6192`، يحتاج التجهيزات المحلية التالية (جميعها مشروحة خطوة بخطوة في `متطلبات التشغيل.md` ومحجوبة في `.gitignore` لضمان بقاء المستودع نظيفًا وآمنًا):

1. **ملفات الخرائط والـ `ini` من عميل `6192`:**
   - كل من `MoonQueen.Game.exe` و `MoonQueen.Ai.exe` يطلب عند الإقلاع قراءة `ini/GameMap.dat` وملفات الخرائط المضغوطة أو `.dmap` المشار إليها داخله (`MapDataManager.cs`)، وكذلك `ini/action_Res.ini`؛ تُنسخ مجلدات `ini/` و `map/` من عميل `6192` المتوافق إلى مجلد البناء المحلي (`src/Bin/net7.0/`).
2. **بيئة التشغيل (`.NET SDK / Runtime`):**
   - المشاريع تستهدف حاليًا `net7.0` (يمكن تثبيت `.NET 7.0 SDK` على Windows، أو ترقية ملفات المشاريع لاحقًا إلى `.NET 8.0` أو `.NET 10.0` عند رغبة المالك).
3. **قواعد البيانات وملفات الإعداد المحلية (`MoonQueen.*.Config.local.json`):**
   - إنشاء قاعدتي بيانات نظيفتين على MariaDB (`127.0.0.1:3306`) باسم `moonqueen_account` و`moonqueen_game` دون المساس بأي قواعد بيانات أخرى على الجهاز، ووضع بيانات الاتصال محليًا فقط داخل `MoonQueen.Login.Config.local.json` و`MoonQueen.Game.Config.local.json` و`MoonQueen.Ai.Config.local.json`.
4. **تشغيل السيرفرات الثلاثة معًا:**
   - تشغيل `MoonQueen.Login.exe` + `MoonQueen.Game.exe` + `MoonQueen.Ai.exe` بالتوازي (`MoonQueen.Ai` مسؤول بالكامل عن رسبون وحركة الوحوش)، ثم تشغيل الكلينت عبر `MoonQueen.exe`.
5. **التحكم الحي الشامل (`MoonQueen Control Studio`):**
   - جميع الريتات والمعادلات مضبوطة افتراضيًا على الأساس الرسمي النقي 100% لكونكر أونلاين `Pure Official Conquer (1.0x)` بدون أي زيادات خفية، ويمكن إدارة الريتات والمسابقات والحروب والمتاجر واللاعبين وغزوات الزعماء وتطبيقها في أقل من ثانية بدون ريستارت عبر `لوحة تحكم السيرفر/تشغيل_لوحة_التحكم.bat`.

---

## 5. الخلاصة النهائية

- المستودع الآن مخصص بالكامل لسيرفر ولودر وموقع ولوحة تحكم **`MoonQueen`** (تطوير **`ahmedzahran`**) في الجذر مباشرة بتنظيم هندسي نظيف، ومحصّن ومُخصص ومضبوط على الأساس الرسمي النقي لكونكر عبر **84 ملفًا في السيرفر** (`6,393,292` بايت) و**34 ملفًا في اللودر** (`7,775,979` بايت) ضد الأخطاء البرمجية الحرجة، وثغرات التشفير الداخلي، وبورتات الربط غير المصادق عليها، وحقن SQL في الـ Actions، وهجمات استنزاف الاتصالات وتجميد البورت وغياب ختم `TQClient`، وهاكات السرعة (Speedhack) وحقن باكتات تحريك أو توريط اللاعبين والبوابات والمخازن البعيدة، وعلة اقتطاع حرف `'z'` في كلمات المرور (`MsgAccount = 1542`)، وجميع فخاخ `NotImplementedException` وثغرة الهروب من السجن عبر مشاهدة الأرينا وتصادم خرائط البطولات، مع مركز قيادة رسومي فوري (5 استوديوهات حية بدون ريستارت) وحماية مشفرة متعددة الطبقات لحقوق المطور **`ahmedzahran`** ضد العبث أو التعديل.
