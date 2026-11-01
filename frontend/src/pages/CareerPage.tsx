import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useParams } from 'react-router-dom'

import { careerApi } from '@/api/endpoints'
import { RolloverModal } from '@/components/career/RolloverModal'
import { Shell } from '@/components/Shell'
import { EmptyNote, ErrorNote, Spinner } from '@/components/States'
import { money } from '@/lib/money'
import type { SeasonRecord, SeasonRollover } from '@/types'

/**
 * A manager's record: what he has won, where he finished, and the chance to
 * close the year and start the next one.
 */
export function CareerPage() {
  const { seasonId = '' } = useParams()
  const queryClient = useQueryClient()

  const [rollover, setRollover] = useState<SeasonRollover | null>(null)

  const career = useQuery({
    queryKey: ['career', seasonId],
    queryFn: () => careerApi.get(seasonId),
  })

  const advance = useMutation({
    mutationFn: () => careerApi.nextSeason(seasonId),
    onSuccess: (summary) => {
      setRollover(summary)

      // A new season changes everything the app has cached about this save.
      queryClient.invalidateQueries()
    },
  })

  if (career.isPending) {
    return (
      <Shell wide>
        <Spinner />
      </Shell>
    )
  }

  if (career.isError) {
    return (
      <Shell wide>
        <h2 className="mb-5 text-xl font-bold tracking-tight">Karyera</h2>
        <ErrorNote error={career.error} />
      </Shell>
    )
  }

  const data = career.data!
  const byYear = groupByYear(data.history)

  return (
    <Shell wide>
      <header className="mb-5 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold tracking-tight">Karyera</h2>
          <p className="mt-0.5 text-sm text-muted">
            {data.userTeamName ?? 'Klub seçilməyib'} · {data.currentYear}. mövsüm
          </p>
        </div>

        <button
          type="button"
          disabled={!data.canAdvance || advance.isPending}
          onClick={() => advance.mutate()}
          title={data.canAdvance ? undefined : 'Mövsüm bitmədən keçmək olmaz'}
          className="rounded-lg bg-violet px-5 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-violet/85 disabled:cursor-not-allowed disabled:bg-white/5 disabled:text-muted"
        >
          {advance.isPending ? 'Keçirilir…' : 'Növbəti mövsüm'}
        </button>
      </header>

      {advance.isError && (
        <div className="mb-4">
          <ErrorNote error={advance.error} />
        </div>
      )}

      <div className="mb-5 grid gap-2.5 sm:grid-cols-3">
        <Stat label="Mövsüm" value={String(data.currentYear)} />
        <Stat label="Çempionluq" value={String(data.titlesWon)} accent={data.titlesWon > 0} />
        <Stat
          label="Oynanılan il"
          value={String(data.history.length > 0 ? data.currentYear - 1 : 0)}
        />
      </div>

      {data.history.length === 0 ? (
        <EmptyNote>
          Hələ bitmiş mövsüm yoxdur. İlk mövsümü bitirəndə nəticələr burada görünəcək.
        </EmptyNote>
      ) : (
        <ul className="space-y-4">
          {byYear.map(([year, records]) => (
            <li key={year}>
              <YearCard year={year} records={records} />
            </li>
          ))}
        </ul>
      )}

      <RolloverModal summary={rollover} onClose={() => setRollover(null)} />
    </Shell>
  )
}

/** The history comes back newest first; this keeps each year's leagues together. */
function groupByYear(history: SeasonRecord[]): [number, SeasonRecord[]][] {
  const years = new Map<number, SeasonRecord[]>()

  for (const record of history) {
    const existing = years.get(record.year)
    if (existing) existing.push(record)
    else years.set(record.year, [record])
  }

  return [...years.entries()].sort((a, b) => b[0] - a[0])
}

function YearCard({ year, records }: { year: number; records: SeasonRecord[] }) {
  const titles = records.filter((record) => record.userWon)

  return (
    <section className="glass-card">
      <div className="mb-2.5 flex items-center justify-between gap-3">
        <h3 className="text-sm font-semibold">{year}. mövsüm</h3>
        {titles.length > 0 && (
          <span className="rounded border border-win/30 bg-win/15 px-2 py-0.5 text-[0.65rem] font-semibold text-win">
            🏆 {titles.length === 1 ? 'Çempion' : `${titles.length} kubok`}
          </span>
        )}
      </div>

      <ul className="space-y-1">
        {records.map((record) => (
          <li
            key={record.competitionName}
            className={`flex flex-wrap items-center gap-x-2 gap-y-0.5 rounded-lg border px-3 py-2 text-sm ${
              record.userWon ? 'border-win/30 bg-win/10' : 'border-line'
            }`}
          >
            <span className="font-medium">{record.competitionName}</span>
            <span className="text-muted">{record.championTeamName ?? '—'}</span>

            {record.userRank !== null ? (
              <span className="ml-auto text-muted">
                <span className="font-bold text-ink">{record.userRank}.</span> yer
                {record.userPoints !== null && ` · ${record.userPoints} xal`}
              </span>
            ) : (
              <span className="ml-auto text-[0.7rem] text-muted">İştirak etməmisiniz</span>
            )}

            {record.userBudget !== null && (
              <span className="w-20 text-right text-[0.7rem] text-muted">
                {money(record.userBudget)}
              </span>
            )}
          </li>
        ))}
      </ul>
    </section>
  )
}

function Stat({ label, value, accent }: { label: string; value: string; accent?: boolean }) {
  return (
    <div className="glass-card">
      <div className="text-[0.7rem] text-muted">{label}</div>
      <div className={`text-2xl font-black ${accent ? 'text-win' : ''}`}>{value}</div>
    </div>
  )
}
