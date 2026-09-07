// اسکرین‌شات از صفحه‌های برنامه، از جمله صفحه‌هایی که پشتِ ورود هستند.
//
// چرا لازم شد: از رابط برنامه نمی‌شود بدون ورود عکس گرفت و از طرفی
// «‎chrome --screenshot‎» هیچ راهی برای اجرای اسکریپت پیش از عکس‌گرفتن نمی‌دهد. این
// اسکریپت کروم را با پروتکل دیباگ (CDP) بالا می‌آورد، خودش فرمِ «ورود توسعه‌دهنده»
// را پر می‌کند، به مسیر خواسته‌شده می‌رود و بعد عکس می‌گیرد.
//
// هیچ پکیجی لازم ندارد: WebSocket از Node 22 به بعد سراسری است و کروم هم همان
// مرورگرِ نصب‌شده‌ی سیستم است.
//
//   node scripts/screenshot.mjs --path=/documents --out=shots/documents.png
//   node scripts/screenshot.mjs --path=/login --width=390 --height=844 --anonymous
//   node scripts/screenshot.mjs --path=/dashboard --theme=dark --role=ReadOnly
//
// یا از ریشه‌ی hseq-client:  npm run screenshot -- --path=/documents
//
// پیش‌نیاز: سرور توسعه باید بالا باشد (npm run dev). «ورود توسعه‌دهنده» فقط در
// بیلد توسعه رندر می‌شود، پس این اسکریپت روی نسخه‌ی عملیاتی کار نمی‌کند - که خودش
// درست است، چون آنجا حساب آزمایشی وجود ندارد.

import { spawn } from 'node:child_process'
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, resolve } from 'node:path'

// ---------------------------------------------------------------------------
// آرگومان‌ها
// ---------------------------------------------------------------------------
const args = Object.fromEntries(
  process.argv.slice(2).map((arg) => {
    const [key, value] = arg.replace(/^--/, '').split('=')
    return [key, value ?? true]
  }),
)

if (args.help) {
  console.log(`
اسکرین‌شات از رابط کاربری

  --path=/documents      مسیری که عکسش گرفته می‌شود (پیش‌فرض: /documents)
  --out=shots/x.png      فایل خروجی (پیش‌فرض: shots/<نام مسیر>.png)
  --width=1400           عرض دیدگاه (پیش‌فرض: 1400)
  --height=900           ارتفاع دیدگاه (پیش‌فرض: 900)
  --theme=light|dark     تم؛ بدون این گزینه از ترجیح سیستم پیروی می‌کند
  --role=Admin           نقش آزمایشی: Admin | DocumentManager | ReadOnly
  --anonymous            بدون ورود (برای خودِ صفحه‌ی ورود)
  --full                 عکس از کلِ صفحه، نه فقط بخشِ دیده‌شده
  --url=http://...:5173  ریشه‌ی سرور توسعه (پیش‌فرض: http://localhost:5173)

  متغیر محیطی CHROME_PATH مسیر مرورگر را دستی تعیین می‌کند.
`)
  process.exit(0)
}

// Git Bash هر آرگومانی را که با ‎/‎ شروع شود به مسیر ویندوزی ترجمه می‌کند، یعنی
// ‎--path=/documents‎ به ‎C:/Program Files/Git/documents‎ تبدیل می‌شود. برای برگرداندنش
// طولانی‌ترین پیشوندی را که واقعاً روی دیسک وجود دارد جدا می‌کنیم؛ باقی‌مانده همان
// چیزی است که کاربر تایپ کرده. در PowerShell این اتفاق نمی‌افتد و تابع بی‌اثر است.
function unmanglePath(value) {
  if (!/^[A-Za-z]:[\\/]/.test(value)) return value
  const parts = value.replace(/\\/g, '/').split('/')
  for (let i = parts.length - 1; i > 0; i--) {
    if (existsSync(parts.slice(0, i).join('/'))) return `/${parts.slice(i).join('/')}`
  }
  return value
}

const path = typeof args.path === 'string' ? unmanglePath(args.path) : '/documents'
const width = Number(args.width ?? 1400)
const height = Number(args.height ?? 900)
const role = typeof args.role === 'string' ? args.role : 'Admin'
const base = (typeof args.url === 'string' ? args.url : 'http://localhost:5173').replace(/\/$/, '')
const anonymous = Boolean(args.anonymous)
const fullPage = Boolean(args.full)
const theme = args.theme === 'dark' || args.theme === 'light' ? args.theme : null

const out = resolve(
  typeof args.out === 'string' ? args.out : `shots/${path.replace(/^\//, '').replace(/\//g, '-') || 'home'}.png`,
)

// ---------------------------------------------------------------------------
// پیدا کردن مرورگر. هر کرومیومی که پروتکل دیباگ داشته باشد کار می‌کند، پس اِج هم
// به‌عنوان جایگزین قبول است - روی ویندوز همیشه نصب است.
// ---------------------------------------------------------------------------
const CANDIDATES = [
  process.env.CHROME_PATH,
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
  'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
].filter(Boolean)

const chromePath = CANDIDATES.find((candidate) => existsSync(candidate))
if (!chromePath) {
  console.error('مرورگر کرومیوم پیدا نشد. مسیرش را در متغیر محیطی CHROME_PATH بگذارید.')
  process.exit(1)
}

// ---------------------------------------------------------------------------
// راه‌اندازی مرورگر
// ---------------------------------------------------------------------------
const PORT = Number(process.env.CDP_PORT ?? 9333)
const profile = mkdtempSync(resolve(tmpdir(), 'hseq-shot-'))
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

const chrome = spawn(
  chromePath,
  [
    '--headless=new',
    '--disable-gpu',
    '--hide-scrollbars',
    '--no-first-run',
    '--no-default-browser-check',
    `--remote-debugging-port=${PORT}`,
    `--user-data-dir=${profile}`,
    'about:blank',
  ],
  { stdio: 'ignore' },
)

// پروسه‌ی مرورگر به والد وابسته نباشد؛ وگرنه Node تا زنده‌بودنِ آن بیرون نمی‌آید.
chrome.unref()

let ws = null
function cleanup() {
  try { ws?.close() } catch {}
  try { chrome.kill() } catch {}
  try { rmSync(profile, { recursive: true, force: true }) } catch {}
}
process.on('SIGINT', () => { cleanup(); process.exit(130) })

try {
  // صبر تا بالا آمدنِ اندپوینت دیباگ - کروم چند صد میلی‌ثانیه طول می‌کشد.
  let target = null
  for (let i = 0; i < 60 && !target; i++) {
    try {
      const list = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json()
      target = list.find((t) => t.type === 'page')
    } catch {}
    if (!target) await sleep(250)
  }
  if (!target) throw new Error('مرورگر بالا نیامد')

  // --- لایه‌ی نازکِ CDP ---------------------------------------------------
  ws = new WebSocket(target.webSocketDebuggerUrl)
  await new Promise((res, rej) => {
    ws.addEventListener('open', res, { once: true })
    ws.addEventListener('error', () => rej(new Error('اتصال به مرورگر برقرار نشد')), { once: true })
  })

  let seq = 0
  const pending = new Map()
  ws.addEventListener('message', (event) => {
    const msg = JSON.parse(event.data)
    const entry = msg.id && pending.get(msg.id)
    if (!entry) return
    pending.delete(msg.id)
    if (msg.error) entry.reject(new Error(JSON.stringify(msg.error)))
    else entry.resolve(msg.result)
  })

  const send = (method, params = {}) =>
    new Promise((resolve_, reject) => {
      const id = ++seq
      pending.set(id, { resolve: resolve_, reject })
      ws.send(JSON.stringify({ id, method, params }))
    })

  const evaluate = async (expression) => {
    const r = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true })
    if (r.exceptionDetails) {
      // ‎text‎ به‌تنهایی معمولاً فقط «Uncaught» است؛ پیام واقعی در description است.
      const detail = r.exceptionDetails.exception?.description ?? r.exceptionDetails.exception?.value
      throw new Error(detail ?? r.exceptionDetails.text)
    }
    return r.result?.value
  }

  await send('Page.enable')
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false })

  // تم را با شبیه‌سازیِ ‎prefers-color-scheme‎ ست می‌کنیم، نه با فلگِ خط فرمان: مقدارِ
  // عددیِ آن فلگ مستند نیست و در عمل تمِ تیره را نمی‌داد. باید پیش از ناوبری اعمال
  // شود، چون اسکریپتِ داخل index.html تم را قبل از اولین رنگ‌آمیزی می‌خواند.
  if (theme) {
    await send('Emulation.setEmulatedMedia', {
      features: [{ name: 'prefers-color-scheme', value: theme }],
    })
  }

  // --- ورود --------------------------------------------------------------
  if (anonymous) {
    await send('Page.navigate', { url: `${base}${path}` })
    await sleep(2500)
  } else {
    await send('Page.navigate', { url: `${base}/login` })
    await sleep(2500)

    // فرم «ورود توسعه‌دهنده» را مثل یک کاربر پر می‌کنیم. مقدار select از راهِ
    // setterِ نیتیو گذاشته می‌شود، وگرنه React تغییر را نمی‌بیند.
    const landed = await evaluate(`
      (async () => {
        const wait = (ms) => new Promise((r) => setTimeout(r, ms))
        const byText = (text) => [...document.querySelectorAll('button')].find((b) => b.textContent.includes(text))

        const toggle = byText('ورود توسعه‌دهنده')
        if (!toggle) return 'no-dev-login'
        toggle.click()
        await wait(300)

        const select = document.getElementById('dev-role')
        if (!select) return 'no-role-select'
        Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(select, ${JSON.stringify(role)})
        select.dispatchEvent(new Event('change', { bubbles: true }))
        await wait(300)

        const submit = byText('ورود مستقیم')
        if (!submit) return 'no-submit'
        submit.click()
        await wait(2500)
        return location.pathname
      })()
    `)

    if (landed === 'no-dev-login') {
      throw new Error('دکمه‌ی «ورود توسعه‌دهنده» نبود - سرور توسعه بالاست؟ (npm run dev)')
    }
    if (typeof landed === 'string' && landed.startsWith('no-')) {
      throw new Error(`فرم ورود توسعه‌دهنده کامل نشد: ${landed}`)
    }

    // ناوبری سمت کلاینت، تا نشستِ تازه با ری‌لود از بین نرود.
    await evaluate(`
      (() => {
        history.pushState({}, '', ${JSON.stringify(path)})
        dispatchEvent(new PopStateEvent('popstate'))
        return 1
      })()
    `)
    await sleep(2500)
  }

  // --- عکس ---------------------------------------------------------------
  const options = { format: 'png' }
  if (fullPage) options.captureBeyondViewport = true

  const shot = await send('Page.captureScreenshot', options)
  mkdirSync(dirname(out), { recursive: true })
  writeFileSync(out, Buffer.from(shot.data, 'base64'))
  console.log(`نوشته شد: ${out}`)
} catch (error) {
  console.error(`خطا: ${error.message}`)
  process.exitCode = 1
} finally {
  // سوکتِ باز و پروسه‌ی مرورگر حلقه‌ی رویداد را زنده نگه می‌دارند، پس بستنشان باید
  // صریح باشد؛ ‎process.on('exit')‎ در این حالت هرگز اجرا نمی‌شد.
  cleanup()
  process.exit(process.exitCode ?? 0)
}
