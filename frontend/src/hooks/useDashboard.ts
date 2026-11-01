import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'

import { careerApi, competitionApi, cupApi, seasonApi, transferApi } from '@/api/endpoints'
import { CompetitionKind, GameMode, type Match, type SeasonRollover } from '@/types'

export function useDashboard() {
  const { seasonId = '' } = useParams()
  const queryClient = useQueryClient()

  const [viewedWeek, setViewedWeek] = useState<number | null>(null)
  const [inspected, setInspected] = useState<Match | null>(null)
  // The summary of the year just closed, shown once when it is.
  const [rollover, setRollover] = useState<SeasonRollover | null>(null)
  // Null means "follow my own club"; set it to browse another league.
  const [pickedLeagueId, setPickedLeagueId] = useState<string | null>(null)

  const season = useQuery({
    queryKey: ['season', seasonId],
    queryFn: () => seasonApi.get(seasonId),
  })

  const teams = useQuery({
    queryKey: ['teams', seasonId],
    queryFn: () => seasonApi.teams(seasonId),
  })

  const cup = useQuery({
    queryKey: ['cup', seasonId],
    queryFn: () => cupApi.bracket(seasonId),
  })

  // Only a managed club has a market to shop in, so these are allowed to fail
  // quietly in God Mode rather than failing the dashboard with them.
  const transferWindow = useQuery({
    queryKey: ['transfer-window', seasonId],
    queryFn: () => transferApi.window(seasonId),
    retry: false,
  })

  const transferOffers = useQuery({
    queryKey: ['transfer-offers', seasonId],
    queryFn: () => transferApi.offers(seasonId),
    retry: false,
  })

  const career = useQuery({
    queryKey: ['career', seasonId],
    queryFn: () => careerApi.get(seasonId),
    retry: false,
  })

  // Closing a year changes every squad, table and fixture in the save, so the
  // whole cache goes rather than trying to name what moved.
  const nextSeason = useMutation({
    mutationFn: () => careerApi.nextSeason(seasonId),
    onSuccess: (summary) => {
      setRollover(summary)
      queryClient.invalidateQueries()
    },
  })

  const leagues = season.data?.competitions.filter(
    (competition) => competition.kind === CompetitionKind.League,
  )

  // A season runs several domestic leagues. The dashboard follows the one the
  // managed club plays in, unless the manager has picked another to look at.
  const userTeamCountry = teams.data?.find((team) => team.id === season.data?.userTeamId)?.country
  const ownLeague =
    leagues?.find((competition) => competition.country === userTeamCountry) ?? leagues?.[0]
  const domestic =
    (pickedLeagueId ? leagues?.find((competition) => competition.id === pickedLeagueId) : null) ??
    ownLeague
  const competitionId = domestic?.id

  const viewingOwnLeague = domestic?.id === ownLeague?.id

  // Statistics follow the league on screen, not the whole save.
  const statistics = useQuery({
    queryKey: ['statistics', seasonId, competitionId],
    queryFn: () => seasonApi.statistics(seasonId, competitionId),
    enabled: Boolean(competitionId),
  })

  const standings = useQuery({
    queryKey: ['standings', competitionId],
    queryFn: () => competitionApi.standings(competitionId!),
    enabled: Boolean(competitionId),
  })

  // Follow the live week until the manager browses somewhere else. Changing
  // league drops back to that league's own current week.
  const currentWeek = domestic?.currentWeek

  useEffect(() => {
    setViewedWeek(null)
  }, [competitionId])

  useEffect(() => {
    if (viewedWeek === null && currentWeek !== undefined) setViewedWeek(currentWeek)
  }, [currentWeek, viewedWeek])

  const week = viewedWeek ?? currentWeek

  const fixtures = useQuery({
    queryKey: ['fixtures', competitionId, week],
    queryFn: () => competitionApi.fixtures(competitionId!, week),
    enabled: Boolean(competitionId) && week !== undefined,
  })

  const simulate = useMutation({
    // One call advances every competition; the manager's own match is settled
    // too, so the week never stalls waiting for it.
    mutationFn: () => seasonApi.simulateWeek(seasonId, true),
    onSuccess: (results) => {
      queryClient.invalidateQueries({ queryKey: ['season', seasonId] })
      queryClient.invalidateQueries({ queryKey: ['standings'] })
      queryClient.invalidateQueries({ queryKey: ['fixtures'] })
      queryClient.invalidateQueries({ queryKey: ['statistics', seasonId] })
      queryClient.invalidateQueries({ queryKey: ['cup', seasonId] })

      const league = results.find((result) => result.competitionId === competitionId)
      if (league) setViewedWeek(league.nextWeek)
    },
  })

  const data = season.data
  const isManager = data?.gameMode === GameMode.ManagerMode
  const userTeam = teams.data?.find((team) => team.id === data?.userTeamId)
  const pendingUserMatch = fixtures.data?.find((match) => match.isUserMatch && !match.isPlayed)
  const champion = standings.data?.[0]
  const cupRunning = cup.data?.started && !cup.data.finished
  const userStandingsRow = standings.data?.find((row) => row.teamId === data?.userTeamId)
  const totalTeams = standings.data?.length ?? domestic?.teamCount ?? 0

  return {
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
    ownLeague,
    leagues,
    competitionId,
    week,
    isManager,
    userTeam,
    pendingUserMatch,
    champion,
    cupRunning,
    userStandingsRow,
    totalTeams,
    viewingOwnLeague,

    simulate,

    viewedWeek,
    setViewedWeek,
    pickedLeagueId,
    setPickedLeagueId,
    inspected,
    setInspected,
  } as const
}

export type DashboardData = ReturnType<typeof useDashboard>
