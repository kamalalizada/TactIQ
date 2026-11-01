import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'

import { seasonApi } from '@/api/endpoints'
import { EmptyNote } from '@/components/States'
import { TeamCrest } from '@/components/ui/TeamCrest'
import type { Match } from '@/types'

/**
 * The next match the manager has to play, with how the two sides have fared
 * against each other so far.
 */
export function MatchHubPreview({
  match,
  seasonId,
}: {
  match: Match | undefined
  seasonId: string
}) {
  const h2h = useQuery({
    queryKey: ['h2h', seasonId, match?.homeTeamId, match?.awayTeamId],
    queryFn: () => seasonApi.headToHead(seasonId, match!.homeTeamId, match!.awayTeamId),
    enabled: Boolean(match),
  })

  return (
    <section className="glass-card flex flex-1 flex-col">
      <h3 className="mb-3 text-sm font-semibold">Match Hub Preview</h3>

      {!match ? (
        <>
          <EmptyNote>Bu həftə sizin matçınız yoxdur.</EmptyNote>
        </>
      ) : (
        <>
          <p className="text-center text-base font-bold">
            {match.homeTeamName} <span className="text-muted">vs</span> {match.awayTeamName}
          </p>

          <div className="mt-3 flex items-center justify-center gap-6">
            <TeamCrest name={match.homeTeamName} logoUrl={match.homeTeamLogoUrl} size="lg" />
            <span className="text-xs text-muted">Həftə {match.weekNumber}</span>
            <TeamCrest name={match.awayTeamName} logoUrl={match.awayTeamLogoUrl} size="lg" />
          </div>

          <div className="mt-4">
            <h4 className="mb-2 text-xs tracking-wide text-muted uppercase">Qarşılaşma tarixi</h4>

            {h2h.isPending && <p className="text-xs text-muted">Yüklənir…</p>}

            {h2h.data && h2h.data.matches.length === 0 && (
              <p className="text-xs text-muted">Bu mövsüm ilk görüşdür.</p>
            )}

            {h2h.data && h2h.data.matches.length > 0 && (
              <>
                <div className="flex items-center gap-1 text-center text-xs">
                  <Tally label="Qalibiyyət" value={h2h.data.teamAWins} tone="text-win" />
                  <Tally label="Heç-heçə" value={h2h.data.draws} tone="text-draw" />
                  <Tally label="Məğlubiyyət" value={h2h.data.teamBWins} tone="text-loss" />
                </div>

                <ul className="mt-2 space-y-1">
                  {h2h.data.matches.slice(-3).map((past) => (
                    <li
                      key={past.matchId}
                      className="flex items-center gap-2 rounded border border-line/50 px-2 py-1 text-xs"
                    >
                      <span className="w-8 shrink-0 text-muted">H{past.weekNumber}</span>
                      <span className="flex-1 truncate text-right">{past.homeTeamName}</span>
                      <span className="shrink-0 font-bold">
                        {past.homeGoals}-{past.awayGoals}
                      </span>
                      <span className="flex-1 truncate">{past.awayTeamName}</span>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </div>

          <Link
            to={`/matches/${match.id}/live`}
            className="gradient-btn mt-4 w-full flex-col gap-0.5"
          >
            <span className="text-base">OYNA</span>
            <span className="text-[0.65rem] font-normal normal-case opacity-90">
              Həftəni simulyasiya etmək üçün əvvəlcə matçınızı oynayın
            </span>
          </Link>

        </>
      )}
    </section>
  )
}

function Tally({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <span className="flex-1 rounded border border-line/50 py-1.5">
      <span className={`block text-base font-bold ${tone}`}>{value}</span>
      <span className="block text-[0.6rem] text-muted">{label}</span>
    </span>
  )
}

