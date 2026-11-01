import { Suspense, lazy, useEffect, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'

import { ErrorNote, Spinner } from '@/components/States'
import { auth, ensureSignedIn } from '@/lib/auth'
import { DashboardPage } from '@/pages/DashboardPage'
import { LeaguesPage } from '@/pages/LeaguesPage'
import { LeagueTablePage } from '@/pages/LeagueTablePage'
import { LoginPage } from '@/pages/LoginPage'
import { NewLeaguePage } from '@/pages/NewLeaguePage'
import { PickTeamPage } from '@/pages/PickTeamPage'
import { TeamDetailPage } from '@/pages/TeamDetailPage'
import { CareerPage } from '@/pages/CareerPage'
import { CupPage } from '@/pages/CupPage'
import { SquadPage } from '@/pages/SquadPage'
import { StatisticsPage } from '@/pages/StatisticsPage'
import { TeamsPage } from '@/pages/TeamsPage'
import { TransferMarketPage } from '@/pages/TransferMarketPage'
import { TransferOffersPage } from '@/pages/TransferOffersPage'

// SignalR only matters on this one screen, so it loads with it.
const LiveMatchPage = lazy(() =>
  import('@/pages/LiveMatchPage').then((module) => ({ default: module.LiveMatchPage })),
)

export default function App() {
  const [ready, setReady] = useState(Boolean(auth.token))
  const [error, setError] = useState<unknown>(null)

  useEffect(() => {
    if (ready) return

    let cancelled = false

    ensureSignedIn()
      .then(() => !cancelled && setReady(true))
      .catch((cause) => !cancelled && setError(cause))

    return () => {
      cancelled = true
    }
  }, [ready])

  if (error) {
    return (
      <div className="mx-auto max-w-md px-4 py-20">
        <ErrorNote error={error} />
        <p className="mt-3 text-center text-xs text-muted">
          Check that the API is running on http://localhost:5199.
        </p>
      </div>
    )
  }

  if (!ready) return <Spinner label="Signing in…" />

  return (
    <Routes>
      <Route path="/" element={<LeaguesPage />} />
      <Route path="/seasons/new" element={<NewLeaguePage />} />
      <Route path="/seasons/:seasonId" element={<DashboardPage />} />
      <Route path="/seasons/:seasonId/pick-team" element={<PickTeamPage />} />
      <Route path="/seasons/:seasonId/table" element={<LeagueTablePage />} />
      <Route path="/seasons/:seasonId/teams" element={<TeamsPage />} />
      <Route path="/seasons/:seasonId/teams/:teamId" element={<TeamDetailPage />} />
      <Route path="/seasons/:seasonId/squad" element={<SquadPage />} />
      <Route path="/seasons/:seasonId/statistics" element={<StatisticsPage />} />
      <Route path="/seasons/:seasonId/cup" element={<CupPage />} />
      <Route path="/seasons/:seasonId/career" element={<CareerPage />} />
      <Route path="/seasons/:seasonId/transfers" element={<TransferMarketPage />} />
      <Route
        path="/seasons/:seasonId/transfers/offers"
        element={<TransferOffersPage />}
      />
      <Route
        path="/matches/:matchId/live"
        element={
          <Suspense fallback={<Spinner label="Matç yüklənir…" />}>
            <LiveMatchPage />
          </Suspense>
        }
      />

      {/* Kept reachable for when the login wall goes back up. */}
      <Route path="/login" element={<LoginPage />} />

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
