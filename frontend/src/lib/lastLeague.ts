// A new key, so ids remembered before the league→season migration are ignored
// rather than pointing the sidebar at a route that no longer resolves.
const KEY = 'tactiq.lastSeasonId'
const LEGACY_KEY = 'tactiq.lastLeagueId'

/**
 * Remembers which season the user was last looking at, so the sidebar can keep
 * Dashboard, Squad and the rest reachable from the season list as well.
 */
export const lastLeague = {
  get(): string | null {
    try {
      localStorage.removeItem(LEGACY_KEY)
      return localStorage.getItem(KEY)
    } catch {
      // Private mode or blocked storage; the sidebar simply stays disabled.
      return null
    }
  },
  set(seasonId: string) {
    try {
      localStorage.setItem(KEY, seasonId)
    } catch {
      // Nothing to do; remembering it is a convenience, not a requirement.
    }
  },
  clear(seasonId?: string) {
    try {
      // Only forget it when the season that was deleted is the one stored.
      if (!seasonId || localStorage.getItem(KEY) === seasonId) localStorage.removeItem(KEY)
    } catch {
      // Ignore.
    }
  },
}
