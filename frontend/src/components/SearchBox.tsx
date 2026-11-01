import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'

import { seasonApi } from '@/api/endpoints'
import { PositionBadge } from '@/components/ui/Badge'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { useActiveSeason } from '@/lib/useActiveSeason'
import { ROLE_LABELS, type PlayerHit, type TeamHit } from '@/types'

/**
 * Finding a club or a player from anywhere in the app.
 *
 * A save holds fifty-eight squads and over a thousand players, and a manager
 * who remembers a name should not have to work out which league the player is
 * in to look him up. Results open under the box and go straight to the club:
 * a player is only ever interesting in the context of the side he plays for.
 */
export function SearchBox() {
  const seasonId = useActiveSeason()
  const navigate = useNavigate()

  const [term, setTerm] = useState('')
  const [open, setOpen] = useState(false)
  const box = useRef<HTMLDivElement>(null)

  // Typing a name sends a request per keystroke otherwise, and the answer for
  // "sal" is worthless once "salah" has been typed.
  const query = useDebounced(term, 250)

  const results = useQuery({
    queryKey: ['search', seasonId, query],
    queryFn: () => seasonApi.search(seasonId!, query),
    enabled: Boolean(seasonId) && query.trim().length >= 2,
  })

  // A click anywhere else closes the panel, as a dropdown should.
  useEffect(() => {
    if (!open) return

    const onClick = (event: MouseEvent) => {
      if (!box.current?.contains(event.target as Node)) setOpen(false)
    }

    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [open])

  const go = (to: string) => {
    setOpen(false)
    setTerm('')
    navigate(to)
  }

  const teams = results.data?.teams ?? []
  const players = results.data?.players ?? []
  const nothingFound =
    query.trim().length >= 2 && !results.isPending && teams.length === 0 && players.length === 0

  return (
    <div ref={box} className="relative min-w-0 flex-1 sm:max-w-sm">
      <label className="relative block">
        <span className="sr-only">Axtarış</span>
        <span
          aria-hidden
          className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-muted"
        >
          ⌕
        </span>
        <input
          type="search"
          value={term}
          disabled={!seasonId}
          onChange={(event) => {
            setTerm(event.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={(event) => event.key === 'Escape' && setOpen(false)}
          placeholder={seasonId ? 'Komanda və ya oyunçu axtar' : 'Əvvəlcə mövsüm seçin'}
          className="w-full rounded-lg border border-line bg-panel py-1.5 pr-3 pl-8 text-sm outline-none placeholder:text-muted/60 focus:border-violet disabled:cursor-not-allowed disabled:opacity-50"
        />
      </label>

      {open && query.trim().length >= 2 && (
        <div className="absolute top-full right-0 left-0 z-40 mt-1 max-h-96 overflow-y-auto rounded-lg border border-line bg-panel shadow-2xl">
          {results.isPending && <p className="px-3 py-3 text-sm text-muted">Axtarılır…</p>}

          {nothingFound && (
            <p className="px-3 py-3 text-sm text-muted">Heç nə tapılmadı.</p>
          )}

          {teams.length > 0 && (
            <Section title="Komandalar">
              {teams.map((team) => (
                <TeamRow
                  key={team.id}
                  team={team}
                  onPick={() => go(`/seasons/${seasonId}/teams/${team.id}`)}
                />
              ))}
            </Section>
          )}

          {players.length > 0 && (
            <Section title="Oyunçular">
              {players.map((player) => (
                <PlayerRow
                  key={player.id}
                  player={player}
                  onPick={() =>
                    go(
                      player.isMine
                        ? `/seasons/${seasonId}/squad`
                        : `/seasons/${seasonId}/teams/${player.teamId}`,
                    )
                  }
                />
              ))}
            </Section>
          )}
        </div>
      )}
    </div>
  )
}

/** Holds back a value until the typing stops. */
function useDebounced(value: string, delay: number) {
  const [settled, setSettled] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setSettled(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay])

  return settled
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="border-b border-line/60 last:border-b-0">
      <h3 className="px-3 pt-2 pb-1 text-[0.65rem] tracking-wide text-muted uppercase">
        {title}
      </h3>
      <ul className="pb-1">{children}</ul>
    </section>
  )
}

function TeamRow({ team, onPick }: { team: TeamHit; onPick: () => void }) {
  return (
    <li>
      <button
        type="button"
        onClick={onPick}
        className="flex w-full items-center gap-2.5 px-3 py-2 text-left text-sm transition-colors hover:bg-white/5"
      >
        <TeamCrest name={team.name} logoUrl={team.logoUrl} />
        <span className="min-w-0 flex-1 truncate">{team.name}</span>
        <span className="shrink-0 text-[0.7rem] text-muted">{team.country}</span>
        <span className="w-7 shrink-0 text-right font-bold">
          {Math.round(team.teamOverall)}
        </span>
      </button>
    </li>
  )
}

function PlayerRow({ player, onPick }: { player: PlayerHit; onPick: () => void }) {
  return (
    <li>
      <button
        type="button"
        onClick={onPick}
        className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-white/5"
      >
        <PositionBadge position={player.position} />
        <span className="min-w-0 flex-1 truncate">
          {player.name}
          {player.isMine && <span className="ml-1.5 text-[0.65rem] text-violet">sizin</span>}
        </span>
        <span className="shrink-0 rounded border border-line px-1 text-[0.6rem] tracking-wide text-muted">
          {ROLE_LABELS[player.role]}
        </span>
        <span className="hidden w-24 shrink-0 truncate text-right text-[0.7rem] text-muted sm:block">
          {player.teamName}
        </span>
        <span className="w-7 shrink-0 text-right font-bold">{player.overall}</span>
      </button>
    </li>
  )
}
