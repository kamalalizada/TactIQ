import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

import { careerApi, seasonApi, transferApi } from '@/api/endpoints'
import { money } from '@/lib/money'
import { useActiveSeason } from '@/lib/useActiveSeason'
import {
  OfferStatus,
  type Player,
  type TransferOffer,
  type TransferWindowState,
} from '@/types'

type Tone = 'urgent' | 'info'

interface Note {
  id: string
  tone: Tone
  title: string
  detail: string
  to: string
}

/**
 * The things waiting on the manager.
 *
 * A save asks for decisions at moments he is not looking for them: a club bids
 * for his best player, a defender is injured the week of a derby, the window
 * is about to shut. None of it was surfaced — he had to go and look at four
 * pages to find out. This gathers them, newest obligation first.
 */
export function NotificationBell() {
  const seasonId = useActiveSeason()
  const [open, setOpen] = useState(false)
  const box = useRef<HTMLDivElement>(null)

  // Every one of these is read elsewhere too, so the shared keys mean the bell
  // costs nothing extra once a page has already asked for them.
  const offers = useQuery({
    queryKey: ['transfer-offers', seasonId],
    queryFn: () => transferApi.offers(seasonId!),
    enabled: Boolean(seasonId),
    retry: false,
  })

  const window = useQuery({
    queryKey: ['transfer-window', seasonId],
    queryFn: () => transferApi.window(seasonId!),
    enabled: Boolean(seasonId),
    retry: false,
  })

  const career = useQuery({
    queryKey: ['career', seasonId],
    queryFn: () => careerApi.get(seasonId!),
    enabled: Boolean(seasonId),
    retry: false,
  })

  const season = useQuery({
    queryKey: ['season', seasonId],
    queryFn: () => seasonApi.get(seasonId!),
    enabled: Boolean(seasonId),
  })

  const teams = useQuery({
    queryKey: ['teams', seasonId],
    queryFn: () => seasonApi.teams(seasonId!),
    enabled: Boolean(seasonId),
  })

  useEffect(() => {
    if (!open) return

    const onClick = (event: MouseEvent) => {
      if (!box.current?.contains(event.target as Node)) setOpen(false)
    }

    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [open])

  if (!seasonId) return null

  const myClub = teams.data?.find((team) => team.id === season.data?.userTeamId)

  const notes = gather({
    seasonId,
    offers: offers.data,
    window: window.data,
    canAdvance: career.data?.canAdvance,
    year: career.data?.currentYear,
    squad: myClub?.players,
  })

  const urgent = notes.filter((note) => note.tone === 'urgent').length

  return (
    <div ref={box} className="relative">
      <button
        type="button"
        aria-label="Bildirişlər"
        onClick={() => setOpen((was) => !was)}
        className="relative rounded-md border border-line px-2 py-1 text-sm text-muted transition-colors hover:text-ink"
      >
        ◎
        {notes.length > 0 && (
          <span
            className={`absolute -top-1.5 -right-1.5 min-w-4 rounded-full px-1 text-[0.6rem] font-bold text-white ${
              urgent > 0 ? 'bg-loss' : 'bg-violet'
            }`}
          >
            {notes.length}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute top-full right-0 z-40 mt-1 max-h-96 w-80 overflow-y-auto rounded-lg border border-line bg-panel shadow-2xl">
          <h3 className="border-b border-line px-3 py-2 text-sm font-semibold">Bildirişlər</h3>

          {notes.length === 0 ? (
            <p className="px-3 py-4 text-sm text-muted">Gözləyən bildiriş yoxdur.</p>
          ) : (
            <ul>
              {notes.map((note) => (
                <li key={note.id} className="border-b border-line/60 last:border-b-0">
                  <Link
                    to={note.to}
                    onClick={() => setOpen(false)}
                    className="block px-3 py-2.5 transition-colors hover:bg-white/5"
                  >
                    <p
                      className={`text-sm font-medium ${
                        note.tone === 'urgent' ? 'text-loss' : 'text-ink'
                      }`}
                    >
                      {note.title}
                    </p>
                    <p className="mt-0.5 text-[0.7rem] text-muted">{note.detail}</p>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}

/** Works out what is waiting, most pressing first. */
function gather({
  seasonId,
  offers,
  window,
  canAdvance,
  year,
  squad,
}: {
  seasonId: string
  offers: TransferOffer[] | undefined
  window: TransferWindowState | undefined
  canAdvance: boolean | undefined
  year: number | undefined
  squad: Player[] | undefined
}): Note[] {
  const notes: Note[] = []

  // A bid on the table is the only thing here that expires, so it leads.
  const waiting = (offers ?? []).filter(
    (offer) => offer.isIncoming && offer.status === OfferStatus.Pending,
  )

  for (const offer of waiting) {
    notes.push({
      id: `offer-${offer.id}`,
      tone: 'urgent',
      title: `${offer.toTeamName} ${offer.playerName} üçün təklif verdi`,
      detail: `${money(offer.amount)} · cavabınızı gözləyir`,
      to: `/seasons/${seasonId}/transfers/offers`,
    })
  }

  if (canAdvance) {
    notes.push({
      id: 'next-season',
      tone: 'info',
      title: 'Mövsüm bitdi',
      detail: `${year ?? ''}. mövsümü bağlayıb növbətiyə keçə bilərsiniz`,
      to: `/seasons/${seasonId}/career`,
    })
  }

  if (window?.isOpen && window.closesAfterWeek !== null) {
    const left = window.closesAfterWeek - window.currentWeek

    notes.push({
      id: 'window',
      tone: left <= 0 ? 'urgent' : 'info',
      title: left <= 0 ? 'Transfer pəncərəsi bu həftə bağlanır' : 'Transfer pəncərəsi açıqdır',
      detail: left <= 0 ? 'Son şansınız' : `Bağlanmasına ${left} həftə qalıb`,
      to: `/seasons/${seasonId}/transfers`,
    })
  }

  const injured = (squad ?? []).filter((player) => player.matchesInjured > 0)
  const banned = (squad ?? []).filter((player) => player.matchesSuspended > 0)

  if (injured.length > 0) {
    notes.push({
      id: 'injuries',
      tone: 'urgent',
      title: `${injured.length} oyunçu zədəlidir`,
      detail: injured
        .slice(0, 3)
        .map((player) => `${player.name} (${player.matchesInjured} matç)`)
        .join(', '),
      to: `/seasons/${seasonId}/squad`,
    })
  }

  if (banned.length > 0) {
    notes.push({
      id: 'bans',
      tone: 'info',
      title: `${banned.length} oyunçu cəzalıdır`,
      detail: banned
        .slice(0, 3)
        .map((player) => `${player.name} (${player.matchesSuspended} matç)`)
        .join(', '),
      to: `/seasons/${seasonId}/squad`,
    })
  }

  return notes
}
