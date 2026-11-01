import type { Spot } from '@/lib/pitch'

/**
 * Edits to a starting eleven, held as the spot each starter stands on. The
 * spot is what gives a player their job, so moving someone is how their role
 * changes, and trading spots trades jobs.
 */
export type Spots = Record<string, Spot>

/** Two starters trade places, and with them the jobs those places carry. */
export function swapSpots(spots: Spots, a: string, b: string): Spots {
  if (a === b || !spots[a] || !spots[b]) return spots
  return { ...spots, [a]: spots[b], [b]: spots[a] }
}

/** A substitute takes over the exact spot, and job, of the player going off. */
export function substitute(spots: Spots, outgoing: string, incoming: string): Spots {
  if (!spots[outgoing] || spots[incoming]) return spots

  const next = { ...spots, [incoming]: spots[outgoing] }
  delete next[outgoing]
  return next
}

/** A starter moved to open grass. */
export function moveTo(spots: Spots, id: string, spot: Spot): Spots {
  if (!spots[id]) return spots
  return { ...spots, [id]: spot }
}

/**
 * The starters in the order a team sheet reads them — from the keeper up, and
 * left to right along each line — which is the order the backend numbers their
 * shirts in.
 */
export function inPitchOrder(spots: Spots): string[] {
  return Object.keys(spots).sort((a, b) => spots[a].y - spots[b].y || spots[a].x - spots[b].x)
}
