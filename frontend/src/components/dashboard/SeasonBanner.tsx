import { Link } from 'react-router-dom'

import type { StandingsRow } from '@/types'

interface SeasonBannerProps {
  champion: StandingsRow | undefined
  cupStarted: boolean
  seasonId: string
  /** True once every competition is settled and the year can be closed. */
  canAdvance?: boolean
  onNextSeason?: () => void
  advancing?: boolean
}

/**
 * The end of a season.
 *
 * Two things are left to do when the table is settled: the cup, if it has not
 * been played, and the year itself. Both belong here, because this is where a
 * manager looks the week his league finishes.
 */
export function SeasonBanner({
  champion,
  cupStarted,
  seasonId,
  canAdvance,
  onNextSeason,
  advancing,
}: SeasonBannerProps) {
  return (
    <div className="mb-5 flex flex-wrap items-center justify-between gap-4 rounded-lg border border-win/30 bg-win/10 p-4">
      <div>
        <p className="font-bold text-win">Mövsüm bitdi 🏆</p>
        <p className="mt-0.5 text-sm">
          Çempion: <span className="font-semibold">{champion?.teamName ?? '—'}</span>
          {champion && <span className="text-muted"> · {champion.points} xal</span>}
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        {!cupStarted && (
          <Link to={`/seasons/${seasonId}/cup`} className="btn-primary">
            Kuboku başlat
          </Link>
        )}

        {canAdvance && onNextSeason && (
          <button
            type="button"
            onClick={onNextSeason}
            disabled={advancing}
            className="rounded-lg bg-violet px-5 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-violet/85 disabled:opacity-40"
          >
            {advancing ? 'Keçirilir…' : 'Növbəti mövsüm'}
          </button>
        )}
      </div>
    </div>
  )
}
