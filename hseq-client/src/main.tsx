import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// فونت خودمیزبان: فایل‌های woff2 داخل خروجی build کپی می‌شوند و از همان مبدأ سرو
// می‌شوند. سرور عملیاتی به اینترنت راه ندارد، پس هر ارجاعی به fonts.googleapis.com
// فقط تایم‌اوت می‌شود. نسخه‌ی variable یک فایل برای کل بازه‌ی وزن ۴۰۰ تا ۷۰۰ دارد.
// پیش از index.css می‌آید تا @font-face زودتر از قواعدی که فونت را به کار می‌برند
// تعریف شده باشد.
import '@fontsource-variable/vazirmatn'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
