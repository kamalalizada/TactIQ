import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'

import { seasonApi } from '@/api/endpoints'
import { PlayerCard } from '@/components/PlayerCard'
import { Shell } from '@/components/Shell'
import { EmptyNote, ErrorNote, Spinner } from '@/components/States'
import { Badge } from '@/components/ui/Badge'
import { RatingBar } from '@/components/ui/ProgressBar'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { Position, type Player } from '@/types'

// GK first, strikers last, the way a team sheet reads.
const POSITION_ORDER: Position[] = [Position.GK, Position.DEF, Position.MID, Position.FWD]

function byPosition(a: Player, b: Player) {
  const byLine = POSITION_ORDER.indexOf(a.position) - POSITION_ORDER.indexOf(b.position)
  return byLine !== 0 ? byLine : a.shirtNumber - b.shirtNumber
}

export function TeamDetailPage() {
  const { seasonId = '', teamId = '' } = useParams()

  const league = useQuery({
    queryKey: ['league', seasonId],
    queryFn: () => seasonApi.get(seasonId),
  })

  // There is no per-team endpoint; the squad comes from the league's team list.
  const teams = useQuery({
    queryKey: ['teams', seasonId],
    queryFn: () => seasonApi.teams(seasonId),
  })

  if (teams.isPending) {
    return (
      <Shell>
        <Spinner />
      </Shell>
    )
  }

  if (teams.isError) {
    return (
      <Shell>
        <ErrorNote error={teams.error} />
      </Shell>
    )
  }

  const team = teams.data?.find((candidate) => candidate.id === teamId)

  if (!team) {
    return (
      <Shell>
        <EmptyNote>Team not found.</EmptyNote>
      </Shell>
    )
  }

  const isUserTeam = team.id === league.data?.userTeamId
  const starters = team.players.filter((player) => player.isStarter).sort(byPosition)
  const bench = team.players.filter((player) => !player.isStarter).sort(byPosition)

  return (
    <Shell>
      <div className="mb-6">
        <Link to={`/seasons/${seasonId}/teams`} className="text-xs text-muted hover:text-accent">
          ← Teams
        </Link>
      </div>

      <div
        className={`mb-6 flex flex-wrap items-center justify-between gap-5 rounded-lg border bg-panel p-5 ${
          isUserTeam ? 'user-row border-accent/40' : 'border-line'
        }`}
      >
        <div className="flex items-center gap-4">
          <TeamCrest name={team.name} logoUrl={team.logoUrl} size="lg" />
          <div>
            <h2 className="text-xl font-bold">{team.name}</h2>
            {isUserTeam && (
              <div className="mt-1">
                <Badge tone="accent">Your team</Badge>
              </div>
            )}
          </div>
        </div>

        <div className="w-full max-w-64 space-y-1.5">
          <RatingBar label="ATK" value={team.attackRating} />
          <RatingBar label="DEF" value={team.defenseRating} />
          <RatingBar label="OVR" value={team.teamOverall} />
        </div>
      </div>

      <Section title="Starting XI" players={starters} />
      <Section title="Bench" players={bench} />
    </Shell>
  )
}

function Section({ title, players }: { title: string; players: Player[] }) {
  if (players.length === 0) return null

  return (
    <section className="mb-8">
      <h3 className="mb-3 text-sm font-semibold tracking-wide text-muted uppercase">
        {title} <span className="text-muted/60">({players.length})</span>
      </h3>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {players.map((player) => (
          <PlayerCard key={player.id} player={player} />
        ))}
      </div>
    </section>
  )
}
