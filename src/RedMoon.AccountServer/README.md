# RedMoon.AccountServer

تسجيل دخول اللاعب والتحقق من الحساب، ثم ربطه بخادم اللعبة.

- `Program.cs`: بدء العملية وقراءة `RedMoon.AccountServer.ini`.
- `ServerKernel.cs`: الحالة المشتركة.
- `Network/ClientTransport`: قبول اتصالات تسجيل الدخول.
- `Network/GameLink`: الربط الداخلي بخادم اللعبة.
- ملفات packet handlers والـpeers في `Network`.

المخرج: `RedMoon.AccountServer.exe`. شغّله عبر Start من جذر الحزمة مع GameServer، لا وحده بـF5. الباكتات والتشفير في Core، وسجلات/queries الحساب في Data. لا تغير credentials أو schema لمجرد تغيير اسم مشروع.
