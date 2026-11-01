import { authApi } from '@/api/endpoints'

const TOKEN_KEY = 'tactiq.token'
const EMAIL_KEY = 'tactiq.email'

// Development convenience: the app signs itself in so there is no login wall.
// Swap this out for the real login screen before shipping.
const DEMO_EMAIL = 'demo@tactiq.local'
const DEMO_PASSWORD = 'demo1234'

export const auth = {
  get token() {
    return localStorage.getItem(TOKEN_KEY)
  },
  get email() {
    return localStorage.getItem(EMAIL_KEY)
  },
  save(token: string, email: string) {
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(EMAIL_KEY, email)
  },
  clear() {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(EMAIL_KEY)
  },
}

let pending: Promise<void> | null = null

/**
 * Makes sure a token exists, signing in as the demo user if it does not.
 * Concurrent callers share one request so the account is never created twice.
 */
export function ensureSignedIn(): Promise<void> {
  if (auth.token) return Promise.resolve()

  pending ??= signInAsDemo().finally(() => {
    pending = null
  })

  return pending
}

async function signInAsDemo(): Promise<void> {
  try {
    const result = await authApi.login(DEMO_EMAIL, DEMO_PASSWORD)
    auth.save(result.token, result.email)
    return
  } catch {
    // First run on a fresh database: the account does not exist yet.
  }

  const created = await authApi.register(DEMO_EMAIL, DEMO_PASSWORD)
  auth.save(created.token, created.email)
}
