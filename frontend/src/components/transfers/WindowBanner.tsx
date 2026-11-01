import { money } from '@/lib/money'
import { TransferWindow, type TransferWindowState } from '@/types'

const WINDOW_NAMES: Record<TransferWindow, string> = {
  [TransferWindow.Closed]: 'Bağlıdır',
  [TransferWindow.Summer]: 'Yay pəncərəsi',
  [TransferWindow.Winter]: 'Qış pəncərəsi',
}

/**
 * Whether the market is open, how long for, and what is left to spend.
 *
 * A manager planning a signing needs all three at once: there is no point
 * reading a price if the window shuts this week, or finding the right player
 * with nothing in the bank.
 */
export function WindowBanner({ state }: { state: TransferWindowState }) {
  if (!state.isOpen) {
    return (
      <div className="mb-4 flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border border-line bg-panel px-4 py-3 text-sm">
        <span className="font-semibold text-muted">Transfer pəncərəsi bağlıdır</span>
        <span className="text-muted">
          {state.opensAtWeek !== null
            ? `${state.opensAtWeek}-ci həftədə açılacaq.`
            : 'Bu mövsümdə bir daha açılmayacaq.'}
        </span>
        <span className="ml-auto text-muted">
          Büdcə <span className="font-bold text-ink">{money(state.budget)}</span>
        </span>
      </div>
    )
  }

  const weeksLeft =
    state.closesAfterWeek !== null ? state.closesAfterWeek - state.currentWeek : null

  return (
    <div className="mb-4 flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border border-win/30 bg-win/10 px-4 py-3 text-sm">
      <span className="font-semibold text-win">{WINDOW_NAMES[state.window]} açıqdır</span>

      {weeksLeft !== null && (
        <span className="text-muted">
          {weeksLeft <= 0
            ? 'Bu həftə bağlanır.'
            : `Bağlanmasına ${weeksLeft} həftə qalıb.`}
        </span>
      )}

      <span className="ml-auto text-muted">
        Büdcə <span className="font-bold text-ink">{money(state.budget)}</span>
      </span>
    </div>
  )
}
