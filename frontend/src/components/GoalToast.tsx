import { useEffect } from 'react'

import { isMajorEvent } from '@/components/EventFeed'
import { MatchEventType, type LiveEvent } from '@/types'

const SCORING: number[] = [MatchEventType.Goal, MatchEventType.Penalty, MatchEventType.OwnGoal]

/** Announces the events worth interrupting for: goals, cards, substitutions. */
export function GoalToast({ event, onDone }: { event: LiveEvent | null; onDone: () => void }) {
  const worthShowing = event !== null && isMajorEvent(event)

  useEffect(() => {
    if (!worthShowing) return
    const timer = setTimeout(onDone, 3000)
    return () => clearTimeout(timer)
  }, [worthShowing, event, onDone])

  if (!event || !worthShowing) return null

  const isGoal = SCORING.includes(event.eventType)

  return (
    <div
      role="status"
      aria-live="polite"
      className="pointer-events-none fixed inset-x-0 top-20 z-50 flex justify-center px-4"
    >
      <div
        className={`animate-[toast_0.25s_ease-out] rounded-lg border px-6 py-4 text-center shadow-2xl ${
          isGoal ? 'border-win/40 bg-win/15' : 'border-line bg-panel'
        }`}
      >
        {isGoal && <p className="text-3xl font-black tracking-tight text-win">GOAL!</p>}
        <p className={`text-sm text-ink ${isGoal ? 'mt-1' : ''}`}>
          {event.minute}′ — {event.description}
        </p>
      </div>
    </div>
  )
}
