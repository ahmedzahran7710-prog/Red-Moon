# RedMoon.Admin

أداة محلية مساعدة للإعداد؛ ليست خادمًا ثالثًا ولا لوحة إدارة أو حساب GM.

- `Program.cs`: hash-password، وفحص تسجيل دخول اصطناعي مقيد ببيئة CI.
- الاعتماد المباشر على `RedMoon.Core` للتشفير/الباكتات.
- يستدعيها Setup عبر stdin خاص، وليس arguments تحمل password.

المخرج: `RedMoon.Admin.exe`. لا تطلب من المستخدم إرسال password في المحادثة، ولا تمنح GM/VIP تلقائيًا. تشغيل اللعبة اليومي يحتاج AccountServer وGameServer فقط عبر Start.
