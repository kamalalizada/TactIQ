import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'

import { seasonApi } from '@/api/endpoints'
import { Shell } from '@/components/Shell'
import { ErrorNote, Spinner } from '@/components/States'
import { Badge } from '@/components/ui/Badge'
import { RatingBar } from '@/components/ui/ProgressBar'
import { TeamCrest } from '@/components/ui/TeamCrest'
import type { Team } from '@/types'

export function TeamsPage() {
  const { seasonId = '' } = useParams()

  const league = useQuery({
    queryKey: ['league', seasonId],
    queryFn: () => seasonApi.get(seasonId),
  })

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

  const userTeamId = league.data?.userTeamId ?? null

  return (
    <Shell>
      <div className="mb-6">
        <Link to={`/seasons/${seasonId}`} className="text-xs text-muted hover:text-accent">
          ← Dashboard
        </Link>
        <h2 className="mt-1 text-2xl font-bold tracking-tight">All Teams</h2>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {teams.data?.map((team) => (
          <TeamCard
            key={team.id}
            team={team}
            seasonId={seasonId}
            isUserTeam={team.id === userTeamId}
          />
        ))}
      </div>
    </Shell>
  )
}

function TeamCard({
  team,
  seasonId,
  isUserTeam,
}: {
  team: Team
  seasonId: string
  isUserTeam: boolean
}) {
  return (
    <Link
      to={`/seasons/${seasonId}/teams/${team.id}`}
      className={`block rounded-lg border bg-panel p-4 transition-colors hover:bg-white/5 ${
        isUserTeam ? 'user-row border-accent/40' : 'border-line'
      }`}
    >
      <div className="mb-3 flex items-center gap-3">
        <TeamCrest name={team.name} logoUrl={team.logoUrl} size="md" />
        <div className="min-w-0">
          <p className="truncate font-semibold">{team.name}</p>
          {isUserTeam && <Badge tone="accent">Yours</Badge>}
        </div>
      </div>

      <div className="space-y-1.5">
        <RatingBar label="ATK" value={team.attackRating} />
        <RatingBar label="DEF" value={team.defenseRating} />
        <RatingBar label="OVR" value={team.teamOverall} />
      </div>
    </Link>
  )
}
