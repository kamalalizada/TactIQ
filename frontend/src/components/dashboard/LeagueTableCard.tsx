import type { UseQueryResult } from '@tanstack/react-query'

import { StandingsTable } from '@/components/StandingsTable'
import { ErrorNote, Spinner } from '@/components/States'
import type { StandingsRow } from '@/types'

interface LeagueTableCardProps {
  standings: UseQueryResult<StandingsRow[]>
  userTeamId?: string | null
}

export function LeagueTableCard({ standings, userTeamId }: LeagueTableCardProps) {
  return (
    <section className="rounded-lg border border-line bg-panel p-4">
      <h3 className="mb-3 text-sm font-semibold">League Table</h3>

      {standings.isPending && <Spinner />}
      {standings.isError && <ErrorNote error={standings.error} />}
      {standings.data && <StandingsTable rows={standings.data} userTeamId={userTeamId} />}
    </section>
  )
}
