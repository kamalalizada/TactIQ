import { TeamCrest } from '@/components/ui/TeamCrest'
import type { StandingsRow } from '@/types'

/** Colour band down the left edge of the table, the way league tables mark qualification. */
function zoneFor(rank: number, teamCount: number) {
  if (rank <= 2) return { bar: 'bg-win', label: 'champions' }
  if (rank <= 4) return { bar: 'bg-accent', label: 'europa' }
  if (rank > teamCount - 2) return { bar: 'bg-loss', label: 'relegation' }
  return { bar: 'bg-transparent', label: null }
}

export function StandingsTable({
  rows,
  userTeamId,
  /** Caps the height so a dashboard card keeps its shape; a full page sets it false. */
  scroll = true,
}: {
  rows: StandingsRow[]
  userTeamId?: string | null
  scroll?: boolean
}) {
  // A full-page table can afford taller rows and larger type than a card can.
  const rowHeight = scroll ? 'h-[38px]' : 'h-[52px]'
  const textSize = scroll ? 'text-sm' : 'text-base'
  const crestSize = scroll ? 'xs' : 'sm'

  return (
    <div>
      <div className={scroll ? 'scroll-list max-h-[26rem] overflow-x-auto' : 'overflow-x-auto'}>
        <table className={`w-full ${textSize}`}>
          <thead className="sticky top-0 z-10 border-b border-line bg-panel text-xs text-muted uppercase">
            <tr>
              <th className="py-2 pr-2 pl-4 text-left font-medium">#</th>
              <th className="px-2 py-2 text-left font-medium">Komanda</th>
              {['P', 'W', 'D', 'L', 'GF', 'GA', 'GD'].map((heading) => (
                <th key={heading} className="px-2 py-2 text-right font-medium">
                  {heading}
                </th>
              ))}
              <th className="py-2 pr-4 pl-2 text-right font-medium text-ink">PTS</th>
            </tr>
          </thead>

          <tbody>
            {rows.map((row) => {
              const isUser = row.teamId === userTeamId
              const zone = zoneFor(row.rank, rows.length)

              return (
                <tr
                  key={row.teamId}
                  className={`${rowHeight} border-b border-line/60 transition-colors last:border-0 ${
                    isUser ? 'bg-accent/10' : 'hover:bg-white/5'
                  }`}
                >
                  <td className="relative py-0 pr-2 pl-4 text-muted">
                    <span
                      aria-hidden
                      className={`absolute top-0 bottom-0 left-0 w-[3px] ${zone.bar}`}
                    />
                    {row.rank}
                  </td>
                  <td className={`px-2 font-medium ${isUser ? 'text-accent' : ''}`}>
                    <span className="flex items-center gap-2">
                      <TeamCrest name={row.teamName} logoUrl={row.logoUrl} size={crestSize} />
                      <span className="truncate">{row.teamName}</span>
                    </span>
                  </td>
                  <td className="px-2 text-right text-muted">{row.played}</td>
                  <td className="px-2 text-right">{row.won}</td>
                  <td className="px-2 text-right">{row.drawn}</td>
                  <td className="px-2 text-right">{row.lost}</td>
                  <td className="px-2 text-right text-muted">{row.goalsFor}</td>
                  <td className="px-2 text-right text-muted">{row.goalsAgainst}</td>
                  <td className="px-2 text-right text-muted">
                    {row.goalDifference > 0 ? `+${row.goalDifference}` : row.goalDifference}
                  </td>
                  <td className="py-0 pr-4 pl-2 text-right font-bold">{row.points}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <div className="mt-2.5 flex flex-wrap gap-4 text-[0.7rem] text-muted">
        <Key colour="bg-win" label="Çempionlar Liqası" />
        <Key colour="bg-accent" label="Avropa Liqası" />
        <Key colour="bg-loss" label="Düşmə Zonası" />
      </div>
    </div>
  )
}

function Key({ colour, label }: { colour: string; label: string }) {
  return (
    <span className="flex items-center gap-1.5">
      <span aria-hidden className={`inline-block h-2.5 w-[3px] rounded-sm ${colour}`} />
      {label}
    </span>
  )
}
