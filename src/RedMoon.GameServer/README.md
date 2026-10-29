# RedMoon.GameServer

خادم العالم وأنظمة اللعب.

- `Program.cs`: بدء/إيقاف العملية وقراءة `RedMoon.GameServer.ini`.
- `ServerKernel.cs`: خدمات وحالة السيرفر؛ اتصال الأكونت `AccountServerConnection`.
- `Database.cs`: تهيئة مستودعات الداتا وتحميل العالم، وليس ملف قاعدة MariaDB.
- `Gameplay`: Entities / Items / World / Actions / Qualifier / Society / Events / Tournament.
- `Network/ClientTransport/Handlers`: معالجة طلبات اللاعب، بأسماء ملفات منتهية بـHandler.
- `Network/AccountLink`: ربط خادم الأكونت.
- `Runtime/Workers`: مهام المعالجة الدورية.

المخرج: `RedMoon.GameServer.exe`. لا تقفز لتغيير البيانات أو الباكتات بمجرد وجود اسم نظام في الكود؛ حدد المطلوب أولًا. التشغيل والإيقاف من ملفات الجذر يحافظان على البيئة والحفظ.
