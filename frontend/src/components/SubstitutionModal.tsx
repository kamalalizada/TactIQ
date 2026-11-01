import { useState } from 'react'

import { ErrorNote } from '@/components/States'
import { PositionBadge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { fitOf } from '@/lib/formations'
import { Position, ROLE_LABELS, type LivePlayer } from '@/types'

const POSITION_ORDER: Position[] = [Position.GK, Position.DEF, Position.MID, Position.FWD]

function byPosition(a: LivePlayer, b: LivePlayer) {
  const byLine = POSITION_ORDER.indexOf(a.position) - POSITION_ORDER.indexOf(b.position)
  return byLine !== 0 ? byLine : a.shirtNumber - b.shirtNumber
}

/**
 * The bench sheet for a running match. It reads the live squad rather than the
 * saved line-up, so a change here lasts for this match only — the manager's
 * standing selection is untouched.
 */
export function SubstitutionModal({
  open,
  squad,
  remaining,
  pending,
  error,
  onClose,
  onConfirm,
}: {
  open: boolean
  squad: LivePlayer[]
  remaining: number
  pending: boolean
  error: unknown
  onClose: () => void
  onConfirm: (playerOutId: string, playerInId: string) => void
}) {
  const [outId, setOutId] = useState<string | null>(null)
  const [inId, setInId] = useState<string | null>(null)

  const close = () => {
    setOutId(null)
    setInId(null)
    onClose()
  }

  const onPitch = squad.filter((player) => player.onPitch).sort(byPosition)
  const onBench = squad
    .filter((player) => !player.onPitch && !player.sentOff && !player.injured)
    .sort(byPosition)

  const ready = Boolean(outId && inId) && remaining > 0

  return (
    <Modal
      open={open}
      wide
      title={`Əvəzetmə — ${remaining} qalıb`}
      onClose={close}
      footer={
        <>
          <button type="button" className="btn-ghost" onClick={close}>
            İmtina
          </button>
          <button
            type="button"
            className="btn-primary"
            disabled={!ready || pending}
            onClick={() => outId && inId && onConfirm(outId, inId)}
          >
            {pending ? 'Tətbiq olunur…' : 'Əvəz et'}
          </button>
        </>
      }
    >
      {remaining <= 0 ? (
        <p className="py-6 text-center text-sm text-muted">
          Bütün əvəzetmələr istifadə olunub.
        </p>
      ) : (
        <>
          <p className="mb-3 text-xs text-muted">
            Bu dəyişiklik yalnız bu matça aiddir; əsas heyətiniz olduğu kimi qalır.
          </p>

          <div className="grid gap-5 sm:grid-cols-2">
            <Column
              title="Meydanda"
              hint="Çıxacaq oyunçunu seçin"
              players={onPitch}
              selectedId={outId}
              onSelect={setOutId}
              tone="loss"
            />
            <Column
              title="Ehtiyat"
              hint="Girəcək oyunçunu seçin"
              players={onBench}
              selectedId={inId}
              onSelect={setInId}
              tone="win"
            />
          </div>

          {error ? (
            <div className="mt-4">
              <ErrorNote error={error} />
            </div>
          ) : null}
        </>
      )}
    </Modal>
  )
}

function Column({
  title,
  hint,
  players,
  selectedId,
  onSelect,
  tone,
}: {
  title: string
  hint: string
  players: LivePlayer[]
  selectedId: string | null
  onSelect: (id: string) => void
  tone: 'win' | 'loss'
}) {
  const active = tone === 'win' ? 'border-win bg-win/10' : 'border-loss bg-loss/10'

  return (
    <div>
      <h4 className="text-sm font-semibold">{title}</h4>
      <p className="mb-2 text-xs text-muted">{hint}</p>

      {players.length === 0 ? (
        <p className="py-4 text-xs text-muted">Oyunçu yoxdur.</p>
      ) : (
        <ul className="scroll-list max-h-72 space-y-1 pr-1">
          {players.map((player) => {
            const isSelected = player.id === selectedId

            return (
              <li key={player.id}>
                <button
                  type="button"
                  onClick={() => onSelect(player.id)}
                  aria-pressed={isSelected}
                  className={`flex w-full items-center gap-2 rounded-md border px-2.5 py-2 text-left text-sm transition-colors ${
                    isSelected ? active : 'border-line hover:bg-white/5'
                  }`}
                >
                  <span className="w-7 shrink-0 text-xs text-muted">#{player.shirtNumber}</span>
                  <PositionBadge position={player.position} />
                  <span className="flex-1 truncate">{player.name}</span>
                  <RoleTag player={player} />

                  {/* Stamina is what decides a substitution, so it leads the numbers. */}
                  <StaminaPip value={player.stamina} />
                  <span className="w-6 shrink-0 text-right font-bold">{player.overall}</span>
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

/**
 * The job a player is doing tonight. Where it is not their own it is marked,
 * because that player is already rated below their ability — which is often
 * the reason to make the change.
 */
function RoleTag({ player }: { player: LivePlayer }) {
  const fit = player.onPitch ? fitOf(player.role, player.playedRole) : 'native'

  const tone =
    fit === 'native'
      ? 'border-line text-muted'
      : fit === 'close'
        ? 'border-draw/50 text-draw'
        : 'border-loss/50 text-loss'

  return (
    <span
      className={`shrink-0 rounded border px-1 text-[0.6rem] tracking-wide ${tone}`}
      title={
        fit === 'native'
          ? undefined
          : `Əsl mövqeyi ${ROLE_LABELS[player.role]}, bu oyunda ${ROLE_LABELS[player.playedRole]} oynayır`
      }
    >
      {ROLE_LABELS[player.onPitch ? player.playedRole : player.role]}
    </span>
  )
}

function StaminaPip({ value }: { value: number }) {
  const tone = value >= 70 ? 'bg-win' : value >= 45 ? 'bg-draw' : 'bg-loss'
  const text = value >= 70 ? 'text-win' : value >= 45 ? 'text-draw' : 'text-loss'

  return (
    <span className="flex shrink-0 items-center gap-1.5" title={`Kondisiya ${value}`}>
      <span className="h-1.5 w-10 overflow-hidden rounded-full bg-bg">
        <span className={`block h-full ${tone}`} style={{ width: `${value}%` }} />
      </span>
      <span className={`w-6 text-right text-[0.7rem] font-semibold ${text}`}>{value}</span>
    </span>
  )
}
