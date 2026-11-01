import { EventFeed } from '@/components/EventFeed'
import { Modal } from '@/components/ui/Modal'
import type { Match } from '@/types'

/** Opens the minute-by-minute log of a match that has already been played. */
export function MatchInspectModal({
  match,
  onClose,
}: {
  match: Match | null
  onClose: () => void
}) {
  return (
    <Modal
      open={Boolean(match)}
      title={
        match
          ? `${match.homeTeamName} ${match.homeGoals} – ${match.awayGoals} ${match.awayTeamName}`
          : ''
      }
      onClose={onClose}
    >
      {match && (
        <EventFeed
          events={match.events}
          homeTeamId={match.homeTeamId}
          emptyLabel="Bu matçda qol olmadı."
        />
      )}
    </Modal>
  )
}
