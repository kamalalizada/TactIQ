import { useDraggable, useDroppable } from '@dnd-kit/core'
import type { Ref } from 'react'

import { PITCH_TEXT_EDGE, RatingStar, RoleChip } from '@/components/ui/Badge'
import { fitOf, ratingAs } from '@/lib/formations'
import { PITCH_DROP_ID, roleAt, type Spot } from '@/lib/pitch'
import { PlayerRole, ROLE_LABELS, type Player } from '@/types'

/**
 * The starting eleven laid out on a pitch, the way a football game shows it.
 *
 * Each player stands on the spot they were placed on, and the spot decides the
 * job: a centre-back carried into the goalmouth is drawn — and rated — as the
 * keeper, and a central midfielder nudged back becomes a holding one.
 */
export function PitchView({
  players,
  spots,
  onSelect,
  selectedId,
  formation,
  draggable = false,
  showZones = false,
  pitchRef,
}: {
  players: Player[]
  /** Where each starter stands; anyone without a spot is not drawn. */
  spots: Record<string, Spot>
  onSelect?: (player: Player) => void
  selectedId?: string | null
  /** The shape's name, printed in the corner of the pitch. */
  formation?: string
  /**
   * Lets each shirt be picked up and dropped on another, on open grass, or on
   * the bench. The page owns what a drop means; this names each shirt
   * `starter:<id>` and the grass itself `pitch`. Needs a DndContext above it.
   */
  draggable?: boolean
  /** Draws where each job begins and ends, while a shirt is being moved. */
  showZones?: boolean
  /** The pitch's own element, so the page can turn a drop point into a spot. */
  pitchRef?: Ref<HTMLDivElement>
}) {
  const grass = useDroppable({ id: PITCH_DROP_ID, disabled: !draggable })

  return (
    <div
      ref={(node) => {
        grass.setNodeRef(node)
        if (typeof pitchRef === 'function') pitchRef(node)
        else if (pitchRef) pitchRef.current = node
      }}
      className="relative mx-auto aspect-[72/109] w-full max-w-[42rem] overflow-hidden rounded-xl shadow-xl"
      style={{ background: 'radial-gradient(ellipse at 50% 45%, #8fca55 0%, #74b443 55%, #5c9a33 100%)' }}
    >
      <PitchMarkings />
      {showZones && <ZoneGuides />}

      {players.map((player) => {
        const spot = spots[player.id]
        if (!spot) return null

        return (
          <PlayerToken
            key={player.id}
            player={player}
            role={roleAt(spot)}
            spot={spot}
            selected={player.id === selectedId}
            onSelect={onSelect}
            draggable={draggable}
          />
        )
      })}

      {formation && (
        <span
          className={`absolute right-3 bottom-2 font-pitch text-[2rem] leading-none font-bold text-white ${PITCH_TEXT_EDGE}`}
        >
          {formation}
        </span>
      )}
    </div>
  )
}

/**
 * Where each job begins and ends, drawn faintly over the grass. The lines and
 * names follow the same bands `roleAt` reads, so a shirt dropped inside a
 * named area takes that name.
 */
function ZoneGuides() {
  // Spots measure y up from the side's own goal line; the drawing measures down.
  const line = (x1: number, y1: number, x2: number, y2: number) => (
    <line
      x1={x1}
      y1={100 - y1}
      x2={x2}
      y2={100 - y2}
      vectorEffect="non-scaling-stroke"
    />
  )

  const labels: { role: PlayerRole; x: number; y: number }[] = [
    { role: PlayerRole.GK, x: 50, y: 6 },
    { role: PlayerRole.LB, x: 14, y: 24 },
    { role: PlayerRole.CB, x: 50, y: 24 },
    { role: PlayerRole.RB, x: 86, y: 24 },
    { role: PlayerRole.CDM, x: 50, y: 40.5 },
    { role: PlayerRole.LM, x: 12, y: 51.5 },
    { role: PlayerRole.CM, x: 50, y: 52 },
    { role: PlayerRole.RM, x: 88, y: 51.5 },
    { role: PlayerRole.CAM, x: 50, y: 63 },
    { role: PlayerRole.LW, x: 15, y: 84 },
    { role: PlayerRole.ST, x: 50, y: 84 },
    { role: PlayerRole.RW, x: 85, y: 84 },
  ]

  return (
    <>
      <svg
        aria-hidden
        viewBox="0 0 100 100"
        preserveAspectRatio="none"
        className="pointer-events-none absolute inset-0 size-full"
      >
        <g stroke="rgb(255 255 255 / 0.55)" strokeWidth="1.5" strokeDasharray="6 5" fill="none">
          {/* Goalmouth */}
          {line(25, 0, 25, 13)}
          {line(75, 0, 75, 13)}
          {line(25, 13, 75, 13)}
          {/* Back line: full-backs either side of the centre-backs */}
          {line(28, 13, 28, 35)}
          {line(72, 13, 72, 35)}
          {line(0, 35, 100, 35)}
          {/* Midfield: wide either side, holding / central / attacking down the middle */}
          {line(25, 35, 25, 68)}
          {line(75, 35, 75, 68)}
          {line(25, 46, 75, 46)}
          {line(25, 58, 75, 58)}
          {line(0, 68, 100, 68)}
          {/* Attack: wingers either side of the centre-forward */}
          {line(30, 68, 30, 100)}
          {line(70, 68, 70, 100)}
        </g>
      </svg>

      {labels.map(({ role, x, y }) => (
        <span
          key={role}
          aria-hidden
          className="pointer-events-none absolute -translate-x-1/2 translate-y-1/2 font-pitch text-sm font-bold tracking-wider text-white/60"
          style={{ left: `${x}%`, bottom: `${y}%` }}
        >
          {ROLE_LABELS[role]}
        </span>
      ))}
    </>
  )
}

function PitchMarkings() {
  // Drawn to a real pitch's proportions, 68 m by 105 m inside a 2 m margin, so
  // the centre circle stays round on the tall, narrow box that holds it.
  return (
    <svg
      aria-hidden
      viewBox="0 0 72 109"
      preserveAspectRatio="none"
      className="absolute inset-0 size-full text-white/85"
    >
      {/* Mown stripes */}
      {Array.from({ length: 24 }).map((_, index) => (
        <rect
          key={index}
          x={0}
          y={(index * 109) / 24}
          width={72}
          height={109 / 24}
          fill={index % 2 === 0 ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.04)'}
        />
      ))}

      <g stroke="currentColor" strokeWidth="2.5" fill="none" vectorEffect="non-scaling-stroke">
        <rect x="2" y="2" width="68" height="105" vectorEffect="non-scaling-stroke" />
        <line x1="2" y1="54.5" x2="70" y2="54.5" vectorEffect="non-scaling-stroke" />
        <circle cx="36" cy="54.5" r="9.15" vectorEffect="non-scaling-stroke" />
        <circle cx="36" cy="54.5" r="0.6" fill="currentColor" stroke="none" />

        {/* Top end: penalty area, goal area, spot and arc */}
        <rect x="15.84" y="2" width="40.32" height="16.5" vectorEffect="non-scaling-stroke" />
        <rect x="26.84" y="2" width="18.32" height="5.5" vectorEffect="non-scaling-stroke" />
        <circle cx="36" cy="13" r="0.5" fill="currentColor" stroke="none" />
        <path d="M 28.69 18.5 A 9.15 9.15 0 0 0 43.31 18.5" vectorEffect="non-scaling-stroke" />

        {/* Bottom end */}
        <rect x="15.84" y="90.5" width="40.32" height="16.5" vectorEffect="non-scaling-stroke" />
        <rect x="26.84" y="101.5" width="18.32" height="5.5" vectorEffect="non-scaling-stroke" />
        <circle cx="36" cy="96" r="0.5" fill="currentColor" stroke="none" />
        <path d="M 28.69 90.5 A 9.15 9.15 0 0 1 43.31 90.5" vectorEffect="non-scaling-stroke" />
      </g>
    </svg>
  )
}

/** The id a starter's shirt carries while it is dragged or dropped on. */
export const starterDragId = (playerId: string) => `starter:${playerId}`

/**
 * What a shirt shows, as a football game's team sheet prints it: a starred
 * rating whose star carries fitness, the job, and the surname in white. The
 * rating is the one the player brings to that job, so a centre-back in goal
 * reads far below his own. Shared by the shirt on the pitch and the copy that
 * follows the pointer.
 */
export function TokenFace({
  player,
  role,
  mark = null,
}: {
  player: Player
  role: PlayerRole
  /** An outline round the rating and job: solid for the picked player, dashed for a drop target. */
  mark?: 'selected' | 'target' | null
}) {
  const outline =
    mark === 'selected'
      ? 'bg-white/20 outline-2 outline-white outline-solid'
      : mark === 'target'
        ? 'bg-white/10 outline-2 outline-white outline-dashed'
        : 'group-hover:bg-white/10'

  return (
    <>
      <span className={`flex flex-col items-center rounded px-1 pt-0.5 pb-1 transition-colors ${outline}`}>
        <RatingStar overall={ratingAs(player, role)} condition={player.condition} large />
        <RoleChip role={role} large />
      </span>

      <span
        className={`max-w-[8rem] truncate font-pitch text-[1.2rem] leading-tight font-bold tracking-wide text-white uppercase ${PITCH_TEXT_EDGE}`}
      >
        {lastName(player.name)}
      </span>
    </>
  )
}

function PlayerToken({
  player,
  role,
  spot,
  selected,
  onSelect,
  draggable,
}: {
  player: Player
  role: PlayerRole
  spot: Spot
  selected: boolean
  onSelect?: (player: Player) => void
  draggable: boolean
}) {
  const id = starterDragId(player.id)
  const drag = useDraggable({ id, data: { player }, disabled: !draggable })
  const drop = useDroppable({ id, data: { player }, disabled: !draggable })

  // Out of position costs a player part of their rating; the hover text says so.
  const native = fitOf(player.role, role) === 'native'

  const mark = drop.isOver && !drag.isDragging ? 'target' : selected ? 'selected' : null

  // The shirt's centre sits on its spot.
  const style = { left: `${spot.x}%`, bottom: `${spot.y}%` }
  const className = `group absolute flex -translate-x-1/2 translate-y-1/2 flex-col items-center px-1 py-1 ${
    drag.isDragging ? 'opacity-30' : player.isAvailable ? '' : 'opacity-50'
  }`

  const title = native
    ? `${player.name} · ${ROLE_LABELS[role]} · ${player.overall} OVR · kondisiya ${player.condition}`
    : `${player.name} · əsl mövqeyi ${ROLE_LABELS[player.role]} (${player.overall}), burada ${ROLE_LABELS[role]} oynayır · ${ratingAs(player, role)} OVR`

  if (!onSelect) {
    return (
      <span className={className} style={style} title={title}>
        <TokenFace player={player} role={role} mark={mark} />
      </span>
    )
  }

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
      onClick={() => onSelect(player)}
      title={title}
      className={`${className} ${draggable ? 'cursor-grab active:cursor-grabbing' : 'cursor-pointer'}`}
      style={style}
    >
      <TokenFace player={player} role={role} mark={mark} />
    </button>
  )
}

/** What a team sheet prints: the surname, or the whole name if it is one word. */
function lastName(name: string) {
  const parts = name.trim().split(/\s+/)
  // "Vini Jr." is known by the whole thing, not by its suffix.
  if (parts.length < 2 || /^(jr|sr)\.?$/i.test(parts[parts.length - 1])) return name
  return parts.slice(1).join(' ')
}
