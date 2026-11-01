import { FORMATION_SLOTS, lineOf } from '@/lib/formations'
import { PlayerRole, Position, type PlayerRole as Role } from '@/types'

/**
 * A spot on the pitch as percentages, the side attacking upwards: `x` runs
 * from the left touchline (0) to the right (100), `y` from the side's own goal
 * line (0) to the opponent's (100). It is also where a shirt is drawn, so the
 * centre of a shirt sits exactly on its spot.
 */
export interface Spot {
  x: number
  y: number
}

/** The id the pitch itself carries as a drop target, for a shirt let go on open grass. */
export const PITCH_DROP_ID = 'pitch'

/**
 * The job a player standing on a spot fills. Mirrors the backend's PitchZones
 * line for line, so the role drawn here is the one the match engine plays.
 */
export function roleAt({ x, y }: Spot): Role {
  if (y < 13 && x >= 25 && x <= 75) return PlayerRole.GK

  if (y < 35) {
    if (x >= 28 && x <= 72) return PlayerRole.CB

    // Wide and deep is a full-back; wide and pushed on is a wing-back, which
    // is the same player given the whole flank.
    const left = x < 28
    if (y < 30) return left ? PlayerRole.LB : PlayerRole.RB
    return left ? PlayerRole.LWB : PlayerRole.RWB
  }

  if (y < 68) {
    if (x < 25) return PlayerRole.LM
    if (x > 75) return PlayerRole.RM
    if (y < 46) return PlayerRole.CDM
    if (y < 58) return PlayerRole.CM
    return PlayerRole.CAM
  }

  if (x < 30) return PlayerRole.LW
  if (x > 70) return PlayerRole.RW
  return PlayerRole.ST
}

/** Keeps a dropped shirt wholly on the grass. */
export function clampSpot({ x, y }: Spot): Spot {
  return {
    x: Math.min(95, Math.max(5, x)),
    y: Math.min(96, Math.max(4, y)),
  }
}

/** Roles that hold one flank, so a side has room for one of each. */
const ONE_PER_SIDE: Role[] = [
  PlayerRole.LB,
  PlayerRole.RB,
  PlayerRole.LWB,
  PlayerRole.RWB,
  PlayerRole.LW,
  PlayerRole.RW,
]

const ROLE_NAMES: Partial<Record<Role, string>> = {
  [PlayerRole.LB]: 'LB',
  [PlayerRole.RB]: 'RB',
  [PlayerRole.LWB]: 'LWB',
  [PlayerRole.RWB]: 'RWB',
  [PlayerRole.LW]: 'LW',
  [PlayerRole.RW]: 'RW',
}

/**
 * Why these roles cannot take the field together, or null if they can. The
 * same rules the backend enforces when the line-up is saved: one keeper, a back
 * line of at least three, and one of each flank role.
 */
export function lineupProblem(roles: Role[]): string | null {
  const keepers = roles.filter((role) => role === PlayerRole.GK).length
  if (keepers === 0) return 'Qapıda heç kim yoxdur.'
  if (keepers > 1) return 'Qapıda yalnız bir oyunçu dayana bilər.'

  const defenders = roles.filter((role) => lineOf(role) === Position.DEF).length
  if (defenders < 3) return 'Müdafiədə ən azı 3 oyunçu olmalıdır.'

  for (const role of ONE_PER_SIDE) {
    if (roles.filter((r) => r === role).length > 1) {
      return `Yalnız bir ${ROLE_NAMES[role]} ola bilər.`
    }
  }

  return null
}

/**
 * Where each role stands in a preset shape. Both numbers come from the job a
 * shirt does, which is what separates a 4-2-3-1 from a 4-5-1: both field five
 * across the middle, but one holds two of them deep and pushes a playmaker on.
 * Every spot here reads back as its own role through `roleAt`.
 */
const PRESET_SPOTS: Record<Role, Spot> = {
  [PlayerRole.GK]: { x: 50, y: 8 },

  [PlayerRole.LB]: { x: 12, y: 28 },
  [PlayerRole.CB]: { x: 50, y: 24 },
  [PlayerRole.RB]: { x: 88, y: 28 },

  // A wing-back holds the flank from higher up than a full-back, which is how
  // `roleAt` tells the two apart.
  [PlayerRole.LWB]: { x: 11, y: 33 },
  [PlayerRole.RWB]: { x: 89, y: 33 },

  [PlayerRole.CDM]: { x: 50, y: 42 },
  [PlayerRole.LM]: { x: 15, y: 52 },
  [PlayerRole.CM]: { x: 50, y: 52 },
  [PlayerRole.RM]: { x: 85, y: 52 },
  [PlayerRole.CAM]: { x: 50, y: 64 },

  [PlayerRole.LW]: { x: 18, y: 78 },
  [PlayerRole.ST]: { x: 50, y: 84 },
  [PlayerRole.RW]: { x: 82, y: 78 },
}

/** How wide a group of players sharing a preset spot is fanned out. */
const FAN = 19

/**
 * Lays a preset shape out: the n-th id takes the n-th role of the formation.
 * Anyone sharing a spot — the two centre-backs of a back four — is fanned out
 * either side of it, so they stand alongside each other rather than stacked.
 */
export function presetSpots(formation: string, ids: string[]): Record<string, Spot> {
  const slots = FORMATION_SLOTS[formation] ?? FORMATION_SLOTS['4-4-2']
  const groups = new Map<Role, string[]>()

  ids.forEach((id, index) => {
    const role = slots[index] ?? PlayerRole.CM
    groups.set(role, [...(groups.get(role) ?? []), id])
  })

  const spots: Record<string, Spot> = {}

  for (const [role, group] of groups) {
    const centre = PRESET_SPOTS[role]
    const spread = Math.min(FAN * (group.length - 1), 58)
    const start = centre.x - spread / 2

    group.forEach((id, index) => {
      const x = group.length === 1 ? centre.x : start + (spread / (group.length - 1)) * index
      spots[id] = { x: Math.min(92, Math.max(8, x)), y: centre.y }
    })
  }

  return spots
}

/**
 * The name to print for a side. A side that still matches a preset shape role
 * for role is called by it; one arranged by hand is counted line by line.
 */
export function shapeLabel(roles: Role[]): string {
  const sorted = [...roles].sort((a, b) => a - b).join(',')

  for (const [name, slots] of Object.entries(FORMATION_SLOTS)) {
    if ([...slots].sort((a, b) => a - b).join(',') === sorted) return name
  }

  const count = (line: Position) => roles.filter((role) => lineOf(role) === line).length
  return `${count(Position.DEF)}-${count(Position.MID)}-${count(Position.FWD)}`
}
