import { useQuery } from '@tanstack/react-query'

import { seasonApi } from '@/api/endpoints'
import type { Team } from '@/types'

/**
 * Loads the squad the user manages in the given season, which is what the
 * substitution lists need to know who is on the pitch and who is on the bench.
 */
export function useManagedSquad(seasonId: string | undefined): {
  squad: Team | undefined
  isPending: boolean
} {
  const season = useQuery({
    queryKey: ['season', seasonId],
    queryFn: () => seasonApi.get(seasonId!),
    enabled: Boolean(seasonId),
  })

  const teams = useQuery({
    queryKey: ['teams', seasonId],
    queryFn: () => seasonApi.teams(seasonId!),
    enabled: Boolean(seasonId),
  })

  return {
    squad: teams.data?.find((team) => team.id === season.data?.userTeamId),
    isPending: season.isPending || teams.isPending,
  }
}
