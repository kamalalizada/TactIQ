import { Link } from 'react-router-dom'

import { TeamCrest } from '@/components/ui/TeamCrest'
import { GameMode, type CompetitionSummary, type SeasonSummary, type Team } from '@/types'

interface DashboardHeaderProps {
  league: SeasonSummary
  /** The domestic league, which supplies the week counter. */
  domestic: CompetitionSummary | undefined
  userTeam?: Team
  cupRunning: boolean
  cupFinished: boolean
  cupWinnerName?: string | null
  seasonId: string
  /** Simulating the week is the page's primary action, so it lives up here. */
  /** False while browsing another league or once the season is over. */
  canSimulate: boolean
  /** True when the manager's own match is blocking the week. */
  hasPendingUserMatch: boolean
  /** The match to play, so the header can send the manager straight to it. */
  pendingMatchId?: string
  simulating: boolean
  onSimulate: () => void
  /** Plays every remaining week, stopping at the manager's own match. */
  onSimulateSeason: () => void
  simulatingSeason: boolean
  seasonWeeksPlayed: number
  /** Every domestic league in the season, so another can be viewed. */
  leagues: CompetitionSummary[]
  onPickLeague: (competitionId: string) => void
  viewingOwnLeague: boolean
}

export function DashboardHeader({
  league,
  domestic,
  userTeam,
  cupRunning,
  cupFinished,
  cupWinnerName,
  seasonId,
  canSimulate,
  hasPendingUserMatch,
  pendingMatchId,
  simulating,
  onSimulate,
  onSimulateSeason,
  simulatingSeason,
  seasonWeeksPlayed,
  leagues,
  onPickLeague,
  viewingOwnLeague,
}: DashboardHeaderProps) {
  const isManager = league.gameMode === GameMode.ManagerMode
  const greeting = userTeam ? `Xoş gəldin, ${userTeam.name}` : 'İdarə Paneli'

  return (
    <header className="mb-5 flex flex-wrap items-center justify-between gap-4">
      <div className="flex min-w-0 items-center gap-3">
        {userTeam && <TeamCrest name={userTeam.name} logoUrl={userTeam.logoUrl} size="lg" />}

        <div className="min-w-0">
          <h1 className="truncate text-2xl font-bold tracking-tight">{greeting}</h1>
          {leagues.length > 1 && domestic && (
            <div className="mt-1.5 flex items-center gap-2">
              <select
                aria-label="Liqa seç"
                value={domestic.id}
                onChange={(event) => onPickLeague(event.target.value)}
                className="rounded-md border border-line bg-panel px-2 py-1 text-xs text-ink outline-none focus:border-violet"
              >
                {leagues.map((competition) => (
                  <option key={competition.id} value={competition.id}>
                    {competition.name}
                  </option>
                ))}
              </select>

              {!viewingOwnLeague && (
                <span className="text-[0.7rem] text-muted">yalnız baxış</span>
              )}
            </div>
          )}

          <p className="mt-1 text-sm text-muted">
            {domestic ? (
              <>
                {domestic.name} · Həftə {Math.min(domestic.currentWeek, domestic.totalWeeks)} /{' '}
                {domestic.totalWeeks}
              </>
            ) : (
              league.name
            )}
            <span className="mx-1.5">·</span>
            <span className={isManager ? 'text-violet' : ''}>
              {isManager ? 'Menecer Rejimi' : 'Tanrı Rejimi'}
            </span>
          </p>
        </div>
      </div>

      <div className="flex shrink-0 items-center gap-2">
        {(cupRunning || cupFinished) && (
          <Link
            to={`/seasons/${seasonId}/cup`}
            title={cupFinished ? `Kubok qalibi: ${cupWinnerName}` : 'Kubok davam edir'}
            className="rounded-lg border border-draw/40 bg-draw/10 px-3 py-2 text-lg transition-colors hover:bg-draw/20"
          >
            🏆
          </Link>
        )}

        {hasPendingUserMatch && pendingMatchId ? (
          <Link
            to={`/matches/${pendingMatchId}/live`}
            className="rounded-lg bg-win px-5 py-2.5 text-sm font-bold text-bg transition-colors hover:bg-win/85"
          >
            Matçını Oyna
          </Link>
        ) : null}

        <button
          type="button"
          onClick={onSimulateSeason}
          disabled={!canSimulate || simulating || simulatingSeason}
          title={
            canSimulate
              ? 'Mövsümü sona qədər oynadır, öz matçınız gələndə dayanır'
              : 'Öz liqanıza qayıdın'
          }
          className="rounded-lg border border-line bg-panel px-4 py-2.5 text-sm font-semibold text-muted transition-colors hover:border-violet hover:text-ink disabled:cursor-not-allowed disabled:opacity-40"
        >
          {simulatingSeason
            ? `Mövsüm oynanılır… (${seasonWeeksPlayed} həftə)`
            : 'Mövsümü Simulyasiya Et'}
        </button>

        <button
          type="button"
          onClick={onSimulate}
          disabled={!canSimulate || simulating || simulatingSeason}
          title={
            !viewingOwnLeague
              ? 'Öz liqanıza qayıdın'
              : hasPendingUserMatch
                ? 'Öz matçınız gözləyir — həftə onsuz tamamlanmır'
                : undefined
          }
          className="rounded-lg bg-violet px-5 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-violet/85 disabled:cursor-not-allowed disabled:opacity-40"
        >
          {simulating ? 'Simulyasiya olunur…' : 'Həftəni Simulyasiya Et'}
        </button>
      </div>
    </header>
  )
}
