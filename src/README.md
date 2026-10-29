# Red Moon — الكود الذي نعدله

افتح **[RedMoon.sln](RedMoon.sln)** لعرض المشاريع بأسماء واضحة، أو استخدم `Open-RedMoon-Source.cmd` من جذر السورس.

- **GameServer** في الحل ← `FoxConquer5517/MsgServer/MsgServer.csproj`.
- **AccountServer** في الحل ← `FoxConquer5517/LoginServer/LoginServer.csproj`.
- **Database** ← `FoxConquer5517/DB/DB.csproj`.
- **ServerCore** ← `FoxConquer5517/ServerCore/ServerCore.csproj`.
- **RedMoon.Admin** ← `RedMoon.Admin/RedMoon.Admin.csproj`، أداة مساعدة وليست سيرفرًا ثالثًا.

نقطتا البداية: `Program.cs` داخل MsgServer وLoginServer. أسماء العرض في الحل لا تغير أسماء الملفات التنفيذية أو namespaces.

[خريطة السورس وطريقة التشغيل والتحديث](../docs/SOURCE-MAP.md)

التشغيل المعتاد من `Start-RedMoon.cmd` في الجذر، والإيقاف من `Stop-RedMoon.cmd`. لا تبنِ فوق نسخة مثبتة/شغالة ولا تعاود استيراد SQL لتحديث كود. الاختبارات لا تُشغّل إلا بطلب المالك.
