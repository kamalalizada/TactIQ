import { Link } from 'react-router-dom'

import { TeamCrest } from '@/components/ui/TeamCrest'
import { CupRound, CUP_ROUND_LABELS, type CupBracket, type CupTie } from '@/types'

// Groups have their own tables; the mini bracket shows the knockout only.
const ROUNDS: CupRound[] = [CupRound.QuarterFinal, CupRound.SemiFinal, CupRound.Final]

/** A compact read-only bracket; the full one lives on the cup page. */
export function KnockoutMini({
  bracket,
  seasonId,
  seasonFinished,
}: {
  bracket: CupBracket | undefined
  seasonId: string
  seasonFinished: boolean
}) {
  return (
    <section className="glass-card flex flex-1 flex-col">
      <header className="mb-3 flex items-baseline justify-between gap-2">
        <h3 className="text-sm font-semibold">Knockout Mərhələsi</h3>
        <Link to={`/seasons/${seasonId}/cup`} className="text-xs text-accent hover:underline">
          Açıq bax →
        </Link>
      </header>

      {!bracket?.started ? (
        <p className="py-6 text-center text-xs text-muted">
          {seasonFinished
            ? 'Kubok hazırdır — başlatmaq üçün kubok səhifəsinə keçin.'
            : 'Kubok mövsüm bitəndən sonra başlayır.'}
        </p>
      ) : (
        <>
          {bracket.finished && (
            <p className="mb-3 flex items-center justify-center gap-2 rounded border border-draw/40 bg-draw/10 py-1.5 text-xs">
              <span aria-hidden>🏆</span>
              <span className="font-bold text-draw">{bracket.winnerTeamName}</span>
            </p>
          )}

          <div className="grid grid-cols-3 gap-2">
            {ROUNDS.map((round) => {
              const ties = bracket.ties.filter((tie) => tie.round === round)

              return (
                <div key={round}>
                  <p className="mb-1.5 text-center text-[0.6rem] tracking-wide text-muted uppercase">
                    {CUP_ROUND_LABELS[round]}
                  </p>

                  {ties.length === 0 ? (
                    <div className="rounded border border-dashed border-line/60 py-4 text-center text-[0.6rem] text-muted/60">
                      —
                    </div>
                  ) : (
                    <div className="space-y-1.5">
                      {ties.map((tie) => (
                        <MiniTie key={tie.tieId} tie={tie} />
                      ))}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </>
      )}
    </section>
  )
}

function MiniTie({ tie }: { tie: CupTie }) {
  return (
    <div className="rounded border border-line/60 bg-bg/60 p-1.5">
      <MiniSide
        name={tie.homeTeamName}
        logoUrl={tie.homeTeamLogoUrl}
        score={tie.homeAggregate}
        isWinner={tie.winnerTeamId === tie.homeTeamId}
      />
      <MiniSide
        name={tie.awayTeamName}
        logoUrl={tie.awayTeamLogoUrl}
        score={tie.awayAggregate}
        isWinner={tie.winnerTeamId === tie.awayTeamId}
      />
      {tie.decidedOnPenalties && (
        <p className="mt-0.5 text-center text-[0.55rem] text-draw">penalti</p>
      )}
    </div>
  )
}

function MiniSide({
  name,
  logoUrl,
  score,
  isWinner,
}: {
  name: string
  logoUrl: string | null
  score: number
  isWinner: boolean
}) {
  return (
    <div className="flex items-center gap-1 py-0.5">
      <TeamCrest name={name} logoUrl={logoUrl} size="xs" />
      <span
        className={`flex-1 truncate text-[0.65rem] ${isWinner ? 'font-semibold' : 'text-muted'}`}
      >
        {name}
      </span>
      <span className={`shrink-0 text-[0.65rem] font-bold ${isWinner ? 'text-win' : ''}`}>
        {score}
      </span>
    </div>
  )
}
