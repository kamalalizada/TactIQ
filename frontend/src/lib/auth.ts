import { authApi } from '@/api/endpoints'

const TOKEN_KEY = 'tactiq.token'
const EMAIL_KEY = 'tactiq.email'

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
  /** A guest's address is a generated one, so nothing is kept to show the manager. */
  saveGuest(token: string) {
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.removeItem(EMAIL_KEY)
  },
  clear() {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(EMAIL_KEY)
  },
}

let pending: Promise<void> | null = null

/**
 * Development convenience: signs in as a demo user so there is no login wall.
 * A production build has none of this, its password included; a visitor signs
 * in or plays as a guest. Concurrent callers share one request so the account
 * is never created twice.
 */
export function ensureSignedIn(): Promise<void> {
  if (!import.meta.env.DEV || auth.token) return Promise.resolve()

  pending ??= signInAsDemo().finally(() => {
    pending = null
  })

  return pending
}

async function signInAsDemo(): Promise<void> {
  const email = 'demo@tactiq.local'
  const password = 'demo1234'

  try {
    const result = await authApi.login(email, password)
    auth.save(result.token, result.email)
    return
  } catch {
    // First run on a fresh database: the account does not exist yet.
  }

  const created = await authApi.register(email, password)
  auth.save(created.token, created.email)
}
