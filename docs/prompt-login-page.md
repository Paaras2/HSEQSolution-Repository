# پرامپت بازسازی صفحه‌ی ورود

این فایل یک پرامپتِ آماده است. کل بخشِ زیرِ خطِ جداکننده را کپی کنید و به دستیار کدنویسیِ
پروژه‌ی مقصد بدهید. اول «متغیرهای پروژه» را با مقادیر پروژه‌ی جدید پر کنید؛ اگر پر نکنید،
همان صفحه‌ی ورودِ سامانه‌ی HSEQ عیناً ساخته می‌شود.

---

# نقش

تو یک مهندس Front-end و طراح UI هستی. قرار است **یک صفحه‌ی ورود (Login Page)** را دقیقاً
مطابق مشخصاتِ زیر پیاده‌سازی کنی. مشخصات کامل است؛ چیزی را از خودت اضافه یا حذف نکن مگر
جایی که صریحاً گفته شده «اختیاری».

# متغیرهای پروژه (اول این‌ها را پر کن)

| متغیر | مقدار |
|---|---|
| `{{APP_TITLE}}` | مدیریت یکپارچه مدارک و مستندات |
| `{{APP_SUBTITLE}}` | برای ادامه، با کد پرسنلی خود وارد شوید |
| `{{BRAND_TITLE}}` | سامانه‌ی مدیریت مدارک و مستندات |
| `{{BRAND_ORG}}` | شرکت طراحی و ساختمان نفت |
| `{{BRAND_LOGO_FULL}}` | `/brand/logo-full.png` (لوگوی کامل، برای ماسکِ سفید) |
| `{{BRAND_LOGO_ICON}}` | `/brand/logo-icon.png` (نشانِ کوچک، برای بافتِ تکرارشونده) |
| `{{IDENTIFIER_LABEL}}` | کد پرسنلی |
| `{{FEATURE_1}}` | نسخه‌بندی و بازنگری کنترل‌شده |
| `{{FEATURE_2}}` | جست‌وجوی پیشرفته در همه‌ی مدارک |
| `{{FEATURE_3}}` | دسترسی نقش‌محور و کنترل‌شده |
| `{{CREDIT}}` | طراحی و توسعه، فناوری اطلاعات، ارتباطات و حکم رانی داده |
| `{{VERSION}}` | ۱.۰ |
| `{{AFTER_LOGIN_PATH}}` | `/documents` |

# قوانین

- **هیچ پکیج جدیدی نصب نکن.** آیکون‌ها را به‌صورت SVG درون‌خطی بنویس (نه کتابخانه‌ی آیکون).
  انیمیشن‌ها CSS خالص‌اند (نه کتابخانه‌ی انیمیشن).
- زبان رابط **فارسی** و جهت صفحه **RTL** است. برای فاصله‌ها و موقعیت‌ها از خواص منطقی
  استفاده کن (`padding-inline-start`، `inset-inline-end`، …) نه فیزیکی (`left`/`right`)،
  تا اگر روزی جهت به LTR تغییر کرد، چیدمان خودش برگردد.
- ارقامِ نمایشی فارسی باشند (۰۱۲۳۴۵۶۷۸۹)، ولی ورودیِ کاربر پیش از ارسال به سرور به ارقام
  لاتین تبدیل شود.
- برای هر بخش از کد یک **کامنت کوتاه فارسی** بنویس که بگوید «چرا»، نه «چه».
- CSS را در یک شیت مشترک بنویس، نه inline style و نه CSS-in-JS.

# ساختار DOM

```
.login-page                     ← ظرفِ تمام‌صفحه، مرکزچین، overflow: hidden
├── .login-aurora               ← لایه‌ی پس‌زمینه، aria-hidden، pointer-events: none
│   ├── span.login-aurora__orb.login-aurora__orb--1
│   ├── span.login-aurora__orb.login-aurora__orb--2
│   └── span.login-aurora__orb.login-aurora__orb--3
└── .login-shell                ← کارت دوستونی، z-index: 1
    ├── .login-panel            ← ستون فرم (سمت شروع = راست در RTL)
    │   ├── .login-panel__head  ← نشان + عنوان + زیرعنوان
    │   ├── form.login-form     ← خطا + دو فیلد + دکمه‌ی ورود
    │   └── p.login-panel__credit
    └── aside.login-hero        ← ستون برند (سمت پایان = چپ در RTL)
        ├── .login-hero__logo   ← لوگوی تک‌رنگِ سفید، role="img" + aria-label
        ├── .login-hero__text   ← h2 + p
        └── ul.login-hero__features  ← سه قابلیت با آیکون
```

# پس‌زمینه: «شفق» (Aurora)

چهار لایه، همه بی‌لبه و کم‌کنتراست، تا رنگ و عمق بدهند ولی کارت نقطه‌ی تمرکز بماند.

**۱. متغیرهای محلی روی `.login-page`** — حالت تیره فقط همین‌ها را بازنویسی می‌کند:

```css
.login-page {
  --login-bg-from: #f4f8fa;
  --login-bg-to: #d9e6ed;
  --login-orb-1: rgb(0 140 191 / 0.34);   /* آبی برند */
  --login-orb-2: rgb(11 118 124 / 0.28);  /* فیروزه‌ای */
  --login-orb-3: rgb(0 150 84 / 0.3);     /* سبز برند */
  --login-dot: rgb(44 76 92 / 0.13);
  --login-vignette: rgb(44 76 92 / 0.12);

  position: relative;
  min-height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: clamp(16px, 4vh, 40px) 20px;
  overflow: hidden;
  background: linear-gradient(155deg, var(--login-bg-from) 0%, var(--login-bg-to) 100%);
}

:root[data-theme="dark"] .login-page {
  --login-bg-from: #111d26;
  --login-bg-to: #0a1116;
  --login-orb-1: rgb(0 140 191 / 0.32);
  --login-orb-2: rgb(62 198 205 / 0.2);
  --login-orb-3: rgb(0 150 84 / 0.26);
  --login-dot: rgb(255 255 255 / 0.08);
  --login-vignette: rgb(0 0 0 / 0.45);
}
```

**۲. شبکه‌ی نقطه‌ای** روی `::before` — بافتِ ریز که به سمت پایین محو می‌شود:

```css
background-image: radial-gradient(var(--login-dot) 1px, transparent 1px);
background-size: 22px 22px;
mask-image: radial-gradient(120% 95% at 50% 0%, #000 0%, transparent 76%);
/* نسخه‌ی -webkit- را هم بنویس */
```

**۳. وینیت** روی `::after` — لبه‌های صفحه یک پله تیره‌تر:

```css
background: radial-gradient(130% 100% at 50% 45%, transparent 42%, var(--login-vignette) 100%);
```

**۴. سه هاله‌ی شناور** داخل `.login-aurora` (که `position: absolute; inset: 0; overflow: hidden`
است). هر هاله `border-radius: 50%`، `filter: blur(90px)`، `will-change: transform`:

| هاله | موقعیت | اندازه | رنگ | انیمیشن |
|---|---|---|---|---|
| ۱ | `inset-inline-start: -10%; top: -16%` | `min(56vw, 620px)` | `--login-orb-1` | `26s` |
| ۲ | `inset-inline-end: -12%; bottom: -18%` | `min(52vw, 560px)` | `--login-orb-2` | `32s` |
| ۳ | `inset-inline-start: 38%; bottom: -26%` | `min(44vw, 460px)` | `--login-orb-3` | `38s` |

انیمیشن‌ها `ease-in-out infinite alternate` هستند و فقط `transform` را تکان می‌دهند:

```css
@keyframes login-orb-1 { from { transform: translate3d(0,0,0) scale(1); }    to { transform: translate3d(7%, 9%, 0) scale(1.12); } }
@keyframes login-orb-2 { from { transform: translate3d(0,0,0) scale(1.08); } to { transform: translate3d(-8%, -7%, 0) scale(1); } }
@keyframes login-orb-3 { from { transform: translate3d(0,0,0) scale(1); }    to { transform: translate3d(-6%, -11%, 0) scale(1.16); } }
```

دو قاعده‌ی پایانی الزامی است:

```css
@media (max-width: 720px) { .login-aurora__orb { filter: blur(64px); } }
@media (prefers-reduced-motion: reduce) { .login-aurora__orb { animation: none; } }
```

# کارت دوستونی

```css
.login-shell {
  position: relative;
  z-index: 1;
  width: min(940px, 100%);
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 0.92fr);  /* فرم کمی پهن‌تر از برند */
  border-radius: 22px;
  overflow: hidden;                    /* گوشه‌های گرد، گرادیانتِ ستون برند را می‌برد */
  border: 1px solid var(--color-border);
  box-shadow: 0 28px 70px rgb(var(--shadow-rgb) / 0.24), 0 2px 8px rgb(var(--shadow-rgb) / 0.08);
}
```

ستون فرم **اول** در DOM می‌آید (سمت شروع = راست در RTL)، چون مسیر خواندن فارسی از آنجا
آغاز می‌شود.

# ستون فرم

```css
.login-panel {
  display: flex;
  flex-direction: column;
  gap: 18px;
  padding: 34px 32px 22px;
  /* شیبِ بسیار ملایم به‌جای سطحِ تخت، تا ستون فرم بی‌جان نباشد */
  background: linear-gradient(180deg, var(--color-surface) 0%, var(--color-surface-2) 100%);
}
```

- **سربرگ** (`.login-panel__head`): ستونی، وسط‌چین، `gap: 6px`.
  - نشان: `<img>` با `alt=""` و ارتفاع `52px`.
  - `h1`: `19px`، `text-wrap: balance`، متن `{{APP_TITLE}}`.
  - زیرِ `h1` یک **خطِ لهجه** با `::after`: عرض `34px`، ارتفاع `3px`، `border-radius: 2px`،
    رنگ `#f04f24`، `margin: 10px auto 0`. تنها جای استفاده از نارنجی در این صفحه است —
    برای متن یا دکمه به کارش نبر، چون کنتراستش روی سفید به AA نمی‌رسد.
  - `p`: `13px` با رنگ متنِ کم‌رنگ، متن `{{APP_SUBTITLE}}`.

- **فرم** (`.login-form`): ستونی با `gap: 14px`، دارای `noValidate`.
  - جعبه‌ی خطا (فقط وقتی خطا هست): `role="alert"`، پس‌زمینه‌ی نرمِ قرمز، متن قرمز، `13px`.
  - دو فیلد در قالب `.field` (ستونی، `gap: 6px`) با `<label>` مرتبط (`htmlFor`/`id`):
    - `{{IDENTIFIER_LABEL}}` → `type="text"`، `inputMode="numeric"`،
      `autoComplete="username"`، `autoFocus`.
    - «رمز عبور» → `type` بین `password` و `text` سوییچ می‌شود،
      `autoComplete="current-password"`.
  - **پوسته‌ی فیلد** (`.login-field`): `position: relative; display: flex; align-items: center`.
    - آیکون در لبه‌ی شروع: `position: absolute; inset-inline-start: 13px`، رنگِ متنِ کم‌رنگ،
      و **حتماً** `pointer-events: none` تا کلیک به خودِ فیلد برسد.
    - ورودی: `padding-inline-start: 40px`؛ برای فیلد رمز `padding-inline-end: 42px`.
    - دکمه‌ی نمایش/پنهان رمز در لبه‌ی پایان: دایره‌ی `32×32`، بدون حاشیه، پس‌زمینه‌ی شفاف،
      در هاور پس‌زمینه‌ی سطحِ دوم. `type="button"` (نه submit)،
      `aria-pressed={isVisible}` و `aria-label` که با حالت عوض می‌شود.
  - ورودی‌های این صفحه یک پله بلندتر و گردتر از فرم‌های داخل برنامه‌اند:
    `padding-block: 12px; font-size: 15px; border-radius: 8px`.
  - **دکمه‌ی ورود**:
    ```css
    .login-submit {
      width: 100%;
      padding: 12px;
      font-size: 15px;
      color: #ffffff;
      border: none;
      border-radius: 8px;
      background: linear-gradient(120deg, #0b6f8f, #0a7a4a);
      box-shadow: 0 8px 20px rgb(6 80 80 / 0.28);
      transition: filter 0.15s ease, transform 0.1s ease;
    }
    .login-submit:hover:not(:disabled) { filter: brightness(1.08); }
    .login-submit:active:not(:disabled) { transform: translateY(1px); }
    ```
    هر دو سرِ گرادیانت عمداً یک پله تیره‌تر از رنگ خامِ برندند تا متن سفید رویشان کنتراست
    AA بگیرد. در حال ارسال: آیکون چرخانِ SVG + متن «در حال ورود...» و `disabled`.

- **امضا** (`.login-panel__credit`): `margin: auto 0 0` تا به کفِ ستون بچسبد و ارتفاع دو
  ستون هم‌تراز بماند. دو سطر: `{{CREDIT}}` و «نسخه {{VERSION}}»، `11px`، `line-height: 1.7`.

# ستون برند

```css
.login-hero {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 22px;
  padding: 40px 28px;
  color: #ffffff;                       /* در هر دو تم ثابت، چون زمینه‌اش ثابت است */
  background: linear-gradient(160deg, #008cbf 0%, #068a92 52%, #009654 100%);
  overflow: hidden;
  isolation: isolate;
}
```

- **بافتِ تکرارشونده** روی `::before` (`z-index: -1`): ماسکِ الفای `{{BRAND_LOGO_ICON}}` با
  `background-color: #ffffff` و `opacity: 0.035`، `mask-size: 96px 178px`، `mask-repeat: repeat`.
  داخل `@supports (mask-image: url(...)) or (-webkit-mask-image: url(...))` بگذار تا در
  مرورگرِ بدون پشتیبانی، مستطیلِ سفیدِ توپُر ظاهر نشود.
- **هاله‌ی نور** روی `::after` (`z-index: -1`):
  `radial-gradient(48% 36% at 50% 31%, rgb(255 255 255 / 0.2), transparent 72%)` —
  کارش این است که لوگو روی ستون «بنشیند» و در گرادیانت شناور نماند.
- **لوگو** (`.login-hero__logo`): یک `<div>` با `role="img"` و `aria-label`، ابعاد
  `118×200`، که نسخه‌ی تک‌رنگِ سفیدِ لوگو را از ماسکِ همان فایلِ رنگی می‌سازد:
  `background-color: #ffffff` + `mask: url({{BRAND_LOGO_FULL}}) no-repeat center / contain`
  (باز هم داخل `@supports`).
- **متن**: `h2` با `17px/700` و `text-wrap: balance` = `{{BRAND_TITLE}}`؛ `p` با `13px` و
  رنگ `rgb(255 255 255 / 0.8)` = `{{BRAND_ORG}}`.
- **سه قابلیت**: `ul` بدون بولت، ستونی، `gap: 12px`، `max-width: 250px`. هر `li` افقی با
  `gap: 10px`، متن `13px` به رنگ `rgb(255 255 255 / 0.92)`، و یک آیکونِ ۱۶ پیکسلی داخل
  مربعِ نیمه‌شفافِ سفید (`border-radius` گرد، پس‌زمینه‌ی `rgb(255 255 255 / 0.14)`).
  آیکون‌ها: لایه‌ها، ذره‌بین، کاربران. متن‌ها: `{{FEATURE_1}}` تا `{{FEATURE_3}}`.

# واکنش‌گرایی

یک نقطه‌ی شکست، در `860px`:

```css
@media (max-width: 860px) {
  .login-shell { width: min(440px, 100%); grid-template-columns: 1fr; }
  /* ستون برند به نوارِ کوتاهِ بالای فرم تبدیل می‌شود */
  .login-hero { order: -1; flex-direction: row; justify-content: flex-start; gap: 14px; padding: 16px 20px; }
  .login-hero__logo { width: 40px; height: 68px; }
  .login-hero__text { text-align: start; }
  .login-hero__text h2 { font-size: 15px; }
  /* فهرست قابلیت‌ها و نشانِ داخل فرم حذف می‌شوند تا صفحه شلوغ نشود */
  .login-hero__features, .login-panel__mark { display: none; }
  .login-panel { padding: 26px 22px 18px; }
}
```

# رفتار

1. اگر کاربر از قبل احراز هویت شده، بدون رندرِ فرم به `{{AFTER_LOGIN_PATH}}` هدایت شود
   (`replace: true`).
2. پس از ورود موفق **همیشه** به `{{AFTER_LOGIN_PATH}}` برو، نه به «صفحه‌ی قبلی». دلیل:
   وقتی کاربر عمداً خروج می‌زند، مسیرِ همان صفحه در state ذخیره می‌ماند و ورودِ بعدی او را
   به جای صفحه‌ی اصلی به همان‌جا برمی‌گرداند.
3. ارسال فرم:
   - اگر درخواستی در جریان است، ارسالِ دوباره نادیده گرفته شود (محافظت از دابل‌کلیک و
     دابل‌اینتر).
   - شناسه با تابعِ تبدیلِ رقم به لاتین تبدیل و `trim` شود.
   - اگر شناسه یا رمز خالی است: «لطفاً {{IDENTIFIER_LABEL}} و رمز عبور را وارد کنید.»
   - خطای سرور: پیامِ خودِ خطا اگر از جنسِ خطای API بود، وگرنه «امکان ورود وجود ندارد.
     لطفاً دوباره تلاش کنید.»
   - در `finally` وضعیتِ ارسال آزاد شود.
4. دکمه‌ی چشم فقط `type` فیلد را عوض می‌کند، نه مقدارش را.
5. **اختیاری** — بخشِ «ورود توسعه‌دهنده»: یک آکاردئون که فقط در بیلد توسعه رندر می‌شود
   (`import.meta.env.DEV` یا معادلش)، شاملِ انتخابگرِ نقش و دکمه‌ی ورود مستقیم، با دو
   جمله‌ی هشدار که این حساب‌ها ساختگی‌اند. اگر پروژه‌ی مقصد چنین چیزی ندارد، حذفش کن.

# دسترس‌پذیری

- هر ورودی `<label>` مرتبط دارد (`htmlFor`/`id`)؛ placeholder جای label را نمی‌گیرد.
- آیکون‌های تزئینی `aria-hidden="true"` و `focusable="false"` دارند.
- لایه‌ی شفق `aria-hidden="true"` و `pointer-events: none` است.
- جعبه‌ی خطا `role="alert"` دارد تا صفحه‌خوان بلافاصله بخواند.
- دکمه‌ی نمایش رمز `aria-pressed` و `aria-label`ِ متغیر دارد.
- حلقه‌ی فوکوسِ دیده‌شدنی روی همه‌ی عناصرِ تعاملی.
- `prefers-reduced-motion` برای انیمیشنِ هاله‌ها رعایت شده.

# چک‌لیست پذیرش

- [ ] در `1400×900` کارت وسط صفحه و دوستونی است؛ ستون فرم سمت راست.
- [ ] سه هاله‌ی رنگی در پس‌زمینه دیده می‌شوند ولی هیچ لبه‌ی تیزی ندارند.
- [ ] در `390×844` ستون برند به نوارِ افقیِ بالای فرم تبدیل شده و فهرست قابلیت‌ها حذف شده.
- [ ] در تم تیره همه‌ی متن‌ها خوانا هستند و ستون برند همان گرادیانت را دارد.
- [ ] کلیک روی آیکونِ داخل فیلد، فوکوس را به خودِ فیلد می‌برد.
- [ ] با `prefers-reduced-motion: reduce` هاله‌ها ثابت‌اند ولی حذف نشده‌اند.
- [ ] با صفحه‌کلید می‌شود کل فرم را پیمود و حلقه‌ی فوکوس همه‌جا دیده می‌شود.
- [ ] بدنه‌ی صفحه اسکرولِ افقی ندارد.

# خروجی

فایل‌های ساخته/تغییریافته را در پایان فهرست کن و بگو در هرکدام دقیقاً چه چیزی اضافه شد.
