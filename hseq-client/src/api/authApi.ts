import { apiPostForm, apiPostJson } from '../lib/httpClient'
import type { LoginResult } from '../types/api'

export const authApi = {
  // بدنه همان قراردادِ checkCredential سامانه‌ی مدیریت کاربران است: { username, password }.
  //
  // مرورگر آن سرویس را مستقیم صدا نمی‌زند. HSEQ.API اعتبار را از آن می‌پرسد و خودش
  // توکن امضا می‌کند؛ اگر پاسخِ «موفق» از مرورگر به ما می‌رسید، هر کسی می‌توانست یک
  // کد پرسنلیِ دلخواه جعل کند.
  async login(username: string, password: string): Promise<LoginResult> {
    return apiPostJson<LoginResult>('/Auth/login', { username, password })
  },

  // Development-only shortcut - see AuthController.DevLogin. Mints a token for a
  // made-up test identity by role, bypassing the UM service entirely.
  async devLogin(role: string): Promise<LoginResult> {
    const body = new URLSearchParams()
    body.set('Role', role)
    return apiPostForm<LoginResult>('/Auth/dev-login', body)
  },
}
