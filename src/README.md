# RedMoon — سورس العمل

افتح [RedMoon.sln](RedMoon.sln)، أو `Open-RedMoon-Source.cmd` من جذر الحزمة.

**التقسيم هنا فعلي، وليس أسماء عرض فقط:** اسم كل مجلد يطابق اسم مشروعه والـassembly الناتجة.

- [RedMoon.AccountServer](RedMoon.AccountServer): تسجيل دخول اللاعبين والتواصل مع خادم اللعبة. البداية `Program.cs`.
- [RedMoon.GameServer](RedMoon.GameServer): اللعب والعالم؛ البداية `Program.cs` ثم `Gameplay` و`Network` و`Runtime`.
- [RedMoon.Data](RedMoon.Data): تعريفات السجلات وربطها بالجداول وعمليات الحفظ/التحميل.
- [RedMoon.Core](RedMoon.Core): الشبكة والتشفير والباكتات والمساعدات المشتركة.
- [RedMoon.Admin](RedMoon.Admin): أداة الإعداد المساعدة، وليست خادمًا ثالثًا.

التجميع باستخدام SDK المثبتة في `global.json`، وخصائص التسمية المشتركة في `Directory.Build.props`. كل المشاريع تستهدف .NET 10. استخدم Visual Studio حديثًا يدعمها أو محررًا مناسبًا مع SDK؛ فتح الحل وحده لا يثبت توافق IDE أو نجاح البناء.

- [خريطة تفصيلية وأماكن الأنظمة](../docs/SOURCE-MAP.md).
- [الانتقال من النسخة القديمة](../docs/MIGRATION.md).
- [حدود التحقق](../docs/VERIFICATION.md).

لا تعمل على `vendor`؛ الأصل محفوظ للتوثيق فقط. لا تمسح ترويسات حقوق المؤلفين ولا تعِد تسمية جداول SQL أو معرفات الباكتات مع إعادة تسمية الكود. تشغيل اللعبة من `Start-RedMoon.cmd` لتجهيز أسرارها، وليس EXE/F5 مباشرة، ولا إعادة build فوق تثبيت حي.
