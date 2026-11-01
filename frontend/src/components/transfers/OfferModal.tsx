import { useEffect, useState } from 'react'

import { ErrorNote } from '@/components/States'
import { PositionBadge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { money, parseMoney } from '@/lib/money'
import { ROLE_LABELS, type MarketPlayer } from '@/types'

/**
 * Making a bid.
 *
 * The selling club answers at once, so this is the one decision in the window
 * a manager makes blind: he is told what the player is worth, and what he has
 * to spend, and has to judge how much over the odds the club will want. Bidding
 * the valuation exactly is usually not enough, which is the point.
 */
export function OfferModal({
  player,
  budget,
  onClose,
  onSubmit,
  pending,
  error,
}: {
  player: MarketPlayer | null
  budget: number
  onClose: () => void
  onSubmit: (amount: number) => void
  pending: boolean
  error: unknown
}) {
  const [amount, setAmount] = useState('')

  // Open the box at the asking price, which is where most bids start.
  useEffect(() => {
    if (player) setAmount(money(player.value).replace('€', ''))
  }, [player])

  if (!player) return null

  const parsed = parseMoney(amount)
  const overBudget = parsed !== null && parsed > budget
  const valid = parsed !== null && parsed > 0 && !overBudget

  return (
    <Modal
      open
      title="Təklif ver"
      onClose={onClose}
      footer={
        <>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-line px-4 py-2 text-sm text-muted transition-colors hover:text-ink"
          >
            İmtina
          </button>
          <button
            type="button"
            disabled={!valid || pending}
            onClick={() => parsed !== null && onSubmit(parsed)}
            className="rounded-lg bg-violet px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-violet/85 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {pending ? 'Göndərilir…' : 'Təklifi göndər'}
          </button>
        </>
      }
    >
      <div className="mb-4 flex items-center gap-3">
        <TeamCrest name={player.teamName} logoUrl={player.teamLogoUrl} />
        <div className="min-w-0 flex-1">
          <p className="truncate font-semibold">{player.name}</p>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted">
            <PositionBadge position={player.position} />
            <span className="rounded border border-line px-1 text-[0.6rem] tracking-wide">
              {ROLE_LABELS[player.role]}
            </span>
            <span>
              {player.age} yaş · {player.teamName}
            </span>
          </div>
        </div>
        <div className="shrink-0 text-right">
          <div className="text-2xl leading-none font-black">{player.overall}</div>
          <div className="text-[0.6rem] tracking-wide text-muted uppercase">OVR</div>
        </div>
      </div>

      <dl className="mb-4 grid grid-cols-2 gap-2 text-sm">
        <div className="rounded-lg border border-line bg-bg/40 px-3 py-2">
          <dt className="text-[0.7rem] text-muted">Bazar dəyəri</dt>
          <dd className="font-bold">{money(player.value)}</dd>
        </div>
        <div className="rounded-lg border border-line bg-bg/40 px-3 py-2">
          <dt className="text-[0.7rem] text-muted">Büdcəniz</dt>
          <dd className="font-bold">{money(budget)}</dd>
        </div>
      </dl>

      <label className="block">
        <span className="text-sm text-muted">Təklif məbləği</span>
        <input
          type="text"
          value={amount}
          autoFocus
          onChange={(event) => setAmount(event.target.value)}
          placeholder="məs. 12.5M"
          className="mt-1 w-full rounded-lg border border-line bg-bg px-3 py-2.5 text-lg font-bold outline-none focus:border-violet"
        />
      </label>

      <p className="mt-2 text-[0.7rem] text-muted">
        Klub adətən bazar dəyərindən çox istəyir — əsas oyunçusu üçün daha çox.
      </p>

      {overBudget && (
        <p className="mt-2 text-sm text-loss">Büdcəniz bu təklif üçün kifayət etmir.</p>
      )}

      {parsed === null && amount.trim() !== '' && (
        <p className="mt-2 text-sm text-loss">Məbləği rəqəmlə yazın, məsələn 12.5M.</p>
      )}

      {Boolean(error) && (
        <div className="mt-3">
          <ErrorNote error={error} />
        </div>
      )}
    </Modal>
  )
}
