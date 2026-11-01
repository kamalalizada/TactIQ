import { PositionBadge } from '@/components/ui/Badge'
import { ProgressBar } from '@/components/ui/ProgressBar'
import type { Player } from '@/types'

const ATTRIBUTES = [
  { key: 'pace', label: 'PAC' },
  { key: 'shooting', label: 'SHO' },
  { key: 'passing', label: 'PAS' },
  { key: 'dribbling', label: 'DRI' },
  { key: 'defending', label: 'DEF' },
  { key: 'physical', label: 'PHY' },
] as const

/** Colours the overall rating the way football games do: elite down to squad filler. */
function overallTone(overall: number) {
  if (overall >= 85) return 'text-win'
  if (overall >= 78) return 'text-accent'
  if (overall >= 70) return 'text-ink'
  return 'text-muted'
}

export function PlayerCard({ player }: { player: Player }) {
  return (
    <div className="rounded-lg border border-line bg-panel p-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold">{player.name}</p>
          <div className="mt-1 flex items-center gap-2">
            <PositionBadge position={player.position} />
            <span className="text-xs text-muted">#{player.shirtNumber}</span>
          </div>
        </div>

        <div className="shrink-0 text-right">
          <div className={`text-2xl leading-none font-black ${overallTone(player.overall)}`}>
            {player.overall}
          </div>
          <div className="text-[0.6rem] tracking-wide text-muted uppercase">OVR</div>
        </div>
      </div>

      <dl className="mt-3 space-y-1">
        {ATTRIBUTES.map((attribute) => (
          <div key={attribute.key} className="flex items-center gap-2">
            <dt className="w-8 shrink-0 text-[0.65rem] text-muted">{attribute.label}</dt>
            <ProgressBar
              value={player[attribute.key]}
              max={99}
              tone="muted"
              className="h-1 flex-1"
            />
            <dd className="w-6 shrink-0 text-right text-[0.7rem] font-semibold">
              {player[attribute.key]}
            </dd>
          </div>
        ))}
      </dl>
    </div>
  )
}
