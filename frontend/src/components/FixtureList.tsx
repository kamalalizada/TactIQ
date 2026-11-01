import { Link } from 'react-router-dom'

import { TeamCrest } from '@/components/ui/TeamCrest'
import type { Match } from '@/types'

export function FixtureList({
  matches,
  week,
  totalWeeks,
  onWeekChange,
  onInspect,
}: {
  matches: Match[]
  week: number
  totalWeeks: number
  onWeekChange: (week: number) => void
  onInspect: (match: Match) => void
}) {
  return (
    <div>
      <div className="mb-3 flex items-center justify-between gap-2 rounded-md border border-line bg-panel px-2 py-1.5">
        <button
          type="button"
          aria-label="Əvvəlki həftə"
          className="rounded px-2 py-1 text-muted transition-colors hover:bg-white/5 hover:text-ink disabled:opacity-30 disabled:hover:bg-transparent"
          disabled={week <= 1}
          onClick={() => onWeekChange(week - 1)}
        >
          ‹
        </button>

        <span className="text-sm font-semibold">Həftə {week}</span>

        <button
          type="button"
          aria-label="Sonrakı həftə"
          className="rounded px-2 py-1 text-muted transition-colors hover:bg-white/5 hover:text-ink disabled:opacity-30 disabled:hover:bg-transparent"
          disabled={week >= totalWeeks}
          onClick={() => onWeekChange(week + 1)}
        >
          ›
        </button>
      </div>

      <div className="scroll-list max-h-[26rem] space-y-2 pr-1">
        {matches.map((match) => (
          <FixtureRow key={match.id} match={match} onInspect={onInspect} />
        ))}
      </div>
    </div>
  )
}

function FixtureRow({ match, onInspect }: { match: Match; onInspect: (match: Match) => void }) {
  // The manager's own unplayed match is the one that needs kicking off.
  if (match.isUserMatch && !match.isPlayed) {
    return (
      <div className="rounded-md border border-win/50 bg-win/5 p-2.5">
        <p className="mb-1.5 text-[0.65rem] font-bold tracking-wide text-win uppercase">
          Your match
        </p>
        <div className="flex items-center gap-2">
          <TeamCrest name={match.homeTeamName} logoUrl={match.homeTeamLogoUrl} size="xs" />
          <span className="flex-1 truncate text-sm">{match.homeTeamName}</span>
          <span className="text-xs text-muted">vs</span>
          <span className="flex-1 truncate text-right text-sm">{match.awayTeamName}</span>
          <TeamCrest name={match.awayTeamName} logoUrl={match.awayTeamLogoUrl} size="xs" />
          <Link
            to={`/matches/${match.id}/live`}
            className="shrink-0 rounded bg-win px-3 py-1 text-xs font-bold text-bg transition-colors hover:bg-win/85"
          >
            PLAY
          </Link>
        </div>
        <p className="mt-1 text-[0.65rem] text-muted">Başlamayıb</p>
      </div>
    )
  }

  const scoreline = (
    <div className="flex items-center gap-2">
      <TeamCrest name={match.homeTeamName} logoUrl={match.homeTeamLogoUrl} size="xs" />
      <span className="flex-1 truncate text-sm">{match.homeTeamName}</span>
      <span className="shrink-0 rounded bg-bg px-2 py-0.5 text-sm font-bold">
        {match.isPlayed ? `${match.homeGoals} - ${match.awayGoals}` : 'vs'}
      </span>
      <span className="flex-1 truncate text-right text-sm">{match.awayTeamName}</span>
      <TeamCrest name={match.awayTeamName} logoUrl={match.awayTeamLogoUrl} size="xs" />
    </div>
  )

  if (match.isPlayed) {
    return (
      <button
        type="button"
        onClick={() => onInspect(match)}
        className={`w-full rounded-md border p-2.5 text-left transition-colors hover:bg-white/5 ${
          match.isUserMatch ? 'border-accent/40 bg-accent/5' : 'border-line bg-panel'
        }`}
      >
        {scoreline}
        <p className="mt-1 text-[0.65rem] text-muted">Bitdi</p>
      </button>
    )
  }

  return (
    <div className="rounded-md border border-line bg-panel p-2.5">
      {scoreline}
      <p className="mt-1 text-[0.65rem] text-muted">Başlamayıb</p>
    </div>
  )
}
