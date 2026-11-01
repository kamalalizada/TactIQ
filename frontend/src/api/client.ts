import { auth } from '@/lib/auth'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')

  const token = auth.token
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const response = await fetch(`/api${path}`, { ...init, headers })

  if (response.status === 401) {
    // The token expired; drop it and reload so the app signs itself back in.
    auth.clear()
    window.location.reload()
    throw new ApiError(401, 'Session expired.')
  }

  if (!response.ok) {
    throw new ApiError(response.status, await errorMessage(response))
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

/** Readable stand-ins for the bare statuses the API returns without detail. */
const STATUS_MESSAGES: Record<number, string> = {
  400: 'Sorğu düzgün deyil.',
  403: 'Bu əməliyyata icazəniz yoxdur.',
  404: 'Axtardığınız məlumat tapılmadı.',
  409: 'Bu əməliyyat indi mümkün deyil.',
  500: 'Serverdə xəta baş verdi.',
}

async function errorMessage(response: Response): Promise<string> {
  try {
    const body = await response.json()
    // ProblemDetails from the API, or an Identity error list.
    if (typeof body?.detail === 'string') return body.detail
    if (Array.isArray(body?.errors)) return body.errors.join(' ')
  } catch {
    // Fall through to the status message.
  }

  // ProblemDetails titles like "Not Found" read as raw plumbing, so they are
  // replaced rather than shown to the manager.
  return STATUS_MESSAGES[response.status] ?? 'Xəta baş verdi.'
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
  patch: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PATCH', body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
