import { Link } from 'react-router-dom'

import { TeamCrest } from '@/components/ui/TeamCrest'
import type { LeagueStatistics, PlayerStatRow } from '@/types'

/** Top scorers and assist leaders — filtered to the current league only. */
export function LeadersCard({
  statistics,
  seasonId,
  leagueTeamIds,
  crestFor,
}: {
  statistics: LeagueStatistics | undefined
  seasonId: string
  /** Team IDs from the standings table — only players on these teams are shown. */
  leagueTeamIds?: Set<string>
  /** Stat rows carry no crest url, so it is looked up by team id. */
  crestFor?: (teamId: string) => string | null
}) {
  // Filter helper: keep only players whose team is in the current league.
  const inLeague = (rows: PlayerStatRow[]) =>
    leagueTeamIds ? rows.filter((row) => leagueTeamIds.has(row.teamId)) : rows

  return (
    <section className="glass-card">
      <header className="mb-3 flex items-baseline justify-between gap-2">
        <h3 className="text-sm font-semibold">Bombardirlər və Assistlər</h3>
        <Link
          to={`/seasons/${seasonId}/statistics`}
          className="shrink-0 text-xs text-accent hover:underline"
        >
          Hamısı →
        </Link>
      </header>

      {!statistics || statistics.matchesPlayed === 0 ? (
        <p className="py-6 text-center text-xs text-muted">
          Matçlar oynanandan sonra burada statistika görünəcək.
        </p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2">
          <Column
            title="Qol"
            rows={inLeague(statistics.topScorers).slice(0, 5)}
            render={(row) => row.goals}
            crestFor={crestFor}
          />
          <Column
            title="Assist"
            rows={inLeague(statistics.topAssists).slice(0, 5)}
            render={(row) => row.assists}
            crestFor={crestFor}
          />
        </div>
      )}
    </section>
  )
}

function Column({
  title,
  rows,
  render,
  tone,
  crestFor,
}: {
  title: string
  rows: PlayerStatRow[]
  render: (row: PlayerStatRow) => number
  tone?: string
  crestFor?: (teamId: string) => string | null
}) {
  return (
    <div>
      <h4 className="mb-1.5 text-[0.65rem] tracking-wide text-muted uppercase">{title}</h4>

      {rows.length === 0 ? (
        <p className="py-2 text-xs text-muted">Məlumat yoxdur.</p>
      ) : (
        <ul className="space-y-1">
          {rows.map((row) => (
            <li key={row.playerId} className="flex items-center gap-1.5">
              <TeamCrest name={row.teamName} logoUrl={crestFor?.(row.teamId)} size="xs" />
              <span className="flex-1 truncate text-[0.7rem]">{row.playerName}</span>
              <span className={`shrink-0 text-xs font-bold ${tone ?? ''}`}>{render(row)}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

