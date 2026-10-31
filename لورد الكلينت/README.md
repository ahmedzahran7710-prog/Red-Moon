# لودر الكلينت الرسمي — MoonQueen (`V6192`)

يحتوي هذا الفولدر على السورس الكامل والملفات الإعدادية الجاهزة للودر الرسمي الخاص بسيرفر **`MoonQueen`** (تطوير **`ahmedzahran`**)، والمتوافق 100% مع الكلينت **`6192`** وسورس السيرفر بتاعنا:
- يولّد ملف `Server.dat` المفكوك تلقائيًا باسم السيرفر **`MoonQueen`** وآي بي الهماشي **`25.21.212.130`** وبورتات **`9958`** و**`5816`**.
- يحقن مكتبة **`ConquerCipherHook.dll`** داخل `Conquer.exe` لتشفير كلمة المرور وإرسال باكت الدخول **`MsgAccount = 1542`** بنفس التشفير اللي بيستقبله سيرفر `MoonQueen.Login` بالظبط.
- واجهة اللودر وعنوان النافذة وبيانات المطور ونافذة `About` كلها مضبوطة بالكامل باسم **`MoonQueen`** والمطور **`ahmedzahran`**، بدون أي روابط أو نوافذ إعلانية خارجية عند فتح أو إغلاق اللعبة.

---

## 1. الطريقة السريعة للتشغيل داخل الكلينت (`Client V6192`)

1. فك ضغط الكلينت المتوافق الرسمي (`Client V6192` الموجود رابطه في ملف `متطلبات التشغيل.md`) في مسار مستقل ونظيف على جهازك مثل:
   - `F:\AhmedZahran\MoonQueen-Client`
2. لو لقيت جوه فولدر الكلينت ملف قديم اسمه `config.json.lock` امسحه، عشان اللودر يقرأ ملف `config.json` بتاعنا فورًا.
3. انسخ ملف **`config.json`** الجاهز من هذا الفولدر (`لورد الكلينت/config.json`) وحطه مباشرة جوّه فولدر الكلينت جنب `Conquer.exe`.
   - الملف مضبوط جاهزًا على اسم السيرفر **`MoonQueen`**، وعنوان اللودر **`MoonQueen`**، والوصف **`Welcome to MoonQueen (V6192) - Developed by ahmedzahran`**، وإصدار **`6192`**، وآي بي الهماشي **`25.21.212.130`**، وبورت الدخول **`9958`**، وبورت الجيم **`5816`**.
4. لبناء ملف التشغيل المسمى رسميًا **`MoonQueen.exe`** وهوك **`ConquerCipherHook.dll`** المحدثين باسمك:
   - افتح ملف الحل **`لورد الكلينت/ConquerLoader/MoonQueen.sln`** في **Visual Studio**.
   - ابنِ مشروع `ConquerCipherHook` على وضع **`Release | x86`** لإنتاج `ConquerCipherHook.dll`، وابنِ مشروع `MoonQueen` لإنتاج **`MoonQueen.exe`**.
   - انسخ **`MoonQueen.exe`** و**`ConquerCipherHook.dll`** الناتجين وحطهم جوه فولدر الكلينت جنب `Conquer.exe` و`config.json` (وممكن تمسح `ConquerLoader.exe` القديم من فولدر الكلينت عشان تفتح اللعبة دايمًا من **`MoonQueen.exe`**).
5. شغّل **`MoonQueen.exe`** كمسؤول (`Run as Administrator`) واضغط **`ENTER`** لبدء اللعب.

---

## 2. ملخص التعديلات المنفذة داخل اللودر (`loader-manifest.json`)

- تغيير اسم الحل إلى **`MoonQueen.sln`** واسم الملف التنفيذي الناتج إلى **`MoonQueen.exe`** مع الحفاظ الكامل على مسارات الـ namespaces والموارد الداخلية لضمان عدم تأثر استقرار البناء أو الـ WPF UI.
- تحديث كافة بيانات الملكية وحقوق التطوير في جميع المشاريع الخمسة وواجهات `WPF` و`WinForms` وملفات اللغة (`en.json`, `es.json`, `pt.json`) لتكون باسم **`MoonQueen`** والمطور **`ahmedzahran`**.
- تفعيل كود تشفير الباسورد لبروتوكول السورس (`Packet 1542` عند الإزاحة `60`) داخل `ConquerCipherHook/ConquerCipherHook.cpp` مع نسخ آمن بـ `strncpy_s` ومسح فوري لكلمة المرور من الذاكرة بـ `SecureZeroMemory`.
- إلغاء فتح المتصفح على موقع خارجي عند الخروج من الكلينت (`Shellexec` في `ConquerCipherHook.cpp`)، وإيقاف استدعاءات الـ API الخارجية غير اللازمة في `Constants.cs` و`PluginLoader.cs`.
- **تأمين متعدد الطبقات لحقوق المطور (`ahmedzahran`):**
  - خزنة مشفرة (`DeveloperRightsGuard` في `CLCore` + تحقق متبادل مستقل في `MoonQueen`) بتفك اسم المطور ونصوص الحقوق ديناميكيًا من بايتات مشفرة بـ `Rolling-XOR` وبتتحقق من بصمتها الرقمية `SHA-256` (`256-bit`) ومن خصائص الـ `AssemblyCompany` و`AssemblyCopyright` و`AssemblyProduct`، مع قفل تلقائي لنصوص الحقوق ضد أي تلاعب في `config.json` أو ملفات اللغات، وختم `FNV-1a 64-bit` أصيل بالـ C++ داخل `ConquerCipherHook.cpp`.
  - دعم أمر `MoonQueen.exe --lock-config` لتشفير `config.json` إلى `config.json.lock` بمفتاح `AES-256` خاص بالمطور `ahmedzahran` قبل توزيع الكلينت للاعبين.
