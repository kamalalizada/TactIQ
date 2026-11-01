import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'

import { competitionApi, seasonApi } from '@/api/endpoints'
import { Shell } from '@/components/Shell'
import { StandingsTable } from '@/components/StandingsTable'
import { EmptyNote, ErrorNote, Spinner } from '@/components/States'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { CompetitionKind } from '@/types'

/** The full league table, opening on whichever league the manager's club is in. */
export function LeagueTablePage() {
  const { seasonId = '' } = useParams()
  const [pickedId, setPickedId] = useState<string | null>(null)

  const season = useQuery({
    queryKey: ['season', seasonId],
    queryFn: () => seasonApi.get(seasonId),
  })

  const teams = useQuery({
    queryKey: ['teams', seasonId],
    queryFn: () => seasonApi.teams(seasonId),
  })

  const leagues =
    season.data?.competitions.filter(
      (competition) => competition.kind === CompetitionKind.League,
    ) ?? []

  // Default to the league the managed club plays in.
  const userTeam = teams.data?.find((team) => team.id === season.data?.userTeamId)
  const ownLeague =
    leagues.find((competition) => competition.country === userTeam?.country) ?? leagues[0]

  const selected = leagues.find((competition) => competition.id === pickedId) ?? ownLeague

  // Once the season loads, settle on the club's own league.
  useEffect(() => {
    if (pickedId === null && ownLeague) setPickedId(ownLeague.id)
  }, [ownLeague, pickedId])

  const standings = useQuery({
    queryKey: ['standings', selected?.id],
    queryFn: () => competitionApi.standings(selected!.id),
    enabled: Boolean(selected?.id),
  })

  if (season.isPending) {
    return (
      <Shell wide>
        <Spinner />
      </Shell>
    )
  }

  if (season.isError) {
    return (
      <Shell wide>
        <ErrorNote error={season.error} />
      </Shell>
    )
  }

  return (
    <Shell wide>
      <header className="mb-5 flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          <h2 className="text-xl font-bold tracking-tight">Xal Cədvəli</h2>

          {selected && (
            <p className="mt-0.5 text-sm text-muted">
              {selected.country} · Həftə {Math.min(selected.currentWeek, selected.totalWeeks)} /{' '}
              {selected.totalWeeks}
              {userTeam && selected.id === ownLeague?.id && (
                <>
                  <span className="mx-1.5">·</span>
                  <span className="inline-flex items-center gap-1 text-accent align-middle">
                    <TeamCrest name={userTeam.name} logoUrl={userTeam.logoUrl} size="xs" />
                    {userTeam.name}
                  </span>
                </>
              )}
            </p>
          )}
        </div>

        {leagues.length > 1 && selected && (
          <select
            aria-label="Liqa seç"
            value={selected.id}
            onChange={(event) => setPickedId(event.target.value)}
            className="rounded-md border border-line bg-panel px-3 py-2 text-sm text-ink outline-none focus:border-violet"
          >
            {leagues.map((competition) => (
              <option key={competition.id} value={competition.id}>
                {competition.name}
              </option>
            ))}
          </select>
        )}
      </header>

      <section className="glass-card">
        {standings.isPending && <Spinner />}
        {standings.isError && <ErrorNote error={standings.error} />}
        {standings.data?.length === 0 && <EmptyNote>Bu liqada hələ matç oynanmayıb.</EmptyNote>}
        {standings.data && standings.data.length > 0 && (
          <StandingsTable rows={standings.data} userTeamId={season.data?.userTeamId} scroll={false} />
        )}
      </section>

      <p className="mt-4 text-center text-sm text-muted">
        <Link to={`/seasons/${seasonId}`} className="text-accent hover:underline">
          İdarə panelinə qayıt
        </Link>
      </p>
    </Shell>
  )
}
