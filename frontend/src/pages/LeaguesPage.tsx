import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'

import { seasonApi } from '@/api/endpoints'
import { Shell } from '@/components/Shell'
import { ErrorNote, Spinner } from '@/components/States'
import { lastLeague } from '@/lib/lastLeague'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { CompetitionKind, GameMode, type SeasonSummary } from '@/types'

export function LeaguesPage() {
  const queryClient = useQueryClient()
  const [pendingDelete, setPendingDelete] = useState<SeasonSummary | null>(null)

  const leagues = useQuery({ queryKey: ['leagues'], queryFn: seasonApi.list })

  const remove = useMutation({
    mutationFn: seasonApi.remove,
    onSuccess: (_result, deletedId) => {
      // The sidebar must not keep pointing at a league that is gone.
      lastLeague.clear(deletedId)
      queryClient.invalidateQueries({ queryKey: ['leagues'] })
      setPendingDelete(null)
    },
  })

  return (
    <Shell>
      <div className="mb-5 flex items-center justify-between gap-4">
        <h2 className="text-xl font-bold tracking-tight">Mövsümlərim</h2>
        <Link
          to="/seasons/new"
          aria-label="Yeni mövsüm"
          title="Yeni mövsüm"
          className="flex size-9 items-center justify-center rounded-lg bg-violet text-xl leading-none font-bold text-white transition-colors hover:bg-violet/85"
        >
          +
        </Link>
      </div>

      {leagues.isPending && <Spinner />}
      {leagues.isError && <ErrorNote error={leagues.error} />}

      {leagues.data?.length === 0 && (
        <div className="rounded-lg border border-line bg-panel py-14 text-center">
          <p className="text-3xl">⚽</p>
          <p className="mt-3 font-semibold">Hələ mövsümünüz yoxdur</p>
          <p className="mt-1 text-sm text-muted">Menecer karyeranıza başlamaq üçün ilk mövsümü yaradın.</p>
          <Link to="/seasons/new" className="btn-primary mt-5">
            Create your first league
          </Link>
        </div>
      )}

      <div className="space-y-3">
        {leagues.data?.map((league) => (
          <LeagueCard key={league.id} league={league} onDelete={() => setPendingDelete(league)} />
        ))}
      </div>

      <Modal
        open={Boolean(pendingDelete)}
        title="Mövsümü sil"
        onClose={() => setPendingDelete(null)}
        footer={
          <>
            <button type="button" className="btn-ghost" onClick={() => setPendingDelete(null)}>
              Cancel
            </button>
            <button
              type="button"
              className="btn-danger"
              disabled={remove.isPending}
              onClick={() => pendingDelete && remove.mutate(pendingDelete.id)}
            >
              {remove.isPending ? 'Silinir…' : 'Sil'}
            </button>
          </>
        }
      >
        <p className="text-sm">
          <span className="font-semibold">{pendingDelete?.name}</span> mövsümü və onun bütün matçları silinəcək.
        </p>
        <p className="mt-2 text-sm text-loss">Bu əməliyyat geri qaytarılmır.</p>
        {remove.isError && (
          <div className="mt-3">
            <ErrorNote error={remove.error} />
          </div>
        )}
      </Modal>
    </Shell>
  )
}

function LeagueCard({ league, onDelete }: { league: SeasonSummary; onDelete: () => void }) {
  const navigate = useNavigate()
  const isManager = league.gameMode === GameMode.ManagerMode

  // Progress is measured by the domestic league, the season's backbone.
  const domestic = league.competitions.find(
    (competition) => competition.kind === CompetitionKind.League,
  )
  const currentWeek = domestic?.currentWeek ?? 0
  const totalWeeks = domestic?.totalWeeks ?? 0
  const playedWeeks = Math.max(0, Math.min(currentWeek - 1, totalWeeks))
  const percent = totalWeeks > 0 ? (playedWeeks / totalWeeks) * 100 : 0

  return (
    <div className="flex flex-wrap items-center gap-4 rounded-lg border border-line bg-panel p-4">
      <span
        aria-hidden
        className={`flex size-11 shrink-0 items-center justify-center rounded-lg text-lg ${
          isManager ? 'bg-win/15 text-win' : 'bg-accent/15 text-accent'
        }`}
      >
        {isManager ? '🛡' : '👁'}
      </span>

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <Link to={`/seasons/${league.id}`} className="font-semibold hover:text-accent">
            {league.name}
          </Link>
          <Badge tone={isManager ? 'accent' : 'neutral'}>
            {isManager ? 'Menecer Rejimi' : 'Tanrı Rejimi'}
          </Badge>
          {league.isFinished && <Badge tone="win">Bitdi</Badge>}
        </div>

        <p className="mt-0.5 text-xs text-muted">
          {league.userTeamName && (
            <>
              <span className="text-accent">{league.userTeamName}</span>
              <span className="mx-1.5">·</span>
            </>
          )}
          {league.competitions.length} liqa · Həftə {Math.min(currentWeek, totalWeeks)} /{' '}
          {totalWeeks}
        </p>

        <div className="mt-2 h-1 max-w-sm overflow-hidden rounded-full bg-bg">
          <div
            className={`h-full transition-[width] ${league.isFinished ? 'bg-win' : 'bg-accent'}`}
            style={{ width: `${percent}%` }}
          />
        </div>
      </div>

      <p className="hidden text-xs text-muted sm:block">
        Yaradılıb{' '}
        {new Date(league.createdAt).toLocaleDateString('en-GB', {
          day: 'numeric',
          month: 'short',
          year: 'numeric',
        })}
      </p>

      <div className="flex shrink-0 items-center gap-2">
        <button
          type="button"
          className="btn-primary px-4 py-1.5 text-xs"
          onClick={() => navigate(`/seasons/${league.id}`)}
        >
          Open
        </button>
        <button
          type="button"
          aria-label={`${league.name} mövsümünü sil`}
          className="rounded px-2 py-1.5 text-loss transition-colors hover:bg-loss/10"
          onClick={onDelete}
        >
          🗑
        </button>
      </div>
    </div>
  )
}
