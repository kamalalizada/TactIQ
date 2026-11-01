import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'

import { seasonApi } from '@/api/endpoints'
import { lastLeague } from '@/lib/lastLeague'

/**
 * Which season the navigation should point at.
 *
 * The URL wins when it names one. Otherwise the last opened season is used,
 * and failing that the newest one the manager owns — so the menu is usable on
 * a first visit rather than sitting greyed out until a season is opened.
 */
export function useActiveSeason(): string | undefined {
  const { seasonId } = useParams()

  // Kept in state so opening a season re-enables the menu immediately; a bare
  // localStorage read would not trigger a re-render.
  const [rememberedId, setRememberedId] = useState(() => lastLeague.get())

  useEffect(() => {
    if (!seasonId) return
    lastLeague.set(seasonId)
    setRememberedId(seasonId)
  }, [seasonId])

  // Only consulted when nothing else has named a season.
  const seasons = useQuery({
    queryKey: ['seasons'],
    queryFn: seasonApi.list,
    enabled: !seasonId && !rememberedId,
  })

  return seasonId ?? rememberedId ?? seasons.data?.[0]?.id
}
