import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useParams } from 'react-router-dom'

import { seasonApi } from '@/api/endpoints'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { Shell } from '@/components/Shell'
import { EmptyNote, ErrorNote, Spinner } from '@/components/States'
import { PositionBadge } from '@/components/ui/Badge'
import { CompetitionKind, type PlayerStatRow, type TeamFormRow } from '@/types'

type Tab = 'scorers' | 'assists' | 'cards' | 'form'

const TABS: { id: Tab; label: string }[] = [
  { id: 'scorers', label: 'Bombardirlər' },
  { id: 'assists', label: 'Assistlər' },
  { id: 'cards', label: 'Kartlar' },
  { id: 'form', label: 'Komanda Formu' },
]

export function StatisticsPage() {
  const { seasonId = '' } = useParams()
  const [tab, setTab] = useState<Tab>('scorers')
  const [pickedId, setPickedId] = useState<string | null>(null)

  const season = useQuery({
    queryKey: ['season', seasonId],
    queryFn: () => seasonApi.get(seasonId),
  })

  // Stat rows name a team but carry no crest, so it is resolved from the squads.
  const teams = useQuery({
    queryKey: ['teams', seasonId],
    queryFn: () => seasonApi.teams(seasonId),
  })

  const leagues =
    season.data?.competitions.filter(
      (competition) => competition.kind === CompetitionKind.League,
    ) ?? []

  // Default to the league the managed club plays in, so the lists are not a
  // mix of every division in the save.
  const userTeam = teams.data?.find((team) => team.id === season.data?.userTeamId)
  const ownLeague =
    leagues.find((competition) => competition.country === userTeam?.country) ?? leagues[0]
  const selected = leagues.find((competition) => competition.id === pickedId) ?? ownLeague

  const stats = useQuery({
    queryKey: ['statistics', seasonId, selected?.id],
    queryFn: () => seasonApi.statistics(seasonId, selected?.id),
    enabled: Boolean(selected?.id),
  })

  const crestFor = (teamId: string) =>
    teams.data?.find((team) => team.id === teamId)?.logoUrl ?? null

  if (season.isPending || stats.isPending) {
    return (
      <Shell>
        <Spinner />
      </Shell>
    )
  }

  if (stats.isError) {
    return (
      <Shell>
        <ErrorNote error={stats.error} />
      </Shell>
    )
  }

  const data = stats.data!

  return (
    <Shell>
      <header className="mb-5 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold tracking-tight">Statistika</h2>
          {selected && (
            <p className="mt-0.5 text-sm text-muted">
              {selected.name} · {selected.country}
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

      <div className="mb-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile label="Oynanılan matç" value={data.matchesPlayed} />
        <StatTile label="Ümumi qol" value={data.totalGoals} />
        <StatTile label="Matç başına qol" value={data.goalsPerMatch.toFixed(2)} />
        <StatTile label="Ən böyük qələbə" value={data.biggestWin ?? '—'} small />
      </div>

      <div className="grid gap-5 lg:grid-cols-[1fr_20rem]">
        <section className="rounded-lg border border-line bg-panel">
          <div className="flex overflow-x-auto border-b border-line">
            {TABS.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => setTab(item.id)}
                aria-pressed={tab === item.id}
                className={`shrink-0 border-b-2 px-4 py-2.5 text-sm font-medium transition-colors ${
                  tab === item.id
                    ? 'border-accent text-accent'
                    : 'border-transparent text-muted hover:text-ink'
                }`}
              >
                {item.label}
              </button>
            ))}
          </div>

          <div className="scroll-list max-h-[28rem] p-1">
            {tab === 'scorers' && (
              <PlayerTable rows={data.topScorers} metric="goals" crestFor={crestFor} />
            )}
            {tab === 'assists' && (
              <PlayerTable rows={data.topAssists} metric="assists" crestFor={crestFor} />
            )}
            {tab === 'cards' && <CardsTable rows={data.mostBooked} crestFor={crestFor} />}
            {tab === 'form' && <FormTable rows={data.form} crestFor={crestFor} />}
          </div>
        </section>

        <aside>
          <h3 className="mb-2.5 text-sm font-semibold">Komanda Formu</h3>
          <div className="scroll-list max-h-[28rem] rounded-lg border border-line bg-panel p-1">
            <FormTable rows={data.form} compact crestFor={crestFor} />
          </div>
          <p className="mt-2 text-[0.7rem] text-muted">Son beş nəticə, yenidən köhnəyə.</p>
        </aside>
      </div>
    </Shell>
  )
}

function StatTile({
  label,
  value,
  small,
}: {
  label: string
  value: string | number
  small?: boolean
}) {
  return (
    <div className="rounded-lg border border-line bg-panel p-4">
      <p className="text-xs text-muted">{label}</p>
      <p className={`mt-1 font-black ${small ? 'text-lg' : 'text-3xl'}`}>{value}</p>
    </div>
  )
}

function PlayerTable({
  rows,
  metric,
  crestFor,
}: {
  rows: PlayerStatRow[]
  metric: 'goals' | 'assists'
  crestFor: (teamId: string) => string | null
}) {
  if (rows.length === 0) return <EmptyNote>Hələ məlumat yoxdur — matç oynayın.</EmptyNote>

  return (
    <table className="w-full text-sm">
      <thead className="text-xs text-muted uppercase">
        <tr>
          <th className="px-3 py-2 text-left font-medium">#</th>
          <th className="px-3 py-2 text-left font-medium">Oyunçu</th>
          <th className="px-3 py-2 text-left font-medium">Komanda</th>
          <th className="px-2 py-2 text-left font-medium">Pos</th>
          <th className="px-3 py-2 text-right font-medium text-ink">
            {metric === 'goals' ? 'Qol' : 'Assist'}
          </th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.playerId} className="border-t border-line/60 hover:bg-white/5">
            <td className="px-3 py-2 text-muted">{row.rank}</td>
            <td className="px-3 py-2 font-medium">{row.playerName}</td>
            <td className="px-3 py-2 text-muted">
              <span className="flex items-center gap-1.5">
                <TeamCrest name={row.teamName} logoUrl={crestFor(row.teamId)} size="xs" />
                <span className="truncate">{row.teamName}</span>
              </span>
            </td>
            <td className="px-2 py-2">
              <PositionBadge position={row.position} />
            </td>
            <td className="px-3 py-2 text-right font-bold">{row[metric]}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function CardsTable({
  rows,
  crestFor,
}: {
  rows: PlayerStatRow[]
  crestFor: (teamId: string) => string | null
}) {
  if (rows.length === 0) return <EmptyNote>Hələ vərəqə yoxdur.</EmptyNote>

  return (
    <table className="w-full text-sm">
      <thead className="text-xs text-muted uppercase">
        <tr>
          <th className="px-3 py-2 text-left font-medium">#</th>
          <th className="px-3 py-2 text-left font-medium">Oyunçu</th>
          <th className="px-3 py-2 text-left font-medium">Komanda</th>
          <th className="px-3 py-2 text-right font-medium text-draw">Sarı</th>
          <th className="px-3 py-2 text-right font-medium text-loss">Qırmızı</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.playerId} className="border-t border-line/60 hover:bg-white/5">
            <td className="px-3 py-2 text-muted">{row.rank}</td>
            <td className="px-3 py-2 font-medium">{row.playerName}</td>
            <td className="px-3 py-2 text-muted">
              <span className="flex items-center gap-1.5">
                <TeamCrest name={row.teamName} logoUrl={crestFor(row.teamId)} size="xs" />
                <span className="truncate">{row.teamName}</span>
              </span>
            </td>
            <td className="px-3 py-2 text-right font-bold text-draw">{row.yellowCards}</td>
            <td className="px-3 py-2 text-right font-bold text-loss">{row.redCards}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function FormTable({
  rows,
  compact,
  crestFor,
}: {
  rows: TeamFormRow[]
  compact?: boolean
  crestFor: (teamId: string) => string | null
}) {
  if (rows.length === 0) return <EmptyNote>Hələ matç oynanmayıb.</EmptyNote>

  return (
    <table className="w-full text-sm">
      {!compact && (
        <thead className="text-xs text-muted uppercase">
          <tr>
            <th className="px-3 py-2 text-left font-medium">Komanda</th>
            <th className="px-3 py-2 text-left font-medium">Forma</th>
            <th className="px-2 py-2 text-right font-medium">GF</th>
            <th className="px-2 py-2 text-right font-medium">GA</th>
            <th className="px-3 py-2 text-right font-medium text-ink">Xal</th>
          </tr>
        </thead>
      )}
      <tbody>
        {rows.map((row) => (
          <tr key={row.teamId} className="border-t border-line/60 first:border-0 hover:bg-white/5">
            <td className={`px-3 py-2 font-medium ${compact ? 'text-xs' : ''}`}>
              <span className="flex items-center gap-1.5">
                <TeamCrest name={row.teamName} logoUrl={crestFor(row.teamId)} size="xs" />
                <span className="truncate">{row.teamName}</span>
              </span>
            </td>
            <td className="px-3 py-2">
              <div className="flex gap-1">
                {row.form.map((result, index) => (
                  <FormPip key={index} result={result} />
                ))}
              </div>
            </td>
            {!compact && (
              <>
                <td className="px-2 py-2 text-right text-muted">{row.goalsFor}</td>
                <td className="px-2 py-2 text-right text-muted">{row.goalsAgainst}</td>
              </>
            )}
            <td className="px-3 py-2 text-right text-xs font-bold">
              {row.pointsFromForm}
              {compact && <span className="ml-0.5 font-normal text-muted">pts</span>}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function FormPip({ result }: { result: string }) {
  const tones: Record<string, string> = {
    W: 'bg-win text-bg',
    D: 'bg-draw text-bg',
    L: 'bg-loss text-white',
  }

  return (
    <span
      title={result}
      className={`flex size-5 items-center justify-center rounded-full text-[0.6rem] font-bold ${
        tones[result] ?? 'bg-white/10 text-muted'
      }`}
    >
      {result}
    </span>
  )
}
