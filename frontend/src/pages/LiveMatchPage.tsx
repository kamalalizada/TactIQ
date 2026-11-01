import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'

import { competitionApi, matchApi } from '@/api/endpoints'
import { EventFeed } from '@/components/EventFeed'
import { GoalToast } from '@/components/GoalToast'
import { Shell } from '@/components/Shell'
import { ErrorNote, Spinner } from '@/components/States'
import { SubstitutionModal } from '@/components/SubstitutionModal'
import { TacticsPanel } from '@/components/TacticsPanel'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { useLiveMatch } from '@/lib/useLiveMatch'
import {
  LiveMatchStatus,
  TacticalStyle,
  type LiveState,
  type TacticsUpdateRequest,
} from '@/types'

const FULL_TIME = 90

export function LiveMatchPage() {
  const { matchId = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { state, connected, isFinished, lastEvent, clearLastEvent, ready, apply } =
    useLiveMatch(matchId)

  const [kickedOff, setKickedOff] = useState(false)
  const [subOpen, setSubOpen] = useState(false)

  const start = useMutation({
    mutationFn: async () => {
      // Join the hub group first, or the opening minutes arrive before we listen.
      await ready()
      return matchApi.startLive(matchId, {
        style: TacticalStyle.Balanced,
        formation: '4-4-2',
        speedMultiplier: 1,
      })
    },
    onSuccess: (next) => {
      apply(next)
      setKickedOff(true)
    },
  })

  const tactics = useMutation({
    mutationFn: (request: TacticsUpdateRequest) => matchApi.updateTactics(matchId, request),
    onSuccess: (next) => {
      apply(next)
      setSubOpen(false)
      // The squad's isStarter flags moved, so the lists must be refetched.
      queryClient.invalidateQueries({ queryKey: ['teams', next.seasonId] })
    },
  })

  const speed = useMutation({
    mutationFn: (multiplier: number) => matchApi.setSpeed(matchId, multiplier),
    onSuccess: apply,
  })

  const pause = useMutation({
    mutationFn: (paused: boolean) => (paused ? matchApi.pause(matchId) : matchApi.resume(matchId)),
    onSuccess: apply,
  })

  const finish = useMutation({
    mutationFn: () => matchApi.finish(matchId),
    onSuccess: apply,
  })

  // Once the manager's match is in, the rest of the round is played out so the
  // week completes in one go rather than needing a second click.
  const competitionId = state?.competitionId
  const [roundSettled, setRoundSettled] = useState(false)

  useEffect(() => {
    if (!isFinished || !competitionId || roundSettled) return

    setRoundSettled(true)

    // Only this competition is advanced: the other leagues in the season keep
    // their own clock and are moved on from the dashboard.
    competitionApi
      .simulateWeek(competitionId)
      .catch(() => {
        // Nothing left to play, or the season is over; the result still stands.
      })
      .finally(() => {
        queryClient.invalidateQueries({ queryKey: ['seasons'] })
        queryClient.invalidateQueries({ queryKey: ['season'] })
        queryClient.invalidateQueries({ queryKey: ['standings'] })
        queryClient.invalidateQueries({ queryKey: ['fixtures'] })
        queryClient.invalidateQueries({ queryKey: ['statistics'] })
        queryClient.invalidateQueries({ queryKey: ['cup'] })
      })
  }, [isFinished, competitionId, roundSettled, queryClient])

  if (!state && !start.isPending && !kickedOff) {
    return (
      <Shell>
        <KickOff onStart={() => start.mutate()} pending={start.isPending} error={start.error} />
      </Shell>
    )
  }

  if (!state) {
    return (
      <Shell>
        <Spinner label="Kicking off…" />
      </Shell>
    )
  }

  const isPaused = state.status === LiveMatchStatus.Paused

  return (
    <Shell>
      <GoalToast event={lastEvent} onDone={clearLastEvent} />

      <Scoreboard state={state} connected={connected} />

      {isFinished && (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button
            type="button"
            className="btn-primary"
            onClick={() => navigate(`/seasons/${state.seasonId}`)}
          >
            İdarə Panelinə qayıt
          </button>
          <span className="text-sm text-muted">
            Nəticə cədvələ yazıldı, həftənin qalan matçları oynanıldı.
          </span>
        </div>
      )}

      {finish.isError && (
        <div className="mt-3">
          <ErrorNote error={finish.error} />
        </div>
      )}

      <div className="mt-5 grid gap-5 lg:grid-cols-[1fr_19rem]">
        <section className="rounded-lg border border-line bg-panel p-4">
          <h3 className="mb-1 text-sm font-semibold">Match Events</h3>
          <EventFeed events={state.events} homeTeamId={state.homeTeamId} newestFirst />
        </section>

        <aside className="rounded-lg border border-line bg-panel p-4">
          <h3 className="mb-4 text-sm font-semibold">Tactics</h3>

          <TacticsPanel
            state={state}
            disabled={isFinished}
            paused={isPaused}
            finishing={finish.isPending}
            onStyle={(style) => tactics.mutate({ style })}
            onSpeed={(multiplier) => speed.mutate(multiplier)}
            onOpenSubstitution={() => setSubOpen(true)}
            onPause={() => pause.mutate(!isPaused)}
            onFinish={() => finish.mutate()}
          />

          {tactics.isError && !subOpen && (
            <div className="mt-3">
              <ErrorNote error={tactics.error} />
            </div>
          )}
        </aside>
      </div>

      <SubstitutionModal
        open={subOpen}
        squad={state.squad}
        remaining={state.substitutionsRemaining}
        pending={tactics.isPending}
        error={tactics.isError ? tactics.error : null}
        onClose={() => setSubOpen(false)}
        onConfirm={(playerOutId, playerInId) =>
          tactics.mutate({ substitution: { playerOutId, playerInId } })
        }
      />
    </Shell>
  )
}

function KickOff({
  onStart,
  pending,
  error,
}: {
  onStart: () => void
  pending: boolean
  error: unknown
}) {
  return (
    <div className="mx-auto max-w-md">
      <div className="card space-y-4 text-center">
        <h2 className="text-xl font-bold">Ready for kick-off</h2>
        <p className="text-sm text-muted">
          The match runs minute by minute. Change tactics, swap players and adjust the speed at any
          point.
        </p>
        {error ? <ErrorNote error={error} /> : null}
        <button type="button" className="btn-primary w-full" onClick={onStart} disabled={pending}>
          {pending ? 'Starting…' : 'Kick off'}
        </button>
      </div>
    </div>
  )
}

function Scoreboard({ state, connected }: { state: LiveState; connected: boolean }) {
  const finished = state.status === LiveMatchStatus.Finished

  return (
    <div className="rounded-lg border border-line bg-panel px-5 py-4">
      <div className="flex items-center justify-between gap-4">
        <TeamSide name={state.homeTeamName} align="left" />

        <div className="shrink-0 text-center">
          <div className="text-3xl font-black sm:text-4xl">
            {state.homeGoals} <span className="text-muted">-</span> {state.awayGoals}
          </div>
          <div className="mt-0.5 text-xs font-semibold text-win">
            {finished ? "Full time" : `${state.minute}′`}
            {state.status === LiveMatchStatus.Paused && ' · paused'}
          </div>
        </div>

        <TeamSide name={state.awayTeamName} align="right" />
      </div>

      <div className="mt-3 flex items-center gap-3">
        <div className="h-1 flex-1 overflow-hidden rounded-full bg-bg">
          <div
            className={`h-full transition-[width] duration-500 ${finished ? 'bg-win' : 'bg-accent'}`}
            style={{ width: `${Math.min(100, (state.minute / FULL_TIME) * 100)}%` }}
          />
        </div>
        <span className="shrink-0 text-xs text-muted">
          {state.minute} / {FULL_TIME}
        </span>
      </div>

      {!connected && !finished && (
        <p className="mt-2 text-center text-xs text-draw">Reconnecting…</p>
      )}
    </div>
  )
}

function TeamSide({ name, align }: { name: string; align: 'left' | 'right' }) {
  return (
    <div
      className={`flex min-w-0 flex-1 items-center gap-2.5 ${
        align === 'right' ? 'flex-row-reverse' : ''
      }`}
    >
      <TeamCrest name={name} size="md" />
      <span className="truncate font-semibold">{name}</span>
    </div>
  )
}
