/**
 * Transfer fees as football prints them: a figure in millions, with enough
 * precision to tell two bids apart and no more. "€27.7M" rather than
 * "€27,700,000", which nobody reads and nobody says out loud.
 */
export function money(amount: number): string {
  if (amount >= 1_000_000) {
    const millions = amount / 1_000_000
    // Round numbers lose the decimal: €50M, not €50.0M.
    return `€${millions % 1 === 0 ? millions : millions.toFixed(1)}M`
  }

  if (amount >= 1_000) return `€${Math.round(amount / 1_000)}K`

  return `€${Math.round(amount)}`
}

/**
 * A figure typed into a bid box, read back as a number. Managers type
 * "12.5M", "12,500,000" or "12500000" and mean the same thing.
 */
export function parseMoney(input: string): number | null {
  const text = input.trim().replace(/[\s,€]/g, '').toUpperCase()
  if (!text) return null

  const match = /^(\d+(?:\.\d+)?)([MK])?$/.exec(text)
  if (!match) return null

  const value = Number(match[1])
  if (!Number.isFinite(value)) return null

  const scale = match[2] === 'M' ? 1_000_000 : match[2] === 'K' ? 1_000 : 1

  return Math.round(value * scale)
}
