import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useCallback, useEffect, useRef, useState } from 'react'

import { matchApi } from '@/api/endpoints'
import { auth } from '@/lib/auth'
import { LiveMatchStatus, type LiveEvent, type LiveState } from '@/types'

/**
 * Subscribes to one match on the MatchHub and keeps the latest broadcast frame.
 *
 * The hub group is joined before the match is kicked off, so the opening
 * minutes cannot slip past while the connection is still opening. A REST
 * snapshot seeds the state as well, so re-entering a running match shows the
 * real score straight away.
 */
export function useLiveMatch(matchId: string | null) {
  const [state, setState] = useState<LiveState | null>(null)
  const [connected, setConnected] = useState(false)
  const [lastEvent, setLastEvent] = useState<LiveEvent | null>(null)

  const latest = useRef<LiveState | null>(null)
  const joined = useRef<Promise<void> | null>(null)

  // Ticks can arrive out of order under load; never let an older minute win.
  const accept = useCallback((next: LiveState) => {
    const current = latest.current
    if (current && current.matchId === next.matchId && next.minute < current.minute) return
    latest.current = next
    setState(next)
  }, [])

  useEffect(() => {
    if (!matchId) {
      latest.current = null
      joined.current = null
      setState(null)
      return
    }

    let cancelled = false

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/match', { accessTokenFactory: () => auth.token ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('Tick', accept)
    connection.on('TacticsUpdated', accept)
    connection.on('StatusChanged', accept)
    connection.on('MatchFinished', accept)
    connection.on('MatchEvent', (event: LiveEvent) => {
      if (!cancelled) setLastEvent(event)
    })

    // Callers await this before kicking off, so no tick is missed.
    joined.current = connection
      .start()
      .then(() => connection.invoke('JoinMatch', matchId))
      .then(() => {
        if (!cancelled) setConnected(true)
      })
      .catch(() => {
        if (!cancelled) setConnected(false)
      })

    // A match already in progress needs its current score right away.
    matchApi
      .state(matchId)
      .then((snapshot) => {
        if (!cancelled) accept(snapshot)
      })
      .catch(() => {
        // Nothing running yet; the kick-off response will seed the state.
      })

    return () => {
      cancelled = true
      joined.current = null
      setConnected(false)

      if (connection.state === HubConnectionState.Connected) {
        connection.invoke('LeaveMatch', matchId).catch(() => {})
      }
      connection.stop().catch(() => {})
    }
  }, [matchId, accept])

  return {
    state,
    connected,
    /** The most recent goal or substitution, for toasts and animations. */
    lastEvent,
    clearLastEvent: useCallback(() => setLastEvent(null), []),
    isFinished: state?.status === LiveMatchStatus.Finished,
    /** Resolves once the hub group is joined, so kick-off can wait for it. */
    ready: useCallback(() => joined.current ?? Promise.resolve(), []),
    /** Lets mutations push their response straight into the view. */
    apply: accept,
  }
}
