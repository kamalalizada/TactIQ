import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'

import { seasonApi } from '@/api/endpoints'
import { Shell } from '@/components/Shell'
import { EmptyNote, ErrorNote, Spinner } from '@/components/States'
import { RatingBar } from '@/components/ui/ProgressBar'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { CompetitionKind, type Team } from '@/types'

/**
 * Choosing the club to manage. The backend assigns one at random when the
 * season is created, so this screen exists to change it before kick-off.
 */
export function PickTeamPage() {
  const { seasonId = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [country, setCountry] = useState<string | null>(null)

  const season = useQuery({
    queryKey: ['season', seasonId],
    queryFn: () => seasonApi.get(seasonId),
  })

  const teams = useQuery({
    queryKey: ['teams', seasonId],
    queryFn: () => seasonApi.teams(seasonId),
  })

  const choose = useMutation({
    mutationFn: (teamId: string) => seasonApi.setUserTeam(seasonId, teamId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['season', seasonId] })
      queryClient.invalidateQueries({ queryKey: ['seasons'] })
      navigate(`/seasons/${seasonId}`)
    },
  })

  if (season.isPending || teams.isPending) {
    return (
      <Shell>
        <Spinner />
      </Shell>
    )
  }

  if (season.isError) {
    return (
      <Shell>
        <ErrorNote error={season.error} />
      </Shell>
    )
  }

  const leagues =
    season.data?.competitions.filter(
      (competition) => competition.kind === CompetitionKind.League,
    ) ?? []

  const countries = leagues
    .map((competition) => competition.country)
    .filter((name): name is string => Boolean(name))

  const shown = (teams.data ?? [])
    .filter((team) => !country || team.country === country)
    .sort((a, b) => b.teamOverall - a.teamOverall)

  return (
    <Shell>
      <header className="mb-5">
        <h2 className="text-xl font-bold tracking-tight">Komandanı seç</h2>
        <p className="mt-0.5 text-sm text-muted">
          {season.data?.name} · İdarə etmək istədiyiniz klubu seçin
          {season.data?.userTeamName && (
            <>
              <span className="mx-1.5">·</span>
              <span className="text-accent">hazırda: {season.data.userTeamName}</span>
            </>
          )}
        </p>
      </header>

      <div className="mb-4 flex flex-wrap gap-1.5">
        <FilterChip active={country === null} onClick={() => setCountry(null)} label="Hamısı" />
        {countries.map((name) => (
          <FilterChip
            key={name}
            active={country === name}
            onClick={() => setCountry(name)}
            label={name}
          />
        ))}
      </div>

      {choose.isError && (
        <div className="mb-4">
          <ErrorNote error={choose.error} />
        </div>
      )}

      {shown.length === 0 ? (
        <EmptyNote>Komanda tapılmadı.</EmptyNote>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {shown.map((team) => (
            <TeamOption
              key={team.id}
              team={team}
              isCurrent={team.id === season.data?.userTeamId}
              pending={choose.isPending}
              onPick={() => choose.mutate(team.id)}
            />
          ))}
        </div>
      )}
    </Shell>
  )
}

function FilterChip({
  active,
  onClick,
  label,
}: {
  active: boolean
  onClick: () => void
  label: string
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`rounded-full border px-3 py-1.5 text-xs font-medium transition-colors ${
        active
          ? 'border-violet bg-violet/15 text-ink'
          : 'border-line text-muted hover:bg-white/5 hover:text-ink'
      }`}
    >
      {label}
    </button>
  )
}

function TeamOption({
  team,
  isCurrent,
  pending,
  onPick,
}: {
  team: Team
  isCurrent: boolean
  pending: boolean
  onPick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onPick}
      disabled={pending}
      className={`rounded-lg border bg-panel p-4 text-left transition-colors disabled:opacity-50 ${
        isCurrent ? 'border-violet bg-violet/5' : 'border-line hover:border-violet/60'
      }`}
    >
      <div className="mb-3 flex items-center gap-3">
        <TeamCrest name={team.name} logoUrl={team.logoUrl} size="md" />
        <div className="min-w-0">
          <p className="truncate font-semibold">{team.name}</p>
          <p className="text-xs text-muted">{team.country}</p>
        </div>
        {isCurrent && (
          <span className="ml-auto shrink-0 text-[0.6rem] tracking-wide text-violet uppercase">
            seçili
          </span>
        )}
      </div>

      <div className="space-y-1.5">
        <RatingBar label="HÜC" value={team.attackRating} />
        <RatingBar label="MÜD" value={team.defenseRating} />
        <RatingBar label="ÜMU" value={team.teamOverall} />
      </div>
    </button>
  )
}
