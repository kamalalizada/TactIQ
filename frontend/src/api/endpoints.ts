import { api } from './client'
import type {
  AuthResponse,
  CreateSeasonRequest,
  Career,
  CompletedTransfer,
  CupBracket,
  CupRoundResult,
  HeadToHeadSummary,
  LeagueStatistics,
  LiveState,
  MakeOfferRequest,
  MarketPlayer,
  MarketQuery,
  Match,
  SearchResult,
  SeasonRollover,
  SeasonSummary,
  SetLineupRequest,
  SimulateWeekResult,
  StandingsRow,
  StartLiveRequest,
  TacticsUpdateRequest,
  Team,
  TransferOffer,
  TransferWindowState,
} from '@/types'

export const authApi = {
  register: (email: string, password: string) =>
    api.post<AuthResponse>('/auth/register', { email, password }),
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/auth/login', { email, password }),
  /** Opens a throwaway account, so a visitor can play without registering. */
  guest: () => api.post<AuthResponse>('/auth/guest'),
}

/** A season owns the squads and holds one or more competitions. */
export const seasonApi = {
  list: () => api.get<SeasonSummary[]>('/seasons'),
  get: (seasonId: string) => api.get<SeasonSummary>(`/seasons/${seasonId}`),
  create: (request: CreateSeasonRequest) => api.post<SeasonSummary>('/seasons', request),
  remove: (seasonId: string) => api.delete<void>(`/seasons/${seasonId}`),
  teams: (seasonId: string, country?: string) =>
    api.get<Team[]>(
      `/seasons/${seasonId}/teams${country ? `?country=${encodeURIComponent(country)}` : ''}`,
    ),
  /** Sets the managed club's formation and starting eleven. */
  setLineup: (seasonId: string, request: SetLineupRequest) =>
    api.patch<Team>(`/seasons/${seasonId}/lineup`, request),
  /** Picks the managed club; only allowed before the season kicks off. */
  setUserTeam: (seasonId: string, teamId: string) =>
    api.patch<SeasonSummary>(`/seasons/${seasonId}/user-team`, { teamId }),
  /** Finds clubs and players by name, for the header search box. */
  search: (seasonId: string, query: string) =>
    api.get<SearchResult>(
      `/seasons/${seasonId}/search?q=${encodeURIComponent(query)}`,
    ),
  /** Season-wide by default; pass a competition to narrow it to one league. */
  statistics: (seasonId: string, competitionId?: string) =>
    api.get<LeagueStatistics>(
      `/seasons/${seasonId}/statistics${competitionId ? `?competitionId=${competitionId}` : ''}`,
    ),
  headToHead: (seasonId: string, teamA: string, teamB: string) =>
    api.get<HeadToHeadSummary>(
      `/seasons/${seasonId}/head-to-head?teamA=${teamA}&teamB=${teamB}`,
    ),
  /**
   * Advances every competition in the season by one week. Pass includeUserMatch
   * to settle the manager's own match rather than leaving it to be played live.
   */
  simulateWeek: (seasonId: string, includeUserMatch = false) =>
    api.post<SimulateWeekResult[]>(
      `/seasons/${seasonId}/simulate-week${includeUserMatch ? '?includeUserMatch=true' : ''}`,
    ),
}

/** Tables and fixtures belong to a competition inside a season. */
export const competitionApi = {
  standings: (competitionId: string) =>
    api.get<StandingsRow[]>(`/competitions/${competitionId}/standings`),
  fixtures: (competitionId: string, week?: number) =>
    api.get<Match[]>(`/competitions/${competitionId}/fixtures${week ? `?week=${week}` : ''}`),
  simulateWeek: (competitionId: string, includeUserMatch = false) =>
    api.post<SimulateWeekResult>(
      `/competitions/${competitionId}/simulate-week${
        includeUserMatch ? '?includeUserMatch=true' : ''
      }`,
    ),
}

export const cupApi = {
  bracket: (seasonId: string) => api.get<CupBracket>(`/seasons/${seasonId}/champions-league`),
  start: (seasonId: string) => api.post<CupBracket>(`/seasons/${seasonId}/champions-league/start`),
  playRound: (seasonId: string, includeUserMatch = false) =>
    api.post<CupRoundResult>(
      `/seasons/${seasonId}/champions-league/play-round${
        includeUserMatch ? '?includeUserMatch=true' : ''
      }`,
    ),
  playAll: (seasonId: string, includeUserMatch = false) =>
    api.post<CupRoundResult>(
      `/seasons/${seasonId}/champions-league/play-all${
        includeUserMatch ? '?includeUserMatch=true' : ''
      }`,
    ),
}

/** Buying and selling players during a season's transfer windows. */
export const transferApi = {
  /** Whether the market is open, and what the managed club can spend. */
  window: (seasonId: string) =>
    api.get<TransferWindowState>(`/seasons/${seasonId}/transfers/window`),

  /** Players at other clubs. Every filter is optional. */
  market: (seasonId: string, query: MarketQuery = {}) => {
    const params = new URLSearchParams()

    if (query.position !== undefined) params.set('position', String(query.position))
    if (query.minOverall !== undefined) params.set('minOverall', String(query.minOverall))
    if (query.maxAge !== undefined) params.set('maxAge', String(query.maxAge))
    if (query.maxPrice !== undefined) params.set('maxPrice', String(query.maxPrice))
    if (query.search) params.set('search', query.search)
    if (query.take !== undefined) params.set('take', String(query.take))

    const search = params.toString()

    return api.get<MarketPlayer[]>(
      `/seasons/${seasonId}/transfers/market${search ? `?${search}` : ''}`,
    )
  },

  /** Bids the managed club has made and received. */
  offers: (seasonId: string) =>
    api.get<TransferOffer[]>(`/seasons/${seasonId}/transfers/offers`),

  /** Every move completed this season, whoever made it. */
  history: (seasonId: string) =>
    api.get<CompletedTransfer[]>(`/seasons/${seasonId}/transfers/history`),

  /**
   * Bid for a player. The selling club answers at once, so the result says
   * whether the signing went through or what it would have taken.
   */
  makeOffer: (seasonId: string, request: MakeOfferRequest) =>
    api.post<TransferOffer>(`/seasons/${seasonId}/transfers/offers`, request),

  /** Accept or turn down a bid for one of your players. */
  respond: (seasonId: string, offerId: string, accept: boolean) =>
    api.patch<TransferOffer>(`/seasons/${seasonId}/transfers/offers/${offerId}`, { accept }),

  /** Pull one of your own bids before it is answered. */
  withdraw: (seasonId: string, offerId: string) =>
    api.delete<TransferOffer>(`/seasons/${seasonId}/transfers/offers/${offerId}`),
}

/** A save that runs on from one season to the next. */
export const careerApi = {
  /** What the manager has won, and whether the year can be closed. */
  get: (seasonId: string) => api.get<Career>(`/seasons/${seasonId}/career`),

  /**
   * Closes the season and opens the next one: squads age, careers end, youth
   * players come through and the fixtures are redrawn.
   */
  nextSeason: (seasonId: string) =>
    api.post<SeasonRollover>(`/seasons/${seasonId}/career/next-season`),
}

export const matchApi = {
  startLive: (matchId: string, request: StartLiveRequest) =>
    api.post<LiveState>(`/matches/${matchId}/start-live`, request),
  state: (matchId: string) => api.get<LiveState>(`/matches/${matchId}/live`),
  updateTactics: (matchId: string, request: TacticsUpdateRequest) =>
    api.patch<LiveState>(`/matches/${matchId}/tactics`, request),
  setSpeed: (matchId: string, speedMultiplier: number) =>
    api.post<LiveState>(`/matches/${matchId}/speed`, { speedMultiplier }),
  pause: (matchId: string) => api.post<LiveState>(`/matches/${matchId}/pause`),
  resume: (matchId: string) => api.post<LiveState>(`/matches/${matchId}/resume`),
  finish: (matchId: string) => api.post<LiveState>(`/matches/${matchId}/finish`),
}
