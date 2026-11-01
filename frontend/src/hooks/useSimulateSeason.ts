import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import { seasonApi } from '@/api/endpoints'

/** Guards against a runaway loop if the backend ever stops advancing. */
const MAX_WEEKS = 60

export type SeasonRunOutcome = { reason: 'finished'; weeksPlayed: number }

/**
 * Plays the season out week by week. There is no season-wide endpoint, so this
 * calls simulate-week repeatedly; the manager's own matches are settled along
 * with the rest, so it runs to the end of the season.
 */
export function useSimulateSeason(seasonId: string) {
  const queryClient = useQueryClient()
  const [weeksPlayed, setWeeksPlayed] = useState(0)

  const run = useMutation<SeasonRunOutcome>({
    mutationFn: async () => {
      let played = 0

      for (let guard = 0; guard < MAX_WEEKS; guard++) {
        const results = await seasonApi.simulateWeek(seasonId, true)

        // Nothing left to play: every competition has finished.
        if (results.length === 0) break

        played++
        setWeeksPlayed(played)

        if (results.every((result) => result.competitionFinished)) break
      }

      return { reason: 'finished', weeksPlayed: played }
    },
    onSettled: () => {
      setWeeksPlayed(0)
      queryClient.invalidateQueries({ queryKey: ['seasons'] })
      queryClient.invalidateQueries({ queryKey: ['season', seasonId] })
      queryClient.invalidateQueries({ queryKey: ['standings'] })
      queryClient.invalidateQueries({ queryKey: ['fixtures'] })
      queryClient.invalidateQueries({ queryKey: ['statistics', seasonId] })
      queryClient.invalidateQueries({ queryKey: ['cup', seasonId] })
    },
  })

  return { run, weeksPlayed }
}
