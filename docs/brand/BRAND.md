# هوية أوركيديا البصرية — Orkidea Brand Guide

مرجع واحد للواجهة والتصميم والتسويق. أي تعديل على الألوان أو الخطوط أو الشعار يتم هنا أولاً ثم ينعكس في الكود (`frontend/assets/css/main.css`).
آخر تحديث: 2026-11-21. المصدر: الشعار وصور المنتجات وغلاف فيسبوك في `docs/brand-assets/reference/`.

## 1. الفكرة بسطر واحد
زيوت ومنتجات طبيعية من ليبيا، بإحساس هادئ وفاخر وبسيط: زهرة أوركيد بنفسجية، لافندر ناعم، دفء الكريمي والذهبي.
الزبون يفهم من أول نظرة: **منتج طبيعي، مضمون الشكل، وأقدر أطلبه بسهولة.**

## 2. الألوان
المصدر المعتمد: **ألوان موقع ستاجينج** (قرار المالك 2026-11-21). بنفسجي الشعار `#751D82` يبقى لون مزج داخل التدرجات ولون الشعار نفسه.

| الدور | التوكن | HEX | الاستعمال |
|---|---|---|---|
| أساسي | `brand` | `#6a1b9a` | العناوين، الأسعار، الأزرار، الروابط |
| داكن | `brand-deep` (و`plum` للتوافق) | `#4a148c` | بداية التدرجات، الفوتر، hover |
| فاتح | `brand-soft` | `#9c4dcc` | نهاية التدرجات، حدود، أيقونات |
| الشعار (مزج) | `orchid` | `#751D82` | لون الشعار؛ يدخل التدرج الأساسي فقط |
| لمسة | `lilac` | `#e0b0ff` | شريط الهيدر المتحرك، لمسات خفيفة |
| تمييز | `accent` | `#A70A78` | ماجنتا الملصق: شارات السعر والخصم وطرف تدرج النص فقط |
| لافندر | `lavender` / `lavender-deep` | `#f3e5f5` / `#e1bee7` | خلفيات الشرائح والحقول والفواصل |
| وردي ناعم | `blush` | `#fce4ff` | وسط تدرج الهيرو |
| خلفية الصفحة | `cream` | `#f8f4ff` | تقريباً أبيض بميل بنفسجي (لم يعد كريمي) |
| نجوم | `gold` | `#ffc107` | نجوم التقييم فقط |
| نص | `ink` / `ink-soft` | `#2A1530` / `#6B5A72` | النص الأساسي والمساعد |
| نجاح / خطر / تحذير / معلومة | `success` `danger` `warning` `info` | `#1b8a5a` `#c62828` `#b26a00` `#1565c0` | حالات النظام فقط |
| حالات الطلب (خلفيات) | `status-pending` `-processing` `-shipped` `-delivered` `-cancelled` | `#fff3cd` `#cfe2ff` `#d1ecf1` `#d4edda` `#f8d7da` | شارات `badge-status-*` |

قواعد: البنفسجي هو البطل. الماجنتا للتأكيد القليل. لا ألوان جديدة خارج التوكنات. تباين النص لا يقل عن 4.5:1.

## 3. الشعار
- ملفات المصدر: `Orkideya/wwwroot/images/logo.jpg` (الشعار الحالي)، وأيقونة الزهرة المربعة في `docs/brand-assets/reference/flower-icon-square.webp`.
- يُستعمل الشعار كصورة معتمدة كما هو؛ لا تُعاد كتابة "ORKIDEA" بخط عادي ولا يُعاد رسم الزهرة.
- الزهرة وحدها (بيضاء على بنفسجي) للأيقونة الصغيرة والـfavicon وصورة الملف الشخصي.
- مساحة حرة حول الشعار ≥ عرض البتلة. أقل ارتفاع للشعار الكامل 32px في الهيدر.
- الزهرة الشفافة: `frontend/public/brand/flower-white.png` (بيضاء، للخلفيات الداكنة) و`flower-purple.png` (بنفسجية #751D82 مشتقة من البيضاء، للخلفيات الفاتحة). الأصل الشفاف: `docs/brand-assets/reference/flower-white-transparent.png`.
- ما زال مطلوباً: نسخة شفافة من الشعار الكامل مع الكلمة (SVG/PNG)؛ لا تُستعمل صورة بخلفية مربعة فوق لون مختلف.

## 4. الخطوط
- العربية: **Cairo** (واجهة المتجر والإدارة)، أوزان 400/600/700.
- أرقام ولاتيني: نفس الخط.
- اسم أوركيديا بالإنجليزية يأتي من الشعار فقط.
- عنوان الصفحة 28–36px على الموبايل، النص 16px، سطر مريح 1.7.
- الاتجاه RTL دائماً، والمحاذاة لليمين.

## 5. الصور
- مشاهد المنتج: إضاءة جانبية ناعمة، خلفية لافندر/كريمي أو برقوقي داكن، مع عنصر من خامة المنتج (حبة سوداء، سمسم، رغوة).
- صور العبوات تُستعمل كما هي: الشكل والغطاء والملصق بما فيه الحجم والشعار ولا تُعدَّل.
- أبعاد: بطاقة المنتج 4:5، صفحة المنتج 4:5 مع معرض، بانر الرئيسية 16:9 وموبايل 4:5.
- صيغة WebP، `loading="lazy"`، نص بديل عربي واضح.
- المراجع: `docs/brand-assets/reference/` (غلاف فيسبوك، الزيتان معاً، الحبة السوداء، السمسم، الصابون، الزهرة).

## 6. الواجهة (UI)
- زوايا: بطاقة 20px (`radius-card`)، حقل 12px (`radius-field`)، أزرار وشارات 999px. ظلال بنفسجية: `shadow-soft` للبطاقات، `shadow-lift` عند المرور، `shadow-glow` للزر الرئيسي.
- `btn-primary`: كبسولة بتدرج `bg-gradient-brand`، نص أبيض، ارتفاع 48px، يرتفع 2px ويفتح عند hover، لمعة تمر عليه (فقط عند عدم تقليل الحركة)، وضغط 0.98.
- `btn-secondary`: حد بنفسجي 1.5px وخلفية شفافة، hover لافندر. `btn-ghost` للأفعال الثانوية.
- `card`: أبيض، حد بنفسجي 10%، ظل ناعم. `card-glass` لصفحات الدخول والسلة. `glass` للهيدر.
- `field`: حد 1.5px بنفسجي خفيف، خلفية `#fafafa`، عند التركيز حد بنفسجي وهالة 3px.
- `badge` و`badge-status-{pending,processing,shipped,delivered,cancelled}` للشارات؛ `chip` للاختيارات (الأحجام).
- `price` بوزن 800 بنفسجي. `section-title` 2.2rem/800 على الكمبيوتر و1.6rem على الموبايل، و`section-eyebrow` شارة صغيرة فوق العنوان.
- الحركة: `animate-fade-in-up`، `animate-float`، `animate-glow-pulse`، `animate-gradient-shift`، `animate-pulse-once` (عداد السلة)، `animate-confirm-bounce` و`animate-confirm-ring` (صفحة التأكيد)، وظهور عند التمرير بـ`.reveal` مع `useReveal`. كلها تتعطل عند `prefers-reduced-motion: reduce`. ممنوع `* { transition }` العام وتلاشي الصفحة كلها.
- بطاقة المنتج: صورة، اسم، الحجم، السعر بالدينار الليبي (د.ل)، زر "أضف للسلة". نجوم ذهبية للتقييم.
- لا ازدحام: هدف واحد لكل شاشة. الموبايل أولاً: اختبار 390px ثم 1440px بلا تمرير أفقي.

## 7. النبرة
لهجة ليبية بسيطة وقريبة، جمل قصيرة، إيموجي قليل. العنوان قصير والنص يعطي معلومة تساعد الزبون يختار أو يطلب. لا ادعاءات صحية أو علاجية غير مؤكدة؛ المكونات والاستعمالات والأحجام من بيانات المنتج المعتمدة فقط.

## 8. المنتجات المؤكدة حالياً
زيت الحبة السوداء (100 مل)، زيت السمسم (100 مل)، زيت جوز الهند، صابون الحبة السوداء. الأسعار والتفاصيل تُعتمد من لوحة الإدارة ولا تُكتب في الكود.

## 9. من الدليل إلى الكود
`frontend/assets/css/main.css` (Tailwind 4 `@theme`) هو التنفيذ. الأسماء القديمة (`brand`, `brand-soft`, `accent`, `lavender`, `lavender-deep`, `cream`, `plum`, `gold`, `ink`, `ink-soft`) باقية بقيم جديدة.

- ألوان: `--color-brand`, `-brand-deep`, `-brand-soft`, `-orchid`, `-lilac`, `-accent`, `-lavender`, `-lavender-deep`, `-blush`, `-cream`, `-plum`, `-gold`, `-ink`, `-ink-soft`, `-success`, `-danger`, `-warning`, `-info`, `-status-pending|processing|shipped|delivered|cancelled`.
- زوايا وظلال: `--radius-card`, `--radius-field`, `--radius-pill`, `--shadow-soft`, `--shadow-lift`, `--shadow-glow`.
- تدرجات (متغيرات `--gradient-brand|hero|band|text|strip|card` وكلاسات): `bg-gradient-brand`, `bg-gradient-hero`, `bg-gradient-band`, `bg-gradient-card`, `bg-gradient-strip` (مع `animate-gradient-shift`), `text-gradient-brand`.
- مكوّنات: `btn-primary`, `btn-secondary`, `btn-ghost`, `card`, `card-glass`, `glass`, `field`, `chip`, `badge`, `badge-status-*`, `price`, `section-title`, `section-eyebrow`, `storefront-action`.
- حركة: `animate-fade-in-up|float|glow-pulse|gradient-shift|pulse-once|confirm-bounce|confirm-ring`, `.reveal` / `.is-visible`، ومعها `frontend/composables/useReveal.ts`.

لا ألوان مكتوبة مباشرة في المكوّنات. عند اعتماد white-label لاحقاً تُقرأ هذه المتغيرات من إعدادات المتجر.

