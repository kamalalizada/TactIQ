import { useState, type ReactNode } from 'react'

import { NotificationBell } from '@/components/NotificationBell'
import { SearchBox } from '@/components/SearchBox'
import { Sidebar } from '@/components/Sidebar'

export function Shell({
  children,
  /** The dashboard runs edge to edge; ordinary pages stay narrower. */
  wide,
}: {
  children: ReactNode
  wide?: boolean
}) {
  const [menuOpen, setMenuOpen] = useState(false)

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[13rem_1fr]">
      <aside className="sticky top-0 hidden h-screen lg:block">
        <Sidebar />
      </aside>

      {/* Below desktop width the rail slides in over the content. */}
      {menuOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/60 lg:hidden"
          onClick={() => setMenuOpen(false)}
        >
          <aside
            className="h-full w-56 animate-[fade-in_0.15s_ease-out]"
            onClick={(event) => event.stopPropagation()}
          >
            <Sidebar onNavigate={() => setMenuOpen(false)} />
          </aside>
        </div>
      )}

      <div className="min-w-0">
        <header className="sticky top-0 z-30 flex items-center gap-3 border-b border-line bg-bg/90 px-4 py-2.5 backdrop-blur">
          <button
            type="button"
            aria-label="Menyu"
            className="rounded-md border border-line px-2 py-1 text-sm lg:hidden"
            onClick={() => setMenuOpen(true)}
          >
            ☰
          </button>

          <SearchBox />

          <div className="ml-auto flex items-center gap-2">
            <NotificationBell />
          </div>
        </header>

        <main className={`mx-auto px-4 py-5 ${wide ? 'max-w-[110rem]' : 'max-w-6xl'}`}>
          {children}
        </main>
      </div>
    </div>
  )
}
