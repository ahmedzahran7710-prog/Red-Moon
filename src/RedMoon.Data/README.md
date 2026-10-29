# RedMoon.Data

مكتبة الوصول إلى البيانات، وليست ملفات DB الفعلية.

- `Entities`: أنواع السجلات.
- `Mappings`: ربط الخصائص بأسماء جداول/أعمدة SQL.
- `Repositories`: استعلامات وعمليات الحفظ/التحميل.
- `SessionFactory.cs`: تكوين جلسات NHibernate.

المخرج: `RedMoon.Data.dll`. تغيير اسم المكتبة/namespace لا يغير `redmoon_account` أو `redmoon_game` أو أسماء `cq_*`. لا تستخدم SQL التثبيت لتحديث سورس منشور أو لإعادة تسمية الجداول.
