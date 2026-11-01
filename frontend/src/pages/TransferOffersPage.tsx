import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'

import { transferApi } from '@/api/endpoints'
import { Shell } from '@/components/Shell'
import { EmptyNote, ErrorNote, Spinner } from '@/components/States'
import { WindowBanner } from '@/components/transfers/WindowBanner'
import { PositionBadge } from '@/components/ui/Badge'
import { money } from '@/lib/money'
import { OfferStatus, type TransferOffer } from '@/types'

const STATUS_LABELS: Record<OfferStatus, string> = {
  [OfferStatus.Pending]: 'Gözləyir',
  [OfferStatus.Accepted]: 'Qəbul edildi',
  [OfferStatus.Rejected]: 'Rədd edildi',
  [OfferStatus.Withdrawn]: 'Geri çəkildi',
  [OfferStatus.Expired]: 'Vaxtı bitdi',
}

const STATUS_TONES: Record<OfferStatus, string> = {
  [OfferStatus.Pending]: 'border-draw/30 bg-draw/10 text-draw',
  [OfferStatus.Accepted]: 'border-win/30 bg-win/10 text-win',
  [OfferStatus.Rejected]: 'border-loss/30 bg-loss/10 text-loss',
  [OfferStatus.Withdrawn]: 'border-line bg-white/5 text-muted',
  [OfferStatus.Expired]: 'border-line bg-white/5 text-muted',
}

export function TransferOffersPage() {
  const { seasonId = '' } = useParams()
  const queryClient = useQueryClient()

  const window = useQuery({
    queryKey: ['transfer-window', seasonId],
    queryFn: () => transferApi.window(seasonId),
  })

  const offers = useQuery({
    queryKey: ['transfer-offers', seasonId],
    queryFn: () => transferApi.offers(seasonId),
  })

  const history = useQuery({
    queryKey: ['transfer-history', seasonId],
    queryFn: () => transferApi.history(seasonId),
  })

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['transfer-offers', seasonId] })
    queryClient.invalidateQueries({ queryKey: ['transfer-window', seasonId] })
    queryClient.invalidateQueries({ queryKey: ['transfer-history', seasonId] })
    queryClient.invalidateQueries({ queryKey: ['teams', seasonId] })
  }

  const respond = useMutation({
    mutationFn: ({ offerId, accept }: { offerId: string; accept: boolean }) =>
      transferApi.respond(seasonId, offerId, accept),
    onSuccess: refresh,
  })

  const withdraw = useMutation({
    mutationFn: (offerId: string) => transferApi.withdraw(seasonId, offerId),
    onSuccess: refresh,
  })

  if (window.isPending || offers.isPending) {
    return (
      <Shell wide>
        <Spinner />
      </Shell>
    )
  }

  if (window.isError) {
    return (
      <Shell wide>
        <h2 className="mb-5 text-xl font-bold tracking-tight">Təkliflər</h2>
        <ErrorNote error={window.error} />
      </Shell>
    )
  }

  const all = offers.data ?? []
  const incoming = all.filter((o) => o.isIncoming)
  const sent = all.filter((o) => !o.isIncoming)
  const waiting = incoming.filter((o) => o.status === OfferStatus.Pending)

  return (
    <Shell wide>
      <header className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold tracking-tight">Təkliflər</h2>
        <Link
          to={`/seasons/${seasonId}/transfers`}
          className="rounded-lg border border-line px-4 py-2 text-sm text-muted transition-colors hover:text-ink"
        >
          Bazara qayıt
        </Link>
      </header>

      <WindowBanner state={window.data!} />

      {(respond.isError || withdraw.isError) && (
        <div className="mb-4">
          <ErrorNote error={respond.error ?? withdraw.error} />
        </div>
      )}

      <div className="grid items-start gap-5 lg:grid-cols-2">
        <section>
          <h3 className="mb-2.5 text-sm font-semibold">
            Mənə gələnlər
            {waiting.length > 0 && (
              <span className="ml-2 rounded-full bg-violet px-2 py-0.5 text-[0.65rem] font-bold text-white">
                {waiting.length}
              </span>
            )}
          </h3>

          {incoming.length === 0 ? (
            <EmptyNote>Hələ oyunçularınıza təklif gəlməyib.</EmptyNote>
          ) : (
            <ul className="space-y-2">
              {incoming.map((offer) => (
                <li key={offer.id}>
                  <OfferCard offer={offer}>
                    {offer.status === OfferStatus.Pending && (
                      <>
                        <button
                          type="button"
                          disabled={respond.isPending}
                          onClick={() => respond.mutate({ offerId: offer.id, accept: true })}
                          className="rounded-lg bg-win/85 px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-win disabled:opacity-40"
                        >
                          Qəbul et
                        </button>
                        <button
                          type="button"
                          disabled={respond.isPending}
                          onClick={() => respond.mutate({ offerId: offer.id, accept: false })}
                          className="rounded-lg border border-line px-3 py-1.5 text-xs text-muted transition-colors hover:text-ink disabled:opacity-40"
                        >
                          Rədd et
                        </button>
                      </>
                    )}
                  </OfferCard>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section>
          <h3 className="mb-2.5 text-sm font-semibold">Göndərdiklərim</h3>

          {sent.length === 0 ? (
            <EmptyNote>Hələ təklif göndərməmisiniz.</EmptyNote>
          ) : (
            <ul className="space-y-2">
              {sent.map((offer) => (
                <li key={offer.id}>
                  <OfferCard offer={offer}>
                    {offer.status === OfferStatus.Pending && (
                      <button
                        type="button"
                        disabled={withdraw.isPending}
                        onClick={() => withdraw.mutate(offer.id)}
                        className="rounded-lg border border-line px-3 py-1.5 text-xs text-muted transition-colors hover:text-ink disabled:opacity-40"
                      >
                        Geri çək
                      </button>
                    )}
                  </OfferCard>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <section className="mt-6">
        <h3 className="mb-2.5 text-sm font-semibold">Mövsümün transferləri</h3>

        {history.data?.length === 0 ? (
          <EmptyNote>Bu mövsümdə hələ transfer olmayıb.</EmptyNote>
        ) : (
          <ul className="scroll-list max-h-80 space-y-1 pr-1">
            {history.data?.map((move) => (
              <li
                key={move.id}
                className="flex flex-wrap items-center gap-x-2 gap-y-0.5 rounded-lg border border-line px-3 py-2 text-sm"
              >
                <span className="font-medium">{move.playerName}</span>
                <span className="text-muted">{move.fromTeamName}</span>
                <span aria-hidden className="text-muted">
                  →
                </span>
                <span className="text-muted">{move.toTeamName}</span>
                <span className="ml-auto font-bold">{money(move.amount)}</span>
                <span className="w-16 text-right text-[0.7rem] text-muted">
                  {move.week}. həftə
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </Shell>
  )
}

function OfferCard({
  offer,
  children,
}: {
  offer: TransferOffer
  children?: React.ReactNode
}) {
  // What the club is being offered against what the player is worth, which is
  // the whole of the decision on an incoming bid.
  const premium = offer.playerValue > 0 ? offer.amount / offer.playerValue : 1

  return (
    <article className="glass-card space-y-2.5">
      <div className="flex items-start gap-3">
        <div className="min-w-0 flex-1">
          <p className="truncate font-semibold">{offer.playerName}</p>
          <div className="mt-1 flex flex-wrap items-center gap-1.5 text-[0.7rem] text-muted">
            <PositionBadge position={offer.playerPosition} />
            <span>{offer.playerOverall} OVR</span>
            <span>{offer.playerAge} yaş</span>
          </div>
          <p className="mt-0.5 truncate text-[0.7rem] text-muted">
            {offer.isIncoming ? `${offer.toTeamName} istəyir` : `${offer.fromTeamName} klubundan`}
          </p>
        </div>

        <span
          className={`shrink-0 rounded border px-2 py-0.5 text-[0.65rem] font-semibold ${STATUS_TONES[offer.status]}`}
        >
          {STATUS_LABELS[offer.status]}
        </span>
      </div>

      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-line/60 pt-2.5">
        <div>
          <div className="text-[0.65rem] text-muted">Təklif</div>
          <div className="font-bold">{money(offer.amount)}</div>
        </div>

        <div>
          <div className="text-[0.65rem] text-muted">Dəyəri</div>
          <div className="text-sm text-muted">
            {money(offer.playerValue)}
            {premium >= 1.15 && (
              <span className="ml-1 text-win">+{Math.round((premium - 1) * 100)}%</span>
            )}
          </div>
        </div>

        <div className="ml-auto flex gap-2">{children}</div>
      </div>

      {offer.responseReason && (
        <p className="text-[0.7rem] text-muted">{offer.responseReason}</p>
      )}
    </article>
  )
}
