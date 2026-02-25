# 🌸 Orkideya — متجر المنتجات الطبيعية

متجر إلكتروني متخصص في بيع الزيوت الطبيعية والصابون والمقشرات المصنوعة يدوياً، مبني بـ **ASP.NET Core MVC**.

---

## ✨ المميزات
- 🛍️ عرض المنتجات مع تصنيفات متعددة
- 🛒 سلة تسوق بتحديث فوري (AJAX)
- ✅ إتمام الطلب مع اختيار مدينة التوصيل
- 📲 إشعارات واتساب عند كل طلب
- ⭐ قسم آراء العملاء
- 👤 لوحة تحكم للمدير (إدارة الطلبات، المنتجات، التقييمات)
- 📱 تصميم متجاوب (Desktop + Mobile)
- 🎨 واجهة زجاجية Glassmorphism باللون البنفسجي

---

## ⚙️ متطلبات التشغيل
- .NET 8 SDK
- SQL Server (LocalDB أو SQLEXPRESS)

---

## 🚀 خطوات التشغيل

```bash
# 1. استنساخ المشروع
git clone https://github.com/YOUR_USERNAME/Orkideya.git
cd Orkideya

# 2. إعداد قاعدة البيانات
cp Orkideya/appsettings.Example.json Orkideya/appsettings.json
# عدّل appsettings.json وضع اسم السيرفر الخاص بك

# 3. تطبيق Migrations
cd Orkideya
dotnet ef database update

# 4. تشغيل المشروع
dotnet run
```

---

## 📁 هيكل المشروع
```
Orkideya/
├── Controllers/          # المتحكمات
├── Models/               # نماذج البيانات
├── Views/                # واجهات العرض
├── Areas/
│   ├── Admin/            # لوحة الإدارة
│   └── Identity/         # تسجيل الدخول والحساب
├── wwwroot/
│   ├── css/site.css      # التنسيقات الرئيسية
│   └── js/site.js        # السكريبتات
└── appsettings.Example.json  # قالب الإعدادات
```

---

## 🔒 الأمان
- لا ترفع ملف `appsettings.json` (مُضمَّن في .gitignore)
- انسخ `appsettings.Example.json` وعدّله محلياً

---

## 📄 الترخيص
خاص — جميع الحقوق محفوظة لـ Orkideya © 2025
