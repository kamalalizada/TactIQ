import type { UseQueryResult } from '@tanstack/react-query'

import { FixtureList } from '@/components/FixtureList'
import { EmptyNote, Spinner } from '@/components/States'
import type { Match } from '@/types'

interface FixturesCardProps {
  fixtures: UseQueryResult<Match[]>
  week: number | undefined
  totalWeeks: number
  pendingUserMatch: Match | undefined
  onWeekChange: (week: number) => void
  onInspect: (match: Match) => void
}

export function FixturesCard({
  fixtures,
  week,
  totalWeeks,
  pendingUserMatch,
  onWeekChange,
  onInspect,
}: FixturesCardProps) {
  return (
    <section className="glass-card flex flex-col">
      <h3 className="mb-3 text-sm font-semibold">Bu Həftənin Matçları</h3>

      <div className="flex-1">
        {fixtures.isPending && <Spinner />}
        {fixtures.data?.length === 0 && <EmptyNote>Bu həftə matç yoxdur.</EmptyNote>}

        {fixtures.data && week !== undefined && (
          <FixtureList
            matches={fixtures.data}
            week={week}
            totalWeeks={totalWeeks}
            onWeekChange={onWeekChange}
            onInspect={onInspect}
          />
        )}
      </div>

      {/* Simulating is driven from the header and the match hub, not here. */}
      {pendingUserMatch && (
        <p className="mt-3 text-center text-xs text-muted">
          Həftəni tamamlamaq üçün öz matçınızı oynayın.
        </p>
      )}
    </section>
  )
}
