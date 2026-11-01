import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  MouseSensor,
  TouchSensor,
  closestCenter,
  pointerWithin,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type Active,
  type CollisionDetection,
  type DragEndEvent,
  type DragMoveEvent,
  type DragStartEvent,
  type Over,
} from '@dnd-kit/core'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'

import { seasonApi } from '@/api/endpoints'
import { PitchView, TokenFace } from '@/components/PitchView'
import { Shell } from '@/components/Shell'
import { EmptyNote, ErrorNote, Spinner } from '@/components/States'
import { PositionBadge, RatingStar, RoleChip } from '@/components/ui/Badge'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { FORMATION_SLOTS, fitOf, lineOf, pickForFormation, ratingAs } from '@/lib/formations'
import { inPitchOrder, moveTo, substitute, swapSpots, type Spots } from '@/lib/lineup'
import {
  PITCH_DROP_ID,
  clampSpot,
  lineupProblem,
  presetSpots,
  roleAt,
  shapeLabel,
  type Spot,
} from '@/lib/pitch'
import { Position, ROLE_LABELS, type Player, type PlayerRole } from '@/types'

const POSITION_ORDER: Position[] = [Position.GK, Position.DEF, Position.MID, Position.FWD]

/** The shapes a manager may pick, in the order the picker lists them. */
const FORMATIONS = Object.keys(FORMATION_SLOTS)

/** How long a refused drop explains itself before the usual hint returns. */
const NOTICE_MS = 3500

/** The id a bench player carries while it is dragged or dropped on. */
const benchDragId = (playerId: string) => `bench:${playerId}`

/** Splits a drag id back into where the player was and who they are. */
function parseDragId(id: string | number): { from: 'starter' | 'bench'; playerId: string } | null {
  const [from, ...rest] = String(id).split(':')
  if (from !== 'starter' && from !== 'bench') return null
  return { from, playerId: rest.join(':') }
}

/**
 * Drops land on whatever is under the pointer, and a shirt beats the grass it
 * stands on, so letting go on a team-mate swaps the two while letting go on
 * open grass moves the player there. A keyboard drag has no pointer, so it
 * falls back to the nearest target.
 */
const dropTarget: CollisionDetection = (args) => {
  const hits = args.pointerCoordinates ? pointerWithin(args) : closestCenter(args)
  const shirt = hits.find((hit) => hit.id !== PITCH_DROP_ID)
  return shirt ? [shirt] : hits
}

function byPosition(a: Player, b: Player) {
  const byLine = POSITION_ORDER.indexOf(a.position) - POSITION_ORDER.indexOf(b.position)
  return byLine !== 0 ? byLine : a.shirtNumber - b.shirtNumber
}

const rolesOf = (spots: Spots) => Object.values(spots).map(roleAt)

/** What letting go would do: the line-up it leaves, the dragged player's job in it, and any rule it breaks. */
interface DropOutcome {
  next: Spots
  role: PlayerRole
  problem: string | null
  playerId: string
}

export function SquadPage() {
  const { seasonId = '' } = useParams()
  const queryClient = useQueryClient()

  const [selected, setSelected] = useState<Player | null>(null)
  // The preset shape last picked, which the backend keeps alongside the spots.
  const [formation, setFormation] = useState<string | null>(null)
  // Null until the squad loads; after that, where each starter stands.
  const [spots, setSpots] = useState<Spots | null>(null)
  // The player marked to come off, waiting for a replacement to be picked.
  const [swapOutId, setSwapOutId] = useState<string | null>(null)
  // Whoever is being dragged, drawn under the pointer until they are dropped.
  const [dragging, setDragging] = useState<{
    player: Player
    from: 'starter' | 'bench'
    role: PlayerRole
  } | null>(null)
  // What letting go right now would do, so the dragged shirt can show it.
  const [preview, setPreview] = useState<{ role: PlayerRole; problem: string | null } | null>(null)
  // Why the last drop was turned away, shown briefly under the pitch.
  const [notice, setNotice] = useState<string | null>(null)

  const pitchRef = useRef<HTMLDivElement>(null)

  // A small move or a short hold before a drag starts, so a click still selects
  // and a finger can still scroll the bench.
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 180, tolerance: 6 } }),
    useSensor(KeyboardSensor),
  )

  // A press anywhere but on a player, or Escape, lets go of whoever is picked.
  // Players carry data-pick so a press on one — or the start of a drag — is
  // left to their own handlers.
  useEffect(() => {
    const clear = () => {
      setSwapOutId(null)
      setSelected(null)
    }

    const onPointerDown = (event: PointerEvent) => {
      if (event.target instanceof Element && event.target.closest('[data-pick]')) return
      clear()
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') clear()
    }

    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [])

  useEffect(() => {
    if (!notice) return
    const timer = window.setTimeout(() => setNotice(null), NOTICE_MS)
    return () => window.clearTimeout(timer)
  }, [notice])

  const season = useQuery({
    queryKey: ['season', seasonId],
    queryFn: () => seasonApi.get(seasonId),
  })

  const teams = useQuery({
    queryKey: ['teams', seasonId],
    queryFn: () => seasonApi.teams(seasonId),
  })

  const squadTeam = teams.data?.find((team) => team.id === season.data?.userTeamId)

  // Start from what the club is already set up to play.
  useEffect(() => {
    if (!squadTeam || spots !== null) return

    const shape = FORMATIONS.includes(squadTeam.formation) ? squadTeam.formation : '4-4-2'
    setFormation(shape)

    const saved = squadTeam.players.filter((p) => p.isStarter)

    // A side the manager arranged by hand stands exactly where it was left.
    // Anything but a number — null, or missing from an older API — means the
    // player was never placed, and the shape lays them out instead.
    const placedByHand = saved.flatMap((p) =>
      typeof p.pitchX === 'number' && typeof p.pitchY === 'number'
        ? [[p.id, { x: p.pitchX, y: p.pitchY }] as const]
        : [],
    )

    if (saved.length === 11 && placedByHand.length === 11) {
      setSpots(Object.fromEntries(placedByHand))
      return
    }

    // Otherwise it is laid out from the shape. The saved order names each
    // player's slot only when every starter has one and there are eleven; a
    // squad left short by injuries is picked again from scratch.
    const bySlot = [...saved].sort((a, b) => a.lineupSlot - b.lineupSlot)
    const ordered =
      bySlot.length === 11 && bySlot.every((p, index) => p.lineupSlot === index + 1)
        ? bySlot.map((p) => p.id)
        : pickForFormation(shape, squadTeam.players)

    setSpots(presetSpots(shape, ordered))
  }, [squadTeam, spots])

  const save = useMutation({
    mutationFn: () => {
      const placed = spots ?? {}
      return seasonApi.setLineup(seasonId, {
        formation: formation ?? '4-4-2',
        starters: inPitchOrder(placed).map((id) => ({ playerId: id, ...placed[id] })),
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teams', seasonId] })
    },
  })

  if (teams.isPending || season.isPending) {
    return (
      <Shell wide>
        <Spinner />
      </Shell>
    )
  }

  if (teams.isError) {
    return (
      <Shell wide>
        <ErrorNote error={teams.error} />
      </Shell>
    )
  }

  const squad = squadTeam

  if (!squad) {
    return (
      <Shell wide>
        <h2 className="mb-5 text-xl font-bold tracking-tight">Heyət</h2>
        <EmptyNote>
          Bu mövsümdə komandanız yoxdur. Bütün komandalara{' '}
          <Link to={`/seasons/${seasonId}/teams`} className="text-accent hover:underline">
            Komandalar
          </Link>{' '}
          bölməsindən baxa bilərsiniz.
        </EmptyNote>
      </Shell>
    )
  }

  const placed = spots ?? {}
  const shape = formation ?? '4-4-2'

  /** The job the spot a starter stands on asks of them, or null for the bench. */
  const roleOf = (playerId: string): PlayerRole | null =>
    placed[playerId] ? roleAt(placed[playerId]) : null

  const starters = inPitchOrder(placed)
    .map((id) => squad.players.find((p) => p.id === id))
    .filter((player): player is Player => Boolean(player))
  const bench = squad.players.filter((p) => !placed[p.id]).sort(byPosition)
  const shown = selected ?? starters[0]

  const unavailable = starters.filter((p) => !p.isAvailable)
  const problem = lineupProblem(rolesOf(placed))

  // Anyone may stand anywhere: the spot names the job, and a player away from
  // their own role simply plays below their rating. What a side cannot do is
  // field the wrong number of men, play someone injured or suspended, or break
  // the shape's rules — one keeper, three at the back, one of each flank role.
  const canSave = starters.length === 11 && unavailable.length === 0 && problem === null

  const swapOut = starters.find((p) => p.id === swapOutId) ?? null

  /**
   * Tapping the pitch: the first tap marks a player, the second either swaps
   * the two around or clears the mark if it was the same player.
   */
  const tapStarter = (player: Player) => {
    setSelected(player)

    if (!swapOut) {
      setSwapOutId(player.id)
      return
    }

    if (swapOut.id === player.id) {
      setSwapOutId(null)
      return
    }

    setSpots(swapSpots(placed, swapOut.id, player.id))
    setSwapOutId(null)
  }

  /**
   * Tapping the bench brings that player on. With someone marked, they take
   * that exact spot; otherwise the weakest player in the same line makes way,
   * so a single tap still works.
   */
  const bringOn = (benchPlayer: Player) => {
    const outgoing = swapOut
      ? swapOut
      : [...starters]
          .filter((p) => {
            const role = roleOf(p.id)
            return role !== null && lineOf(role) === benchPlayer.position
          })
          // Give up the shirt that suits its job least, and among equals the
          // weakest player, so a single tap does not unseat a specialist.
          .sort((a, b) => {
            const roleA = roleOf(a.id)
            const roleB = roleOf(b.id)
            const fitA = roleA === null ? 1 : fitOf(a.role, roleA) === 'native' ? 1 : 0
            const fitB = roleB === null ? 1 : fitOf(b.role, roleB) === 'native' ? 1 : 0
            return fitA - fitB || a.overall - b.overall
          })[0]

    if (!outgoing) return

    setSpots(substitute(placed, outgoing.id, benchPlayer.id))
    setSelected(benchPlayer)
    setSwapOutId(null)
  }

  /**
   * Where the dragged shirt's centre would land, as a spot, or null if it is
   * off the grass. The shirt is drawn centred on its spot, so its centre is
   * what the manager is aiming with.
   */
  const landing = (active: Active, delta: { x: number; y: number }): Spot | null => {
    const pitch = pitchRef.current?.getBoundingClientRect()
    const start = active.rect.current.initial
    if (!pitch || !start) return null

    const x = start.left + start.width / 2 + delta.x
    const y = start.top + start.height / 2 + delta.y
    if (x < pitch.left || x > pitch.right || y < pitch.top || y > pitch.bottom) return null

    return clampSpot({
      x: ((x - pitch.left) / pitch.width) * 100,
      y: ((pitch.bottom - y) / pitch.height) * 100,
    })
  }

  /**
   * What letting go would do. Two starters trade spots, and a starter and a
   * substitute trade between pitch and bench — neither changes the jobs on
   * the pitch, so neither can break a rule. A starter let go on open grass
   * moves there and takes the job that spot asks for, which can.
   */
  const outcomeOf = (active: Active, over: Over | null, delta: { x: number; y: number }): DropOutcome | null => {
    const source = parseDragId(active.id)
    if (!source || !over || over.id === active.id) return null

    if (over.id === PITCH_DROP_ID) {
      if (source.from !== 'starter') return null

      const spot = landing(active, delta)
      if (!spot) return null

      const next = moveTo(placed, source.playerId, spot)
      return { next, role: roleAt(spot), problem: lineupProblem(rolesOf(next)), playerId: source.playerId }
    }

    const target = parseDragId(over.id)
    if (!target) return null

    if (source.from === 'starter' && target.from === 'starter') {
      return {
        next: swapSpots(placed, source.playerId, target.playerId),
        role: roleAt(placed[target.playerId]),
        problem: null,
        playerId: source.playerId,
      }
    }

    if (source.from === 'bench' && target.from === 'starter') {
      return {
        next: substitute(placed, target.playerId, source.playerId),
        role: roleAt(placed[target.playerId]),
        problem: null,
        playerId: source.playerId,
      }
    }

    if (source.from === 'starter' && target.from === 'bench') {
      const player = squad.players.find((p) => p.id === source.playerId)
      return {
        next: substitute(placed, source.playerId, target.playerId),
        role: player?.role ?? roleAt(placed[source.playerId]),
        problem: null,
        playerId: source.playerId,
      }
    }

    return null
  }

  const startDrag = ({ active }: DragStartEvent) => {
    const source = parseDragId(active.id)
    const player = squad.players.find((p) => p.id === source?.playerId)
    if (!source || !player) return

    setNotice(null)
    setDragging({
      player,
      from: source.from,
      role: (source.from === 'starter' ? roleOf(player.id) : null) ?? player.role,
    })
  }

  const moveDrag = ({ active, over, delta }: DragMoveEvent) => {
    const outcome = outcomeOf(active, over, delta)
    const next = outcome ? { role: outcome.role, problem: outcome.problem } : null

    // Only re-render when what the shirt would show actually changes.
    setPreview((current) =>
      current?.role === next?.role && current?.problem === next?.problem ? current : next,
    )
  }

  const endDrag = ({ active, over, delta }: DragEndEvent) => {
    const outcome = outcomeOf(active, over, delta)
    setDragging(null)
    setPreview(null)

    if (!outcome) return

    // A drop that would break the side is turned away: the player goes back
    // where they were, and the manager is told why.
    if (outcome.problem) {
      setNotice(outcome.problem)
      return
    }

    setSpots(outcome.next)
    setSwapOutId(null)
    setSelected(squad.players.find((p) => p.id === outcome.playerId) ?? null)
  }

  const cancelDrag = () => {
    setDragging(null)
    setPreview(null)
  }

  const applyFormation = (next: string) => {
    setFormation(next)
    setSwapOutId(null)

    // Fill each slot with the player who actually plays that job, so switching
    // shape puts a right-back at right-back rather than the next best defender.
    setSpots(presetSpots(next, pickForFormation(next, squad.players, inPitchOrder(placed))))
  }

  return (
    <Shell wide>
      <header className="mb-5 flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <TeamCrest name={squad.name} logoUrl={squad.logoUrl} size="lg" />
          <div>
            <h2 className="text-xl font-bold tracking-tight">{squad.name}</h2>
            <p className="mt-0.5 text-sm text-muted">
              İlk 11: {starters.length} · Ehtiyat: {bench.length}
            </p>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <label className="flex items-center gap-2 text-sm">
            <span className="text-muted">Düzülüş</span>
            <select
              value={shape}
              onChange={(event) => applyFormation(event.target.value)}
              className="rounded-md border border-line bg-panel px-3 py-2 text-sm text-ink outline-none focus:border-violet"
            >
              {FORMATIONS.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </label>

          <button
            type="button"
            onClick={() => save.mutate()}
            disabled={!canSave || save.isPending}
            title={
              problem ??
              (unavailable.length > 0
                ? `Oynaya bilməyən oyunçular ilk 11-dədir: ${unavailable.map((p) => p.name).join(', ')}`
                : undefined)
            }
            className="rounded-lg bg-violet px-5 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-violet/85 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {save.isPending ? 'Yadda saxlanılır…' : 'Heyəti Yadda Saxla'}
          </button>
        </div>
      </header>

      {save.isError && (
        <div className="mb-4">
          <ErrorNote error={save.error} />
        </div>
      )}

      {save.isSuccess && canSave && (
        <p className="mb-4 rounded-md border border-win/30 bg-win/10 px-3 py-2 text-sm text-win">
          Heyət yadda saxlanıldı.
        </p>
      )}

      <DndContext
        sensors={sensors}
        collisionDetection={dropTarget}
        onDragStart={startDrag}
        onDragMove={moveDrag}
        onDragEnd={endDrag}
        onDragCancel={cancelDrag}
      >
        <div className="grid gap-5 lg:justify-center lg:grid-cols-[16rem_minmax(0,42rem)_18rem]">
          <section className="order-2 lg:order-none">
            <h3 className="mb-1 text-sm font-semibold">Ehtiyat ({bench.length})</h3>
            <p className="mb-2.5 text-[0.7rem] text-muted">
              {swapOut
                ? `${swapOut.name} əvəzinə girəcək oyunçunu seçin.`
                : 'Oyunçunu meydana sürükləyin, ya da basın: eyni mövqedən ən zəifi ilə yer dəyişir.'}
            </p>

            <ul className="scroll-list max-h-[56rem] space-y-0.5 rounded-xl bg-linear-to-b from-[#1a2a44] to-[#0c1524] p-1.5 shadow-lg">
              {bench.map((player) => (
                <li key={player.id}>
                  <BenchRow
                    player={player}
                    shown={player.id === selected?.id}
                    swapOutName={swapOut?.name ?? null}
                    onPick={bringOn}
                  />
                </li>
              ))}
            </ul>
          </section>

          <section>
            <PitchView
              players={starters}
              spots={placed}
              selectedId={swapOutId ?? selected?.id}
              onSelect={tapStarter}
              formation={shapeLabel(rolesOf(placed))}
              draggable
              showZones={dragging?.from === 'starter'}
              pitchRef={pitchRef}
            />

            {notice ? (
              <p role="status" className="mt-2.5 text-sm font-medium text-loss">
                ✕ {notice}
              </p>
            ) : swapOut ? (
              <p className="mt-2.5 flex flex-wrap items-center gap-2 rounded-md border border-violet/40 bg-violet/10 px-3 py-2 text-sm">
                <span className="font-semibold">{swapOut.name}</span>
                <span className="text-muted">
                  seçildi — yerini dəyişmək üçün meydandan, əvəz etmək üçün ehtiyatdan seçin
                </span>
                <button
                  type="button"
                  onClick={() => setSwapOutId(null)}
                  className="ml-auto text-xs text-muted underline hover:text-ink"
                >
                  imtina
                </button>
              </p>
            ) : (
              <p className="mt-2.5 text-[0.7rem] text-muted">
                Oyunçunu meydanın istənilən yerinə sürükləyin: dayandığı yer onun mövqeyini
                təyin edir. Başqasının üstünə buraxanda yerlərini dəyişir, ehtiyatla isə əvəz
                olunur.
              </p>
            )}

            <div className="mt-2.5 flex flex-wrap gap-4 text-[0.7rem] text-muted">
              <Key colour="text-[#ffb347]" label="Forması yerində" />
              <Key colour="text-[#d6d6d6]" label="Yorğun" />
              <Key colour="text-loss" label="Tükənmiş" />
            </div>
          </section>

          <aside className="order-3 lg:order-none">
            {shown && <PlayerDetail player={shown} playedRole={roleOf(shown.id)} />}
          </aside>
        </div>

        <DragOverlay dropAnimation={null}>
          {dragging && (
            <div
              className={`flex cursor-grabbing flex-col items-center drop-shadow-xl ${
                preview?.problem ? 'opacity-50 grayscale' : ''
              }`}
            >
              <TokenFace
                player={dragging.player}
                role={preview?.role ?? dragging.role}
                mark="selected"
              />
            </div>
          )}
        </DragOverlay>
      </DndContext>
    </Shell>
  )
}

/**
 * A substitute on the bench sheet. Tapping brings them on; they can also be
 * dragged onto a shirt on the pitch, or have a starter dropped on them.
 */
function BenchRow({
  player,
  shown,
  swapOutName,
  onPick,
}: {
  player: Player
  shown: boolean
  swapOutName: string | null
  onPick: (player: Player) => void
}) {
  const id = benchDragId(player.id)
  // Someone injured or suspended can neither come on nor be swapped in.
  const drag = useDraggable({ id, data: { player }, disabled: !player.isAvailable })
  const drop = useDroppable({ id, data: { player }, disabled: !player.isAvailable })

  return (
    <button
      type="button"
      ref={(node) => {
        drag.setNodeRef(node)
        drop.setNodeRef(node)
      }}
      {...drag.attributes}
      {...drag.listeners}
      data-pick
      onClick={() => onPick(player)}
      disabled={!player.isAvailable}
      title={
        !player.isAvailable
          ? 'Bu oyunçu oynaya bilməz'
          : swapOutName
            ? `${player.name}, ${swapOutName} əvəzinə girsin`
            : `${player.name} oyuna girsin`
      }
      className={`flex w-full items-center gap-2.5 rounded-md px-2 py-1 text-left transition-colors ${
        drop.isOver && !drag.isDragging
          ? 'bg-white/10 outline-2 outline-white outline-dashed'
          : shown
            ? 'bg-white/15 outline-2 outline-white/80 outline-solid'
            : 'hover:bg-white/5'
      } ${
        !player.isAvailable
          ? 'cursor-not-allowed opacity-40'
          : drag.isDragging
            ? 'opacity-30'
            : 'cursor-grab active:cursor-grabbing'
      }`}
    >
      <span className="min-w-0 flex-1 truncate text-right font-pitch text-[1.15rem] leading-tight font-bold tracking-wide text-white uppercase">
        {player.name}
      </span>
      <span className="flex w-12 shrink-0 flex-col items-start">
        <RatingStar overall={player.overall} condition={player.condition} />
        <RoleChip role={player.role} />
      </span>
    </button>
  )
}

function Key({ colour, label }: { colour: string; label: string }) {
  return (
    <span className="flex items-center gap-1.5">
      <span aria-hidden className={`text-sm leading-none ${colour}`}>
        ★
      </span>
      {label}
    </span>
  )
}

const ATTRIBUTES = [
  { key: 'pace', label: 'SÜR' },
  { key: 'shooting', label: 'ZƏR' },
  { key: 'passing', label: 'PAS' },
  { key: 'dribbling', label: 'DRİ' },
  { key: 'defending', label: 'MÜD' },
  { key: 'physical', label: 'FİZ' },
] as const

/**
 * A keeper is judged on different things entirely. Showing him pace and
 * shooting says nothing about whether he will keep the ball out.
 */
const KEEPER_ATTRIBUTES = [
  { key: 'reflexes', label: 'REF' },
  { key: 'positioning', label: 'MÖV' },
  { key: 'handling', label: 'TUT' },
  { key: 'diving', label: 'UÇU' },
  { key: 'kicking', label: 'VUR' },
  { key: 'speed', label: 'SÜR' },
] as const

function PlayerDetail({ player, playedRole }: { player: Player; playedRole: PlayerRole | null }) {
  // A starter away from their own role is worth less there; say how much.
  const away = playedRole !== null && playedRole !== player.role ? playedRole : null

  return (
    <section className="glass-card">
      <div className="mb-3 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate font-semibold">{player.name}</p>
          <div className="mt-1 flex items-center gap-2">
            <PositionBadge position={player.position} />
            <span className="rounded border border-line px-1 text-[0.6rem] tracking-wide text-muted">
              {ROLE_LABELS[player.role]}
            </span>
            <span className="text-xs text-muted">
              #{player.shirtNumber} · {player.age} yaş
            </span>
          </div>
          <p className="mt-0.5 text-xs text-muted">{player.nationality}</p>
        </div>

        <div className="shrink-0 text-right">
          <div className="text-3xl leading-none font-black">
            {away === null ? player.overall : ratingAs(player, away)}
          </div>
          <div className="text-[0.6rem] tracking-wide text-muted uppercase">
            {away === null ? 'OVR' : `${ROLE_LABELS[away]} kimi · əsl ${player.overall}`}
          </div>
        </div>
      </div>

      <dl className="space-y-1.5">
        {(player.position === Position.GK && player.reflexes > 0
          ? KEEPER_ATTRIBUTES
          : ATTRIBUTES
        ).map((attribute) => (
          <div key={attribute.key} className="flex items-center gap-2">
            <dt className="w-8 shrink-0 text-[0.65rem] text-muted">{attribute.label}</dt>
            <span className="h-1.5 flex-1 overflow-hidden rounded-full bg-bg">
              <span
                className="progress-gradient block h-full"
                style={{ width: `${player[attribute.key]}%` }}
              />
            </span>
            <dd className="w-6 shrink-0 text-right text-xs font-semibold">
              {player[attribute.key]}
            </dd>
          </div>
        ))}
      </dl>

      <div className="mt-3 border-t border-line/60 pt-3">
        <ConditionRow value={player.condition} />
        <StatusRow player={player} />
      </div>
    </section>
  )
}

function ConditionRow({ value }: { value: number }) {
  const tone = value >= 80 ? 'bg-win' : value >= 55 ? 'bg-draw' : 'bg-loss'
  const text = value >= 80 ? 'text-win' : value >= 55 ? 'text-draw' : 'text-loss'

  return (
    <div className="flex items-center gap-2">
      <span className="w-16 shrink-0 text-[0.65rem] text-muted">Kondisiya</span>
      <span className="h-1.5 flex-1 overflow-hidden rounded-full bg-bg">
        <span className={`block h-full ${tone}`} style={{ width: `${value}%` }} />
      </span>
      <span className={`w-6 shrink-0 text-right text-xs font-semibold ${text}`}>{value}</span>
    </div>
  )
}

function StatusRow({ player }: { player: Player }) {
  if (player.matchesInjured > 0) {
    return (
      <p className="mt-2 text-xs font-medium text-loss">Zədəli · {player.matchesInjured} matç</p>
    )
  }

  if (player.matchesSuspended > 0) {
    return (
      <p className="mt-2 text-xs font-medium text-loss">Cəzalı · {player.matchesSuspended} matç</p>
    )
  }

  if (player.yellowCards > 0) {
    return (
      <p className="mt-2 flex items-center gap-1.5 text-xs text-draw">
        <span aria-hidden className="inline-block h-3 w-2 rounded-[1px] bg-draw" />
        {player.yellowCards} sarı vərəqə
      </p>
    )
  }

  return <p className="mt-2 text-xs text-muted">Oynamağa hazırdır</p>
}
