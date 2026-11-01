import { Link } from 'react-router-dom'

import {
  ClubHub,
  DashboardHeader,
  FixturesCard,
  KnockoutMini,
  LeadersCard,
  LeagueTableCard,
  MatchHubPreview,
  MatchInspectModal,
  QuickStatsRow,
  SeasonBanner,
  TransferCard,
  WeekProgressBar,
} from '@/components/dashboard'
import { RolloverModal } from '@/components/career/RolloverModal'
import { Shell } from '@/components/Shell'
import { ErrorNote, Spinner } from '@/components/States'
import { useDashboard } from '@/hooks/useDashboard'
import { useSimulateSeason } from '@/hooks/useSimulateSeason'
import { lastLeague } from '@/lib/lastLeague'

export function DashboardPage() {
  const {
    seasonId,
    season,
    standings,
    fixtures,
    cup,
    statistics,
    teams,
    transferWindow,
    transferOffers,
    career,
    nextSeason,
    rollover,
    setRollover,
    data,
    domestic,
    leagues,
    week,
    userTeam,
    pendingUserMatch,
    champion,
    cupRunning,
    userStandingsRow,
    totalTeams,
    viewingOwnLeague,
    simulate,
    setViewedWeek,
    setPickedLeagueId,
    inspected,
    setInspected,
  } = useDashboard()

  const seasonRun = useSimulateSeason(seasonId)


  // Statistics rows name a team but carry no crest, so it is resolved here.
  const crestFor = (teamId: string) =>
    teams.data?.find((team) => team.id === teamId)?.logoUrl ?? null

  if (season.isPending) {
    return (
      <Shell wide>
        <Spinner />
      </Shell>
    )
  }

  if (season.isError || !data) {
    // A remembered season can be deleted from elsewhere; offer a way back
    // rather than leaving the manager on a dead page.
    lastLeague.clear(seasonId)

    return (
      <Shell wide>
        <div className="mx-auto max-w-md pt-10 text-center">
          <ErrorNote error={season.error ?? new Error('Mövsüm tapılmadı.')} />
          <p className="mt-3 text-sm text-muted">
            Bu mövsüm silinmiş və ya mövcud deyil.
          </p>
          <Link to="/" className="btn-primary mt-5">
            Mövsümlərə qayıt
          </Link>
        </div>
      </Shell>
    )
  }

  return (
    <Shell wide>
      <DashboardHeader
        league={data}
        domestic={domestic}
        userTeam={userTeam}
        cupRunning={Boolean(cupRunning)}
        cupFinished={Boolean(cup.data?.finished)}
        cupWinnerName={cup.data?.winnerTeamName}
        seasonId={seasonId}
        canSimulate={!data.isFinished && viewingOwnLeague}
        hasPendingUserMatch={Boolean(pendingUserMatch)}
        pendingMatchId={pendingUserMatch?.id}
        simulating={simulate.isPending}
        onSimulate={() => simulate.mutate()}
        onSimulateSeason={() => seasonRun.run.mutate()}
        simulatingSeason={seasonRun.run.isPending}
        seasonWeeksPlayed={seasonRun.weeksPlayed}
        leagues={leagues ?? []}
        onPickLeague={setPickedLeagueId}
        viewingOwnLeague={viewingOwnLeague}
      />

      {domestic && (
        <WeekProgressBar currentWeek={domestic.currentWeek} totalWeeks={domestic.totalWeeks} />
      )}

      {data.userTeamId && (
        <QuickStatsRow
          userRank={userStandingsRow?.rank ?? null}
          userPoints={userStandingsRow?.points ?? null}
          userGoalsFor={userStandingsRow?.goalsFor ?? null}
          userWon={userStandingsRow?.won ?? null}
          userPlayed={userStandingsRow?.played ?? null}
          totalTeams={totalTeams}
        />
      )}

      {data.isFinished && (
        <SeasonBanner
          champion={champion}
          cupStarted={Boolean(cup.data?.started)}
          seasonId={seasonId}
          canAdvance={career.data?.canAdvance}
          onNextSeason={() => nextSeason.mutate()}
          advancing={nextSeason.isPending}
        />
      )}

      {simulate.isError && (
        <div className="mb-4">
          <ErrorNote error={simulate.error} />
        </div>
      )}

      {/* Three columns on wide screens, folding down to one on a phone. */}
      <div className="grid items-start gap-4 md:grid-cols-2 xl:grid-cols-3">
        <div className="space-y-4">
          <LeagueTableCard standings={standings} userTeamId={data.userTeamId} />
        </div>

        <div className="space-y-4">
          <FixturesCard
            fixtures={fixtures}
            week={week}
            totalWeeks={domestic?.totalWeeks ?? 0}
            pendingUserMatch={pendingUserMatch}
            onWeekChange={setViewedWeek}
            onInspect={setInspected}
          />

          <LeadersCard statistics={statistics.data} seasonId={seasonId} crestFor={crestFor} />
        </div>

        <div className="space-y-4">
          <MatchHubPreview match={pendingUserMatch} seasonId={seasonId} />

          {data.userTeamId && (
            <ClubHub team={userTeam} seasonId={seasonId} />
          )}

          {data.userTeamId && (
            <TransferCard
              state={transferWindow.data}
              offers={transferOffers.data}
              seasonId={seasonId}
            />
          )}

          <KnockoutMini bracket={cup.data} seasonId={seasonId} seasonFinished={data.isFinished} />
        </div>
      </div>

      <MatchInspectModal match={inspected} onClose={() => setInspected(null)} />

      <RolloverModal summary={rollover} onClose={() => setRollover(null)} />
    </Shell>
  )
}
