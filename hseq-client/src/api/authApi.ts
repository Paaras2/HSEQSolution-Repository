import { apiPostForm } from '../lib/httpClient'
import type { LoginResult } from '../types/api'

export const authApi = {
  async login(pcode: string, password: string): Promise<LoginResult> {
    const body = new URLSearchParams()
    body.set('Username', pcode)
    body.set('Password', password)
    return apiPostForm<LoginResult>('/Auth/login', body)
  },

  // Development-only shortcut - see AuthController.DevLogin. Mints a token for a
  // made-up test identity by role, bypassing the UM service entirely.
  async devLogin(role: string): Promise<LoginResult> {
    const body = new URLSearchParams()
    body.set('Role', role)
    return apiPostForm<LoginResult>('/Auth/dev-login', body)
  },
}