# خريطة سورس RedMoon

## المدخل الواحد

افتح **`Open-RedMoon-Source.cmd`** أو **`src/RedMoon.sln`**. تظهر مجموعات Start here / Servers / Shared / Tools، وخلفها هذه المجلدات الفعلية:

```text
src/
├── RedMoon.sln
├── global.json
├── Directory.Build.props
├── .editorconfig
├── RedMoon.AccountServer/
│   ├── Program.cs
│   ├── ServerKernel.cs
│   └── Network/
│       ├── ClientTransport/      ← استقبال اتصالات تسجيل الدخول
│       └── GameLink/             ← اتصال خادم اللعبة الداخلي
├── RedMoon.GameServer/
│   ├── Program.cs
│   ├── ServerKernel.cs
│   ├── Database.cs              ← تحميل/ربط خدمات العالم، لا ملفات DB نفسها
│   ├── ServerStrings.cs
│   ├── Network/
│   │   ├── ClientTransport/
│   │   │   └── Handlers/        ← معالجة طلبات اللاعب
│   │   └── AccountLink/         ← ربط GameServer بالأكونت
│   ├── Gameplay/
│   │   ├── Entities/            ← الشخصية والوحوش وNPCs
│   │   ├── Items/               ← المعدات والجرد والمخازن
│   │   ├── World/               ← الخرائط والأرضيات والمناطق
│   │   ├── Society/             ← العلاقات والجماعات
│   │   ├── Qualifier/           ← Arena
│   │   ├── Events/
│   │   ├── Tournament/
│   │   ├── Actions/
│   │   └── Interfaces/
│   └── Runtime/
│       └── Workers/            ← مهام التشغيل والمعالجة الدورية
├── RedMoon.Data/
│   ├── Entities/
│   ├── Mappings/
│   ├── Repositories/
│   └── SessionFactory.cs
├── RedMoon.Core/
│   ├── Common/                 ← الإعدادات والمساعدات والـenums
│   ├── Networking/Packets/     ← شكل الباكت وأرقامه وحقوله
│   ├── Security/               ← التشفير، ومنها البديل المُدار
│   ├── ThirdParty/IniParser/   ← مكوّن ذو هوية/حقوق محفوظة
│   └── Compatibility/CO2Core/  ← تعريفات توافق تاريخية، لا DLL مجهولة
└── RedMoon.Admin/
    └── Program.cs
```

كل مجلد مشروع يحتوي ملفًا بنفس اسمه، مثل `RedMoon.GameServer.csproj`. كل مشاريع العمل موجودة مباشرة تحت `src`؛ لا توجد شجرة عمل ثانية باسم FoxConquer داخله.

## عايز تغيّر حاجة؟ ابدأ من هنا

- **الشخصية:** `RedMoon.GameServer/Gameplay/Entities/Character.cs`.
- **العناصر والجرد:** `Gameplay/Items/Item.cs` و`Inventory.cs` و`Equipment.cs` و`Warehouse.cs`.
- **NPC والحوار والأوامر:** `Gameplay/Actions`، ومعالجة رسالة الحوار في `Network/ClientTransport/Handlers/MsgTaskDialogHandler.cs`.
- **Arena:** `Gameplay/Qualifier`، وملفات معالجة رسائل الـArena في `Network/ClientTransport/Handlers`.
- **أحداث/حروب:** `Gameplay/Events` و`Gameplay/Tournament`؛ وجود ملف لا يعني أن كل سيناريوهاته مكتملة أو متحققة.
- **الخرائط:** `Gameplay/World`، وتحميل الداتا في `Database.cs`. أسماء الخرائط وداتا العميل ليست أسماء مشاريع يجوز تبديلها عشوائيًا.
- **الحفظ:** ابدأ بـ`RedMoon.Data/Entities`، ثم `Mappings` ثم `Repositories`. إعادة تسمية مشروع Data لم تغيّر أسماء جداول MariaDB.
- **الشبكة:** المعالجة في GameServer/AccountServer، وصيغة الرسائل والتشفير في Core.

ملفات الباكتات باسم النوع، مثل `MsgConnectEx.cs`؛ ملفات المعالجة مثل `MsgTaskDialogHandler.cs`. أرقام ومعرفات البروتوكول بقيت في التعريفات/attributes كما هي. لا تستبدل `Msg` أو `TQServer` أو أسماء الأعمدة جماعيًا لمجرد التسمية.

## لو كنت حافظ المسارات القديمة

- `src/FoxConquer5517/MsgServer` أصبح `src/RedMoon.GameServer`.
- `LoginServer` أصبح `RedMoon.AccountServer`، و`DB` أصبح `RedMoon.Data`، و`ServerCore` أصبح `RedMoon.Core`.
- `GameServer/Structures` أصبح `GameServer/Gameplay`، و`Threads` أصبح `Runtime`.
- namespaces المشاريع تتبع الأسماء الجديدة. حقل اتصال الأكونت داخل `ServerKernel` اسمه `AccountServerConnection`؛ ليس namespace.
- [سجل النقل](reference/REDMOON-RENAMES.json) يذكر النقل بالتتابع للمقارنة. الترويسات الأصلية تذكر أسماء تاريخية عمدًا لحفظ النسب؛ ليست إشارة لسورس آخر مطلوب تشغيله.

## ملفات التشغيل ليست ملفات C#

في **الحزمة الكاملة**:

```text
RedMoon-Source/
├── START-HERE.md
├── Open-RedMoon-Source.cmd
├── Migrate-RedMoon.cmd          ← انتقال من تثبيت موجود إلى هذا المجلد الجديد
├── Start-RedMoon.cmd
├── Stop-RedMoon.cmd
├── Build-RedMoon.cmd            ← تطوير فقط، في checkout جديد غير مثبت
├── src/                        ← كود العمل
├── setup/                      ← أدوات الإعداد والتشغيل
├── server/                     ← المخرجات ذاتية الاحتواء والداتا التشغيلية
│   ├── RedMoon.AccountServer.exe
│   ├── RedMoon.GameServer.exe
│   ├── RedMoon.Admin.exe
│   ├── RedMoon.Data.dll
│   └── RedMoon.Core.dll
├── database/                   ← SQL نظيفة لتثبيت جديد فقط؛ لا تعاود استيرادها
├── docs/
├── licenses/
├── tests/                      ← فحوص العمل، بالطلب فقط
└── .local/                     ← تنشأ محليًا بعد الإعداد/الانتقال؛ ليست ضمن التسليم
```

بعد الإعداد/الانتقال توجد `server/RedMoon.AccountServer.ini` و`server/RedMoon.GameServer.ini`. الخزنة المشفّرة في `.local`؛ Start يمرّر الأسرار كمتغيرات بيئة إلى العمليتين. logs القياسية في `.local/logs/RedMoon.AccountServer.stdout.log` و`RedMoon.GameServer.stdout.log` ونظيرتي stderr؛ السجلات التفصيلية تحت `server/Logs`.

تغيير C# لا يبدّل EXE القديمة. التجميع والنشر يتمان في مجلد جديد، والانتقال منفصل. لا تحاول إصلاح ذلك بتشغيل مشروع منفرد من IDE فوق سيرفر حي.

## ما الذي بقي خارج حزمة التشغيل؟

في GitHub، `vendor` أصل ثابت و`audit` أدلة تاريخية، و`downloads` أرشيف التنقل الصغير القديم. ليست نسخًا أخرى للعمل. الحزمة لا تنسخ الأصل أو ZIPs القديمة، لكنها تضم كل سورس العمل وmanifest المصدر المثبت وحقوقه. بيانات العميل/اللودر والأسرار وقواعد اللاعبين ليست ضمنها.
