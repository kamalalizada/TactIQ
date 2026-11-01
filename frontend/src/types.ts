// Mirrors the DTOs exposed by TactIQ.API.

export const GameMode = { GodMode: 0, ManagerMode: 1 } as const
export type GameMode = (typeof GameMode)[keyof typeof GameMode]

export const Position = { GK: 0, DEF: 1, MID: 2, FWD: 3 } as const
export type Position = (typeof Position)[keyof typeof Position]

/**
 * Where a player actually belongs. Position says which line of a formation a
 * player fills; this says whereabouts in that line, so a left-back fielded on
 * the right is a worse left-back rather than simply a defender.
 */
export const PlayerRole = {
  GK: 0,
  LB: 1,
  CB: 2,
  RB: 3,
  CDM: 4,
  CM: 5,
  CAM: 6,
  LM: 7,
  RM: 8,
  LW: 9,
  ST: 10,
  RW: 11,
  // Wing-backs come after the rest so stored roles keep their meaning.
  LWB: 12,
  RWB: 13,
} as const
export type PlayerRole = (typeof PlayerRole)[keyof typeof PlayerRole]

/** The short label football uses for each role, as it is printed on a shirt. */
export const ROLE_LABELS: Record<PlayerRole, string> = {
  [PlayerRole.GK]: 'GK',
  [PlayerRole.LB]: 'LB',
  [PlayerRole.CB]: 'CB',
  [PlayerRole.RB]: 'RB',
  [PlayerRole.CDM]: 'CDM',
  [PlayerRole.CM]: 'CM',
  [PlayerRole.CAM]: 'CAM',
  [PlayerRole.LM]: 'LM',
  [PlayerRole.RM]: 'RM',
  [PlayerRole.LW]: 'LW',
  [PlayerRole.ST]: 'ST',
  [PlayerRole.RW]: 'RW',
  [PlayerRole.LWB]: 'LWB',
  [PlayerRole.RWB]: 'RWB',
}

export const TacticalStyle = { Defensive: 0, Balanced: 1, Attacking: 2 } as const
export type TacticalStyle = (typeof TacticalStyle)[keyof typeof TacticalStyle]

export const MatchEventType = {
  Goal: 0,
  YellowCard: 1,
  RedCard: 2,
  Substitution: 3,
  Injury: 4,
  Assist: 5,
  Save: 6,
  ShotOffTarget: 7,
  Penalty: 8,
  OwnGoal: 9,
} as const
export type MatchEventType = (typeof MatchEventType)[keyof typeof MatchEventType]

export const LiveMatchStatus = { Running: 0, Paused: 1, Finished: 2 } as const
export type LiveMatchStatus = (typeof LiveMatchStatus)[keyof typeof LiveMatchStatus]

export const CupRound = {
  GroupStage: 0,
  RoundOf16: 1,
  QuarterFinal: 2,
  SemiFinal: 3,
  Final: 4,
} as const
export type CupRound = (typeof CupRound)[keyof typeof CupRound]

export const CUP_ROUND_LABELS: Record<CupRound, string> = {
  0: 'Group Stage',
  1: 'Round of 16',
  2: 'Quarterfinals',
  3: 'Semifinals',
  4: 'Final',
}

/** A season now holds several competitions rather than being one table. */
export const CompetitionKind = { League: 0, ChampionsLeague: 1, DomesticCup: 2 } as const
export type CompetitionKind = (typeof CompetitionKind)[keyof typeof CompetitionKind]

export interface AuthResponse {
  token: string
  email: string
  expiresAt: string
}

export interface CompetitionSummary {
  id: string
  name: string
  country: string | null
  kind: CompetitionKind
  teamCount: number
  currentWeek: number
  totalWeeks: number
  currentRound: CupRound | null
  winnerTeamId: string | null
  winnerTeamName: string | null
  isFinished: boolean
}

export interface SeasonSummary {
  id: string
  name: string
  gameMode: GameMode
  userTeamId: string | null
  userTeamName: string | null
  isFinished: boolean
  createdAt: string
  competitions: CompetitionSummary[]
}

export interface Player {
  id: string
  name: string
  position: Position
  /** The role this player is best at; away from it they play below their rating. */
  role: PlayerRole
  shirtNumber: number
  age: number
  nationality: string
  pace: number
  shooting: number
  passing: number
  dribbling: number
  defending: number
  physical: number

  // Goalkeeping; zero for an outfield player.
  diving: number
  handling: number
  kicking: number
  reflexes: number
  speed: number
  positioning: number

  overall: number
  isStarter: boolean
  /** Where a starter stands in the shape, counted across each line from the left. */
  lineupSlot: number
  /** Where the manager placed a starter, as a percentage from the left touchline; null if never placed. */
  pitchX: number | null
  /** How far up the pitch a placed starter stands, as a percentage from their own goal line. */
  pitchY: number | null
  /** The job the spot a starter was placed on asks of them. */
  lineupRole: PlayerRole | null
  /** Fitness carried between matches, 0-100. */
  condition: number
  matchesInjured: number
  matchesSuspended: number
  yellowCards: number
  /** False while injured or suspended; such a player cannot be picked. */
  isAvailable: boolean
}

export interface Team {
  id: string
  name: string
  country: string
  logoUrl: string | null
  /** The shape the manager has chosen, as "4-4-2". */
  formation: string
  attackRating: number
  defenseRating: number
  teamOverall: number
  players: Player[]
}

export interface MatchEvent {
  minute: number
  eventType: MatchEventType
  teamId: string
  description: string
  playerId: string | null
  assistPlayerId: string | null
}

export interface Match {
  id: string
  competitionId: string
  competitionName: string
  kind: CompetitionKind
  weekNumber: number
  groupName: string | null
  round: CupRound | null
  leg: number | null
  tieId: string | null
  homePenalties: number | null
  awayPenalties: number | null
  homeTeamId: string
  homeTeamName: string
  homeTeamLogoUrl: string | null
  awayTeamId: string
  awayTeamName: string
  awayTeamLogoUrl: string | null
  homeGoals: number
  awayGoals: number
  isPlayed: boolean
  isUserMatch: boolean
  events: MatchEvent[]
}

export interface StandingsRow {
  rank: number
  teamId: string
  teamName: string
  logoUrl: string | null
  played: number
  won: number
  drawn: number
  lost: number
  goalsFor: number
  goalsAgainst: number
  goalDifference: number
  points: number
}

export interface SimulateWeekResult {
  competitionId: string
  weekNumber: number
  simulatedMatches: Match[]
  pendingUserMatch: Match | null
  nextWeek: number
  competitionFinished: boolean
}

export interface LiveEvent {
  minute: number
  eventType: MatchEventType
  teamId: string
  description: string
  playerId: string | null
  assistPlayerId: string | null
}

/** A player as they stand in the running match, not as the squad stores them. */
export interface LivePlayer {
  id: string
  name: string
  position: Position
  /** The role this player belongs in. */
  role: PlayerRole
  /** The role being filled tonight, which may not be their own. */
  playedRole: PlayerRole
  shirtNumber: number
  /** Tonight's rating, already reduced if the role is not theirs. */
  overall: number
  /** Remaining stamina, 0-100, which falls as the match runs. */
  stamina: number
  onPitch: boolean
  sentOff: boolean
  injured: boolean
  yellowCards: number
}

export interface LiveState {
  matchId: string
  seasonId: string
  competitionId: string
  minute: number
  status: LiveMatchStatus
  homeTeamId: string
  homeTeamName: string
  homeGoals: number
  awayTeamId: string
  awayTeamName: string
  awayGoals: number
  userStyle: TacticalStyle
  userFormation: string
  substitutionsUsed: number
  substitutionsRemaining: number
  speedMultiplier: number
  /** The managed side as it stands right now, for the bench sheet. */
  squad: LivePlayer[]
  events: LiveEvent[]
}

export interface CreateSeasonRequest {
  name: string
  gameMode: GameMode
  userTeamId: string | null
}

export interface StartLiveRequest {
  style: TacticalStyle
  formation: string
  speedMultiplier: number
}

export interface TacticsUpdateRequest {
  style?: TacticalStyle | null
  formation?: string | null
  substitution?: { playerOutId: string; playerInId: string } | null
}

export interface PlayerStatRow {
  rank: number
  playerId: string
  playerName: string
  position: Position
  teamId: string
  teamName: string
  goals: number
  assists: number
  yellowCards: number
  redCards: number
  matchesPlayed: number
}

export interface TeamFormRow {
  teamId: string
  teamName: string
  /** Most recent result first: "W", "D" or "L". */
  form: string[]
  goalsFor: number
  goalsAgainst: number
  pointsFromForm: number
}

export interface LeagueStatistics {
  topScorers: PlayerStatRow[]
  topAssists: PlayerStatRow[]
  mostBooked: PlayerStatRow[]
  form: TeamFormRow[]
  matchesPlayed: number
  totalGoals: number
  goalsPerMatch: number
  cleanSheets: number
  biggestWin: string | null
}

export interface GroupRow {
  rank: number
  teamId: string
  teamName: string
  logoUrl: string | null
  played: number
  won: number
  drawn: number
  lost: number
  goalsFor: number
  goalsAgainst: number
  goalDifference: number
  points: number
  qualified: boolean
}

export interface CupGroup {
  name: string
  table: GroupRow[]
}

export interface CupTie {
  tieId: string
  round: CupRound
  homeTeamId: string
  homeTeamName: string
  homeTeamLogoUrl: string | null
  awayTeamId: string
  awayTeamName: string
  awayTeamLogoUrl: string | null
  homeAggregate: number
  awayAggregate: number
  isComplete: boolean
  winnerTeamId: string | null
  decidedOnPenalties: boolean
  legs: Match[]
}

export interface CupBracket {
  competitionId: string
  name: string
  started: boolean
  finished: boolean
  currentRound: CupRound | null
  winnerTeamId: string | null
  winnerTeamName: string | null
  groups: CupGroup[]
  ties: CupTie[]
}

export interface CupRoundResult {
  round: CupRound
  simulatedMatches: Match[]
  pendingUserMatch: Match | null
  nextRound: CupRound | null
  cupFinished: boolean
  winnerTeamId: string | null
  winnerTeamName: string | null
}

export interface HeadToHeadMatch {
  matchId: string
  weekNumber: number
  homeTeamName: string
  homeGoals: number
  awayGoals: number
  awayTeamName: string
}

export interface HeadToHeadSummary {
  teamAId: string
  teamAName: string
  teamBId: string
  teamBName: string
  teamAWins: number
  draws: number
  teamBWins: number
  teamAGoals: number
  teamBGoals: number
  matches: HeadToHeadMatch[]
}

/** One starter and the spot they were placed on; the spot decides their job. */
export interface LineupSpot {
  playerId: string
  x: number
  y: number
}

export interface SetLineupRequest {
  /** The preset shape the manager last picked, which the spots were laid out from. */
  formation: string
  starters: LineupSpot[]
}

/** When the market is open. Mirrors the backend's TransferWindow. */
export const TransferWindow = { Closed: 0, Summer: 1, Winter: 2 } as const
export type TransferWindow = (typeof TransferWindow)[keyof typeof TransferWindow]

/** Where a bid has got to. */
export const OfferStatus = {
  Pending: 0,
  Accepted: 1,
  Rejected: 2,
  Withdrawn: 3,
  Expired: 4,
} as const
export type OfferStatus = (typeof OfferStatus)[keyof typeof OfferStatus]

export interface TransferWindowState {
  window: TransferWindow
  isOpen: boolean
  currentWeek: number
  /** Last week of the window now open; null while it is shut. */
  closesAfterWeek: number | null
  /** Week the market next opens; null once winter has passed. */
  opensAtWeek: number | null
  /** What the managed club has left to spend. */
  budget: number
}

/** A player as the market lists him. */
export interface MarketPlayer {
  id: string
  name: string
  position: Position
  role: PlayerRole
  age: number
  nationality: string
  overall: number
  pace: number
  shooting: number
  passing: number
  dribbling: number
  defending: number
  physical: number
  teamId: string
  teamName: string
  teamLogoUrl: string | null
  /** What he is worth; a club will usually want more than this. */
  value: number
  hasPendingOffer: boolean
  /** Why he cannot be bought right now, or null if he can. */
  unavailable: string | null
}

export interface TransferOffer {
  id: string
  playerId: string
  playerName: string
  playerPosition: Position
  playerOverall: number
  playerAge: number
  playerValue: number
  fromTeamId: string
  fromTeamName: string
  toTeamId: string
  toTeamName: string
  amount: number
  status: OfferStatus
  weekMade: number
  responseReason: string | null
  /** True when the managed club is the one being asked to sell. */
  isIncoming: boolean
}

export interface CompletedTransfer {
  id: string
  playerId: string
  playerName: string
  fromTeamName: string
  toTeamName: string
  amount: number
  week: number
  window: TransferWindow
  completedAt: string
}

export interface MakeOfferRequest {
  playerId: string
  amount: number
}

/** Filters for the market list; every one of them optional. */
export interface MarketQuery {
  position?: Position
  minOverall?: number
  maxAge?: number
  maxPrice?: number
  search?: string
  take?: number
}

/** How one league finished in one year of a career. */
export interface SeasonRecord {
  year: number
  competitionName: string
  country: string | null
  championTeamId: string | null
  championTeamName: string | null
  /** Where the managed club finished, if it played in this league. */
  userRank: number | null
  userPoints: number | null
  userBudget: number | null
  /** True when the manager's own club won it. */
  userWon: boolean
}

/** A player whose career ended, and the youngster who took his place. */
export interface Retirement {
  name: string
  age: number
  overall: number
  teamName: string
  replacedBy: string | null
}

/** A player whose rating moved over the summer. */
export interface Development {
  name: string
  age: number
  before: number
  after: number
  teamName: string
}

/** What a year did, shown when the next one begins. */
export interface SeasonRollover {
  /** The year just closed. */
  year: number
  /** The year now beginning. */
  nextYear: number
  records: SeasonRecord[]
  retired: Retirement[]
  improved: Development[]
  declined: Development[]
}

/** A manager's record across every season played. */
export interface Career {
  currentYear: number
  /** True once every competition is finished and the year can be closed. */
  canAdvance: boolean
  userTeamName: string | null
  titlesWon: number
  history: SeasonRecord[]
}

/** A club the search turned up. */
export interface TeamHit {
  id: string
  name: string
  country: string
  logoUrl: string | null
  teamOverall: number
}

/** A player the search turned up, with the club he plays for. */
export interface PlayerHit {
  id: string
  name: string
  position: Position
  role: PlayerRole
  age: number
  overall: number
  teamId: string
  teamName: string
  teamLogoUrl: string | null
  /** True when he plays for the club the manager runs. */
  isMine: boolean
}

export interface SearchResult {
  teams: TeamHit[]
  players: PlayerHit[]
}
