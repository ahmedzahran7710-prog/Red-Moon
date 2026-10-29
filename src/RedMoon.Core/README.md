# RedMoon.Core

المكتبة المشتركة بين الخادمين وأداة الإعداد.

- `Networking`: sockets وتأطير الرسائل.
- `Networking/Packets`: صيغة باكتات5517، بأسماء ملفات تطابق أنواعها.
- `Security`: التشفير والبديل المُدار المستخدم بدل الـnative DLL القديمة.
- `Common`: مساعدات وenums و`RuntimeSettings.cs` لقراءة الإعدادات/الأسرار من البيئة.
- `ThirdParty/IniParser`: مكون parsing بهويته وحقوقه الأصلية.
- `Compatibility/CO2Core`: تعريفات توافق تاريخية، مع إبقاء namespaces الطرف الثالث.

المخرج: `RedMoon.Core.dll`. أسماء packet types والـopcodes والـfooters مفاتيح توافق وليست branding. لا تُعد تسمية كل رمز بحثًا عن RedMoon، ولا تعِد `libeay32.dll` القديمة إلى runtime.
