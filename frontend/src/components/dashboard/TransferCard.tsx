import { Link } from 'react-router-dom'

import { money } from '@/lib/money'
import { OfferStatus, TransferWindow, type TransferOffer, type TransferWindowState } from '@/types'

const WINDOW_NAMES: Record<TransferWindow, string> = {
  [TransferWindow.Closed]: 'Bağlıdır',
  [TransferWindow.Summer]: 'Yay pəncərəsi',
  [TransferWindow.Winter]: 'Qış pəncərəsi',
}

/**
 * The state of the market, on the dashboard.
 *
 * Two things pull a manager to the transfer pages: an open window he might
 * miss, and a bid for one of his players waiting on an answer. Both are worth
 * surfacing here rather than leaving him to go and look.
 */
export function TransferCard({
  state,
  offers,
  seasonId,
}: {
  state: TransferWindowState | undefined
  offers: TransferOffer[] | undefined
  seasonId: string
}) {
  if (!state) return null

  const waiting = (offers ?? []).filter(
    (offer) => offer.isIncoming && offer.status === OfferStatus.Pending,
  )

  return (
    <section className="glass-card">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h3 className="text-sm font-semibold">Transfer</h3>
        <span
          className={`rounded border px-2 py-0.5 text-[0.65rem] font-semibold ${
            state.isOpen
              ? 'border-win/30 bg-win/10 text-win'
              : 'border-line bg-white/5 text-muted'
          }`}
        >
          {state.isOpen ? WINDOW_NAMES[state.window] : 'Bağlı'}
        </span>
      </div>

      <dl className="mb-3 space-y-1.5 text-sm">
        <div className="flex items-center justify-between gap-3">
          <dt className="text-muted">Büdcə</dt>
          <dd className="font-bold">{money(state.budget)}</dd>
        </div>

        <div className="flex items-center justify-between gap-3">
          <dt className="text-muted">{state.isOpen ? 'Bağlanır' : 'Açılır'}</dt>
          <dd className="text-muted">
            {state.isOpen
              ? state.closesAfterWeek !== null
                ? `${state.closesAfterWeek}. həftədən sonra`
                : '—'
              : state.opensAtWeek !== null
                ? `${state.opensAtWeek}. həftə`
                : 'Bu mövsüm yox'}
          </dd>
        </div>
      </dl>

      {waiting.length > 0 && (
        <Link
          to={`/seasons/${seasonId}/transfers/offers`}
          className="mb-2.5 flex items-center gap-2 rounded-lg border border-draw/30 bg-draw/10 px-3 py-2 text-sm text-draw transition-colors hover:bg-draw/15"
        >
          <span className="font-semibold">{waiting.length}</span>
          <span>
            {waiting.length === 1 ? 'təklif' : 'təklif'} cavabınızı gözləyir
          </span>
          <span aria-hidden className="ml-auto">
            →
          </span>
        </Link>
      )}

      <Link
        to={`/seasons/${seasonId}/transfers`}
        className="block rounded-lg border border-line px-3 py-2 text-center text-sm text-muted transition-colors hover:text-ink"
      >
        Bazara bax
      </Link>
    </section>
  )
}
