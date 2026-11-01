import { Link } from 'react-router-dom'

import { EmptyNote } from '@/components/States'
import { PositionBadge } from '@/components/ui/Badge'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { Position, ROLE_LABELS, type Player, type Team } from '@/types'

const POSITION_ORDER: Position[] = [Position.GK, Position.DEF, Position.MID, Position.FWD]

function byPosition(a: Player, b: Player) {
  const line = POSITION_ORDER.indexOf(a.position) - POSITION_ORDER.indexOf(b.position)
  return line !== 0 ? line : a.shirtNumber - b.shirtNumber
}

/**
 * The manager's own club: squad list with fitness and availability at a glance.
 */
export function ClubHub({
  team,
  seasonId,
}: {
  team: Team | undefined
  seasonId: string
}) {
  if (!team) {
    return (
      <section className="glass-card">
        <h3 className="mb-2 text-sm font-semibold">Club Hub</h3>
        <EmptyNote>God Mode-da idarə etdiyiniz komanda yoxdur.</EmptyNote>
      </section>
    )
  }

  const squad = [...team.players].sort(byPosition)

  return (
    <section className="glass-card flex min-h-0 flex-col">
      <header className="mb-3 flex items-center gap-2">
        <TeamCrest name={team.name} logoUrl={team.logoUrl} size="sm" />
        <h3 className="truncate text-sm font-semibold">{team.name} Club Hub</h3>
      </header>

      <div className="mb-2 flex items-baseline justify-between">
        <h4 className="text-sm font-semibold">Heyət Siyahısı</h4>
        <Link to={`/seasons/${seasonId}/squad`} className="text-xs text-accent hover:underline">
          Hamısı →
        </Link>
      </div>

      <div className="scroll-list max-h-[24rem] min-h-0 flex-1 pr-1">
        <table className="w-full text-sm">
          <thead className="sticky top-0 bg-panel text-[0.65rem] text-muted uppercase">
            <tr>
              <th className="py-1.5 pr-2 text-left font-medium">Player</th>
              <th className="px-1 py-1.5 text-center font-medium">Ban</th>
              <th className="px-1 py-1.5 text-center font-medium">Inj</th>
              <th className="px-2 py-1.5 text-right font-medium">OVR</th>
              <th className="px-2 py-1.5 text-left font-medium">Condition</th>
            </tr>
          </thead>
          <tbody>
            {squad.map((player) => (
              <SquadRow key={player.id} player={player} />
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function SquadRow({ player }: { player: Player }) {
  return (
    <tr className={`border-t border-line/40 ${player.isAvailable ? '' : 'opacity-50'}`}>
      <td className="py-1.5 pr-2">
        <span className="flex items-center gap-1.5">
          <span className="w-5 shrink-0 text-[0.65rem] text-muted">{player.shirtNumber}</span>
          <PositionBadge position={player.position} />
          {/* The table is tight, so the exact role rides on the name's tooltip. */}
          <span className="truncate text-xs" title={`${player.name} · ${ROLE_LABELS[player.role]}`}>
            {player.name}
          </span>
        </span>
      </td>

      <td className="px-1 text-center">
        {player.matchesSuspended > 0 ? (
          <span title={`${player.matchesSuspended} matç cəza`} className="text-loss">
            ▮
          </span>
        ) : player.yellowCards > 0 ? (
          <span title={`${player.yellowCards} sarı vərəqə`} className="text-draw">
            ▮
          </span>
        ) : (
          <span className="text-muted/30">–</span>
        )}
      </td>

      <td className="px-1 text-center">
        {player.matchesInjured > 0 ? (
          <span title={`${player.matchesInjured} matç zədəli`} className="text-loss">
            ✚
          </span>
        ) : (
          <span className="text-muted/30">–</span>
        )}
      </td>

      <td className="px-2 text-right text-xs font-bold">{player.overall}</td>

      <td className="px-2">
        <ConditionBar value={player.condition} />
      </td>
    </tr>
  )
}

function ConditionBar({ value }: { value: number }) {
  const tone = value >= 80 ? 'bg-win' : value >= 55 ? 'bg-draw' : 'bg-loss'

  return (
    <span className="flex items-center gap-1.5" title={`${value}%`}>
      <span className="h-1.5 w-14 overflow-hidden rounded-full bg-bg">
        <span className={`block h-full ${tone}`} style={{ width: `${value}%` }} />
      </span>
      <span className="w-6 text-[0.65rem] text-muted">{value}</span>
    </span>
  )
}
