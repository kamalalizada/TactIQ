import { NavLink } from 'react-router-dom'

import { auth } from '@/lib/auth'
import { useActiveSeason } from '@/lib/useActiveSeason'

type Item = {
  label: string
  icon: string
  to: (seasonId?: string) => string
  needsSeason?: boolean
}

const ITEMS: Item[] = [
  { label: 'İdarə Paneli', icon: '▦', to: (id) => (id ? `/seasons/${id}` : '/'), needsSeason: true },
  { label: 'Liqa', icon: '▤', to: (id) => `/seasons/${id}/table`, needsSeason: true },
  { label: 'Mövsümlər', icon: '⌂', to: () => '/' },
  { label: 'Komandalar', icon: '⛉', to: (id) => `/seasons/${id}/teams`, needsSeason: true },
  { label: 'Heyət', icon: '☰', to: (id) => `/seasons/${id}/squad`, needsSeason: true },
  { label: 'Transfer', icon: '⇄', to: (id) => `/seasons/${id}/transfers`, needsSeason: true },
  { label: 'Statistika', icon: '◫', to: (id) => `/seasons/${id}/statistics`, needsSeason: true },
  { label: 'Kubok', icon: '♛', to: (id) => `/seasons/${id}/cup`, needsSeason: true },
  { label: 'Karyera', icon: '✦', to: (id) => `/seasons/${id}/career`, needsSeason: true },
]

export function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const activeSeasonId = useActiveSeason()

  return (
    <div className="flex h-full flex-col border-r border-line bg-panel">
      <div className="px-5 py-5 text-lg font-black tracking-tight">
        Tact<span className="text-violet">IQ</span>
      </div>

      <nav className="flex-1 space-y-1 px-3">
        {ITEMS.map((item) => {
          const disabled = item.needsSeason && !activeSeasonId

          if (disabled) {
            return (
              <span
                key={item.label}
                aria-disabled
                className="flex cursor-not-allowed items-center gap-3 rounded-lg px-3 py-2.5 text-sm text-muted/40"
              >
                <span aria-hidden className="w-4 text-center">
                  {item.icon}
                </span>
                {item.label}
              </span>
            )
          }

          return (
            <NavLink
              key={item.label}
              to={item.to(activeSeasonId)}
              end={item.label === 'İdarə Paneli' || item.label === 'Mövsümlər'}
              onClick={onNavigate}
              className={({ isActive }) =>
                `relative flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors ${
                  isActive
                    ? 'bg-violet/12 font-medium text-ink'
                    : 'text-muted hover:bg-white/5 hover:text-ink'
                }`
              }
            >
              {({ isActive }) => (
                <>
                  {/* The active page is marked by a violet bar, as in the design. */}
                  {isActive && (
                    <span
                      aria-hidden
                      className="absolute top-1.5 bottom-1.5 left-0 w-0.5 rounded-full bg-violet"
                    />
                  )}
                  <span aria-hidden className="w-4 text-center">
                    {item.icon}
                  </span>
                  {item.label}
                </>
              )}
            </NavLink>
          )
        })}
      </nav>

      <div className="border-t border-line px-3 py-4">
        <div className="flex items-center gap-3 px-2">
          <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-violet/20 text-xs font-bold text-violet">
            {(auth.email ?? 'Qonaq').charAt(0).toUpperCase()}
          </span>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-medium">Menecer</p>
            <p className="truncate text-[0.7rem] text-muted">{auth.email ?? 'Qonaq'}</p>
          </div>
          {/* A guest has no password, so signing out would lose the save for good. */}
          {auth.email && (
            <button
              type="button"
              onClick={() => {
                auth.clear()
                window.location.assign('/login')
              }}
              className="shrink-0 text-[0.7rem] text-muted underline hover:text-ink"
            >
              Çıxış
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
