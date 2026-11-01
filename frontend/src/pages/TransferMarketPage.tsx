import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'

import { transferApi } from '@/api/endpoints'
import { Shell } from '@/components/Shell'
import { EmptyNote, ErrorNote, Spinner } from '@/components/States'
import { OfferModal } from '@/components/transfers/OfferModal'
import { WindowBanner } from '@/components/transfers/WindowBanner'
import { PositionBadge } from '@/components/ui/Badge'
import { TeamCrest } from '@/components/ui/TeamCrest'
import { money } from '@/lib/money'
import {
  OfferStatus,
  Position,
  ROLE_LABELS,
  type MarketPlayer,
  type MarketQuery,
  type TransferOffer,
} from '@/types'

const POSITION_FILTERS: { label: string; value: Position | undefined }[] = [
  { label: 'Hamısı', value: undefined },
  { label: 'Qapıçı', value: Position.GK },
  { label: 'Müdafiə', value: Position.DEF },
  { label: 'Yarımmüdafiə', value: Position.MID },
  { label: 'Hücum', value: Position.FWD },
]

export function TransferMarketPage() {
  const { seasonId = '' } = useParams()
  const queryClient = useQueryClient()

  const [filters, setFilters] = useState<MarketQuery>({ take: 60 })
  const [search, setSearch] = useState('')
  const [bidding, setBidding] = useState<MarketPlayer | null>(null)
  const [outcome, setOutcome] = useState<TransferOffer | null>(null)

  const window = useQuery({
    queryKey: ['transfer-window', seasonId],
    queryFn: () => transferApi.window(seasonId),
  })

  const market = useQuery({
    queryKey: ['transfer-market', seasonId, filters],
    queryFn: () => transferApi.market(seasonId, filters),
  })

  const offer = useMutation({
    mutationFn: (amount: number) =>
      transferApi.makeOffer(seasonId, { playerId: bidding!.id, amount }),
    onSuccess: (result) => {
      setBidding(null)
      setOutcome(result)

      // A signing changes the budget, the squad and the market all at once.
      queryClient.invalidateQueries({ queryKey: ['transfer-window', seasonId] })
      queryClient.invalidateQueries({ queryKey: ['transfer-market', seasonId] })
      queryClient.invalidateQueries({ queryKey: ['transfer-offers', seasonId] })
      queryClient.invalidateQueries({ queryKey: ['teams', seasonId] })
    },
  })

  const apply = (next: Partial<MarketQuery>) => setFilters((old) => ({ ...old, ...next }))

  if (window.isPending) {
    return (
      <Shell wide>
        <Spinner />
      </Shell>
    )
  }

  if (window.isError) {
    return (
      <Shell wide>
        <h2 className="mb-5 text-xl font-bold tracking-tight">Transfer bazarı</h2>
        <ErrorNote error={window.error} />
        <p className="mt-3 text-sm text-muted">
          Transfer üçün idarə etdiyiniz klub olmalıdır.{' '}
          <Link to={`/seasons/${seasonId}`} className="text-accent hover:underline">
            İdarə panelinə qayıt
          </Link>
        </p>
      </Shell>
    )
  }

  const state = window.data!

  return (
    <Shell wide>
      <header className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold tracking-tight">Transfer bazarı</h2>
        <Link
          to={`/seasons/${seasonId}/transfers/offers`}
          className="rounded-lg border border-line px-4 py-2 text-sm text-muted transition-colors hover:text-ink"
        >
          Təkliflər
        </Link>
      </header>

      <WindowBanner state={state} />

      {outcome && <OfferOutcome offer={outcome} onDismiss={() => setOutcome(null)} />}

      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div className="flex flex-wrap gap-1.5">
          {POSITION_FILTERS.map((option) => (
            <button
              key={option.label}
              type="button"
              onClick={() => apply({ position: option.value })}
              className={`rounded-lg border px-3 py-1.5 text-sm transition-colors ${
                filters.position === option.value
                  ? 'border-violet bg-violet/15 text-ink'
                  : 'border-line text-muted hover:text-ink'
              }`}
            >
              {option.label}
            </button>
          ))}
        </div>

        <form
          className="min-w-44 flex-1"
          onSubmit={(event) => {
            event.preventDefault()
            apply({ search: search.trim() || undefined })
          }}
        >
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            onBlur={() => apply({ search: search.trim() || undefined })}
            placeholder="Oyunçu adı"
            className="w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm outline-none placeholder:text-muted/60 focus:border-violet"
          />
        </form>

        <NumberFilter
          label="Min reytinq"
          value={filters.minOverall}
          onChange={(value) => apply({ minOverall: value })}
        />
        <NumberFilter
          label="Maks yaş"
          value={filters.maxAge}
          onChange={(value) => apply({ maxAge: value })}
        />

        <button
          type="button"
          onClick={() => apply({ maxPrice: filters.maxPrice ? undefined : state.budget })}
          className={`rounded-lg border px-3 py-2 text-sm transition-colors ${
            filters.maxPrice
              ? 'border-violet bg-violet/15 text-ink'
              : 'border-line text-muted hover:text-ink'
          }`}
        >
          Büdcəmə uyğun
        </button>
      </div>

      {market.isPending && <Spinner />}
      {market.isError && <ErrorNote error={market.error} />}

      {market.data?.length === 0 && (
        <EmptyNote>Bu şərtlərə uyğun oyunçu tapılmadı.</EmptyNote>
      )}

      {market.data && market.data.length > 0 && (
        <ul className="grid gap-2.5 md:grid-cols-2 xl:grid-cols-3">
          {market.data.map((player) => (
            <li key={player.id}>
              <MarketCard
                player={player}
                canBid={state.isOpen}
                onBid={() => setBidding(player)}
              />
            </li>
          ))}
        </ul>
      )}

      <OfferModal
        player={bidding}
        budget={state.budget}
        onClose={() => setBidding(null)}
        onSubmit={(amount) => offer.mutate(amount)}
        pending={offer.isPending}
        error={offer.error}
      />
    </Shell>
  )
}

function MarketCard({
  player,
  canBid,
  onBid,
}: {
  player: MarketPlayer
  canBid: boolean
  onBid: () => void
}) {
  // A player already bid for, or one his club will not sell, is shown with the
  // reason rather than hidden: a manager wants to know who is out of reach.
  const blocked = player.hasPendingOffer
    ? 'Təklifiniz gözləyir'
    : !canBid
      ? 'Pəncərə bağlıdır'
      : player.unavailable

  return (
    <article className="glass-card flex h-full flex-col gap-3">
      <div className="flex items-start gap-3">
        <TeamCrest name={player.teamName} logoUrl={player.teamLogoUrl} />

        <div className="min-w-0 flex-1">
          <p className="truncate font-semibold">{player.name}</p>
          <div className="mt-1 flex flex-wrap items-center gap-1.5 text-[0.7rem] text-muted">
            <PositionBadge position={player.position} />
            <span className="rounded border border-line px-1 tracking-wide">
              {ROLE_LABELS[player.role]}
            </span>
            <span>{player.age} yaş</span>
          </div>
          <p className="mt-0.5 truncate text-[0.7rem] text-muted">{player.teamName}</p>
        </div>

        <div className="shrink-0 text-right">
          <div className="text-2xl leading-none font-black">{player.overall}</div>
          <div className="text-[0.6rem] tracking-wide text-muted uppercase">OVR</div>
        </div>
      </div>

      <div className="mt-auto flex items-center justify-between gap-3 border-t border-line/60 pt-3">
        <div>
          <div className="text-[0.65rem] text-muted">Bazar dəyəri</div>
          <div className="font-bold">{money(player.value)}</div>
        </div>

        <button
          type="button"
          onClick={onBid}
          disabled={Boolean(blocked)}
          title={blocked ?? undefined}
          className="rounded-lg bg-violet px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-violet/85 disabled:cursor-not-allowed disabled:bg-white/5 disabled:text-muted"
        >
          {blocked ? 'Mümkün deyil' : 'Təklif ver'}
        </button>
      </div>

      {blocked && <p className="text-[0.7rem] text-muted">{blocked}</p>}
    </article>
  )
}

/** What came of a bid, which the selling club answers straight away. */
function OfferOutcome({
  offer,
  onDismiss,
}: {
  offer: TransferOffer
  onDismiss: () => void
}) {
  const signed = offer.status === OfferStatus.Accepted

  return (
    <p
      className={`mb-4 flex flex-wrap items-center gap-2 rounded-lg border px-4 py-3 text-sm ${
        signed ? 'border-win/30 bg-win/10 text-win' : 'border-draw/30 bg-draw/10 text-draw'
      }`}
    >
      <span className="font-semibold">{offer.playerName}</span>
      <span>
        {signed
          ? `${money(offer.amount)} müqabilində komandanıza qoşuldu.`
          : (offer.responseReason ?? 'Təklif rədd edildi.')}
      </span>
      <button
        type="button"
        onClick={onDismiss}
        className="ml-auto text-xs text-muted underline hover:text-ink"
      >
        bağla
      </button>
    </p>
  )
}

function NumberFilter({
  label,
  value,
  onChange,
}: {
  label: string
  value: number | undefined
  onChange: (value: number | undefined) => void
}) {
  return (
    <label className="text-sm">
      <span className="mb-1 block text-[0.7rem] text-muted">{label}</span>
      <input
        type="number"
        value={value ?? ''}
        onChange={(event) => {
          const next = Number(event.target.value)
          onChange(event.target.value === '' || !Number.isFinite(next) ? undefined : next)
        }}
        className="w-24 rounded-lg border border-line bg-panel px-3 py-2 text-sm outline-none focus:border-violet"
      />
    </label>
  )
}
