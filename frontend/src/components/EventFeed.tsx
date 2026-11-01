import { MatchEventType, type LiveEvent, type MatchEvent } from '@/types'

const ICONS: Record<number, { glyph: string; tone: string }> = {
  [MatchEventType.Goal]: { glyph: '⚽', tone: 'bg-win/15 text-win' },
  [MatchEventType.YellowCard]: { glyph: '▮', tone: 'bg-draw/15 text-draw' },
  [MatchEventType.RedCard]: { glyph: '▮', tone: 'bg-loss/15 text-loss' },
  [MatchEventType.Substitution]: { glyph: '⇄', tone: 'bg-accent/15 text-accent' },
  [MatchEventType.Injury]: { glyph: '✚', tone: 'bg-loss/15 text-loss' },
  [MatchEventType.Assist]: { glyph: '→', tone: 'bg-accent/15 text-accent' },
  [MatchEventType.Save]: { glyph: '✋', tone: 'bg-white/5 text-muted' },
  [MatchEventType.ShotOffTarget]: { glyph: '↗', tone: 'bg-white/5 text-muted' },
  [MatchEventType.Penalty]: { glyph: '◎', tone: 'bg-draw/15 text-draw' },
  [MatchEventType.OwnGoal]: { glyph: '⚽', tone: 'bg-loss/15 text-loss' },
}

/** Saves and wayward shots are colour but not news; they can be hidden. */
const MINOR: number[] = [MatchEventType.Save, MatchEventType.ShotOffTarget]

export function isMajorEvent(event: LiveEvent | MatchEvent) {
  return !MINOR.includes(event.eventType)
}

export function EventFeed({
  events,
  homeTeamId,
  newestFirst = false,
  emptyLabel = 'No events yet.',
}: {
  events: (LiveEvent | MatchEvent)[]
  homeTeamId: string
  /** Live matches read best newest-first; a finished log reads chronologically. */
  newestFirst?: boolean
  emptyLabel?: string
}) {
  if (events.length === 0) {
    return <p className="py-8 text-center text-sm text-muted">{emptyLabel}</p>
  }

  const ordered = newestFirst ? [...events].reverse() : events

  return (
    <ol className="divide-y divide-line">
      {ordered.map((event, index) => {
        const icon = ICONS[event.eventType] ?? { glyph: '•', tone: 'bg-white/5 text-muted' }
        const isHome = event.teamId === homeTeamId
        const minor = !isMajorEvent(event)

        return (
          <li
            key={`${event.minute}-${index}`}
            className={`flex items-center gap-3 py-2.5 ${minor ? 'opacity-60' : ''}`}
          >
            <span className="w-8 shrink-0 text-right text-xs font-bold text-muted">
              {event.minute}′
            </span>
            <span
              aria-hidden
              className={`flex size-6 shrink-0 items-center justify-center rounded-full text-xs ${icon.tone}`}
            >
              {icon.glyph}
            </span>
            <span className={`flex-1 ${minor ? 'text-xs text-muted' : 'text-sm'}`}>
              {event.description}
            </span>
            {/* A quiet marker for which end of the pitch the event belongs to. */}
            <span aria-hidden className="shrink-0 text-[0.6rem] text-muted/50">
              {isHome ? 'H' : 'A'}
            </span>
          </li>
        )
      })}
    </ol>
  )
}
