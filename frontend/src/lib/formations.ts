import { PlayerRole, Position, type Player, type PlayerRole as Role } from '@/types'

/**
 * What each shirt in a formation is asked to do, in the order a line-up is
 * read: the keeper first, then every line left to right. This mirrors the
 * backend's own table, so the side drawn on the pitch is the side the match
 * engine fields.
 *
 * A count of defenders cannot tell a 4-3-3 from a 4-4-2 — both field four at
 * the back. What separates them is the job each of those four does, and whether
 * the men ahead of them are wingers or wide midfielders. A player standing in a
 * slot meant for somebody else plays below their rating, which is what makes
 * the choice of shape matter.
 */
export const FORMATION_SLOTS: Record<string, Role[]> = {
  '4-4-2': [
    PlayerRole.GK,
    PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
    PlayerRole.LM, PlayerRole.CM, PlayerRole.CM, PlayerRole.RM,
    PlayerRole.ST, PlayerRole.ST,
  ],
  '4-3-3': [
    PlayerRole.GK,
    PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
    PlayerRole.CM, PlayerRole.CDM, PlayerRole.CM,
    PlayerRole.LW, PlayerRole.ST, PlayerRole.RW,
  ],
  '4-2-3-1': [
    PlayerRole.GK,
    PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
    PlayerRole.CDM, PlayerRole.CDM,
    PlayerRole.LM, PlayerRole.CAM, PlayerRole.RM,
    PlayerRole.ST,
  ],
  '3-5-2': [
    PlayerRole.GK,
    PlayerRole.CB, PlayerRole.CB, PlayerRole.CB,
    PlayerRole.LWB, PlayerRole.CM, PlayerRole.CDM, PlayerRole.CM, PlayerRole.RWB,
    PlayerRole.ST, PlayerRole.ST,
  ],
  '3-4-2-1': [
    PlayerRole.GK,
    PlayerRole.CB, PlayerRole.CB, PlayerRole.CB,
    PlayerRole.LWB, PlayerRole.CM, PlayerRole.CM, PlayerRole.RWB,
    PlayerRole.CAM, PlayerRole.CAM,
    PlayerRole.ST,
  ],
  '5-3-2': [
    PlayerRole.GK,
    PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
    PlayerRole.CM, PlayerRole.CDM, PlayerRole.CM,
    PlayerRole.ST, PlayerRole.ST,
  ],
  '4-5-1': [
    PlayerRole.GK,
    PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
    PlayerRole.LM, PlayerRole.CM, PlayerRole.CDM, PlayerRole.CM, PlayerRole.RM,
    PlayerRole.ST,
  ],
  '3-4-3': [
    PlayerRole.GK,
    PlayerRole.CB, PlayerRole.CB, PlayerRole.CB,
    PlayerRole.LM, PlayerRole.CM, PlayerRole.CM, PlayerRole.RM,
    PlayerRole.LW, PlayerRole.ST, PlayerRole.RW,
  ],
}

/** The line of the pitch a role belongs to. */
export function lineOf(role: Role): Position {
  if (role === PlayerRole.GK) return Position.GK
  if (
    role === PlayerRole.LB ||
    role === PlayerRole.CB ||
    role === PlayerRole.RB ||
    role === PlayerRole.LWB ||
    role === PlayerRole.RWB
  ) {
    return Position.DEF
  }
  if (role === PlayerRole.LW || role === PlayerRole.ST || role === PlayerRole.RW) {
    return Position.FWD
  }
  return Position.MID
}

/**
 * Roles that read as the same job, shifted across or along, and so cost a
 * player almost nothing. Mirrors the backend's own neighbour table.
 */
const NEIGHBOURS: Record<Role, Role[]> = {
  [PlayerRole.GK]: [],
  [PlayerRole.LB]: [PlayerRole.RB, PlayerRole.LM, PlayerRole.LWB],
  [PlayerRole.CB]: [PlayerRole.CDM],
  [PlayerRole.RB]: [PlayerRole.LB, PlayerRole.RM, PlayerRole.RWB],
  [PlayerRole.CDM]: [PlayerRole.CM, PlayerRole.CB],
  [PlayerRole.CM]: [PlayerRole.CDM, PlayerRole.CAM],
  [PlayerRole.CAM]: [PlayerRole.CM, PlayerRole.ST],
  [PlayerRole.LM]: [PlayerRole.LW, PlayerRole.LB],
  [PlayerRole.RM]: [PlayerRole.RW, PlayerRole.RB],
  [PlayerRole.LW]: [PlayerRole.LM, PlayerRole.RW],
  [PlayerRole.RW]: [PlayerRole.RM, PlayerRole.LW],
  [PlayerRole.ST]: [PlayerRole.CAM],
  // A wing-back is his flank's full-back asked to get forward, and its wide
  // midfielder asked to defend.
  [PlayerRole.LWB]: [PlayerRole.LB, PlayerRole.LM],
  [PlayerRole.RWB]: [PlayerRole.RB, PlayerRole.RM],
}

/** Which flank a role works, or null for anyone central. */
function flankOf(role: Role): 'left' | 'right' | null {
  if (role === PlayerRole.LW || role === PlayerRole.LM || role === PlayerRole.LWB) return 'left'
  if (role === PlayerRole.RW || role === PlayerRole.RM || role === PlayerRole.RWB) return 'right'
  return null
}

/**
 * Whether a role is a wide attacking job. Winger and wide midfielder are
 * interchangeable on a flank; a wing-back, who holds the same flank from the
 * back line, is not one of them.
 */
function isWideAttacking(role: Role): boolean {
  return (
    role === PlayerRole.LW ||
    role === PlayerRole.RW ||
    role === PlayerRole.LM ||
    role === PlayerRole.RM
  )
}

/** How well a player copes in a role that is not their own, as the engine rates it. */
export function roleFitness(natural: Role, playedAs: Role): number {
  if (natural === playedAs) return 1

  // Winger and wide midfielder are one job on a given flank; the other flank
  // costs a tenth. A wing-back shares the flank but defends first, so he falls
  // through to the neighbour and line rules rather than reading as at home at
  // winger.
  const naturalFlank = flankOf(natural)
  const playedFlank = flankOf(playedAs)

  if (isWideAttacking(natural) && isWideAttacking(playedAs)) {
    return naturalFlank === playedFlank ? 1 : 0.9
  }

  // Either way, the wrong flank is the wrong foot.
  if (naturalFlank && playedFlank && naturalFlank !== playedFlank) return 0.9

  if (natural === PlayerRole.GK || playedAs === PlayerRole.GK) return 0.45
  if (NEIGHBOURS[natural].includes(playedAs)) return 0.94

  const naturalLine = lineOf(natural)
  const playedLine = lineOf(playedAs)

  if (naturalLine === playedLine) return 0.87

  return Math.abs(naturalLine - playedLine) === 1 ? 0.78 : 0.62
}

/** How a player's suitability for a slot should read to the manager. */
export type Fit = 'native' | 'close' | 'poor'

export function fitOf(natural: Role, playedAs: Role): Fit {
  const fitness = roleFitness(natural, playedAs)
  if (fitness === 1) return 'native'
  return fitness >= 0.9 ? 'close' : 'poor'
}

/** A player's rating in the role they are filling, as the engine will play them. */
export function ratingAs(player: Player, playedAs: Role): number {
  return Math.max(1, roundHalfEven(player.overall * roleFitness(player.role, playedAs)))
}

/** Rounds a .5 to the even neighbour, as the backend's Math.Round does, so both agree on 76.5. */
function roundHalfEven(value: number): number {
  const floor = Math.floor(value)
  const fraction = value - floor
  if (Math.abs(fraction - 0.5) > 1e-9) return Math.round(value)
  return floor % 2 === 0 ? floor : floor + 1
}

/**
 * Picks an eleven for a shape by filling each slot with the best player for
 * that particular job. Whoever is already in the side keeps their place where
 * they suit the slot, so changing shape does not shuffle the whole team.
 */
export function pickForFormation(
  formation: string,
  players: Player[],
  alreadyIn: string[] = [],
): string[] {
  const slots = FORMATION_SLOTS[formation] ?? FORMATION_SLOTS['4-4-2']
  const used = new Set<string>()
  const picked: string[] = []

  for (const slot of slots) {
    const line = lineOf(slot)

    // A slot is filled by whoever suits it best and can play. Someone from
    // another line is a poor fit rather than no fit, so a club short of
    // midfielders still takes the field with eleven men: leaving the slot empty
    // would put out a side of eight, which is worse than a defender in midfield.
    const best = players
      .filter((player) => !used.has(player.id) && player.isAvailable)
      .sort((a, b) => {
        // The right line first, then suitability within it, then the men
        // already picked, and quality last.
        const byLine = Number(b.position === line) - Number(a.position === line)
        if (byLine !== 0) return byLine

        const byFit = roleFitness(b.role, slot) - roleFitness(a.role, slot)
        if (byFit !== 0) return byFit

        const byPlace = Number(alreadyIn.includes(b.id)) - Number(alreadyIn.includes(a.id))
        return byPlace !== 0 ? byPlace : b.overall - a.overall
      })[0]

    if (best) {
      used.add(best.id)
      picked.push(best.id)
    }
  }

  return picked
}
