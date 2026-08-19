// Single centralized place that knows how to talk to HSEQ.API: base URL,
// auth header attachment, and HTTP status handling (401/403/other errors).
// Nothing outside this file should call fetch() directly against the API.

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL

export class ApiError extends Error {
  status: number
  code?: number

  constructor(status: number, message: string, code?: number) {
    super(message)
    this.status = status
    this.code = code
  }
}

type TokenGetter = () => string | null
let getToken: TokenGetter = () => null
let onUnauthorized: () => void = () => {}

// Wired up once by AuthProvider so the http layer can read the current token
// and react to a 401 (session invalid/expired) without importing React state
// directly into this module.
export function configureHttpClient(tokenGetter: TokenGetter, unauthorizedHandler: () => void) {
  getToken = tokenGetter
  onUnauthorized = unauthorizedHandler
}

async function parseErrorBody(response: Response): Promise<{ message: string; code?: number }> {
  try {
    const body = await response.json()
    if (body && typeof body.message === 'string') {
      return { message: body.message, code: body.code }
    }
  } catch {
    // Non-JSON or empty body - fall through to the generic message below.
  }
  return { message: `Request failed with status ${response.status}` }
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (response.status === 401) {
    onUnauthorized()
    const { message, code } = await parseErrorBody(response)
    throw new ApiError(401, message || 'Your session has expired. Please log in again.', code)
  }

  if (response.status === 403) {
    const { message, code } = await parseErrorBody(response)
    throw new ApiError(403, message || 'You do not have permission to perform this action.', code)
  }

  if (!response.ok) {
    const { message, code } = await parseErrorBody(response)
    throw new ApiError(response.status, message, code)
  }

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  if (!text) return undefined as T
  return JSON.parse(text) as T
}

function authHeaders(): HeadersInit {
  const token = getToken()
  return token ? { Authorization: `Bearer ${token}` } : {}
}

export async function apiGet<T>(path: string, params?: Record<string, string | number | boolean | undefined>): Promise<T> {
  const url = new URL(API_BASE_URL + path)
  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined) url.searchParams.set(key, String(value))
    }
  }
  const response = await fetch(url, { headers: { ...authHeaders() } })
  return handleResponse<T>(response)
}

// Same auth/error handling as apiGet, but keeps the response as binary. Needed
// because the file routes are Bearer-protected: a plain <a href> or window.open
// would not carry the Authorization header, so files have to be fetched here and
// handed to the browser as a blob URL.
export async function apiGetBlob(
  path: string,
  params?: Record<string, string | number | boolean | undefined>,
): Promise<Blob> {
  const url = new URL(API_BASE_URL + path)
  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined) url.searchParams.set(key, String(value))
    }
  }

  const response = await fetch(url, { headers: { ...authHeaders() } })

  if (!response.ok) {
    // Errors still come back as the API's usual JSON envelope, so reuse the shared
    // status handling instead of surfacing a broken blob.
    await handleResponse<unknown>(response)
  }

  return response.blob()
}

export async function apiPostForm<T>(path: string, body: FormData | URLSearchParams): Promise<T> {
  const response = await fetch(API_BASE_URL + path, {
    method: 'POST',
    headers: { ...authHeaders() },
    body,
  })
  return handleResponse<T>(response)
}
