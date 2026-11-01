using TactIQ.Domain.Enums;

namespace TactIQ.Application.Seasons;

public record CreateSeasonRequest(string Name, GameMode GameMode, Guid? UserTeamId);

public record SetUserTeamRequest(Guid TeamId);

/// <summary>
/// The shape the manager started from and the eleven who start, each with the
/// spot they were placed on. The spot decides the job; see <c>PitchZones</c>.
/// </summary>
public record SetLineupRequest(string Formation, IReadOnlyList<LineupSpotRequest> Starters);

/// <summary>
/// One starter and where they stand, as percentages of the pitch: <c>X</c>
/// from the left touchline, <c>Y</c> up from the side's own goal line.
/// </summary>
public record LineupSpotRequest(Guid PlayerId, double X, double Y);

public record CompetitionSummaryDto(
    Guid Id,
    string Name,
    string? Country,
    CompetitionKind Kind,
    int TeamCount,
    int CurrentWeek,
    int TotalWeeks,
    CupRound? CurrentRound,
    Guid? WinnerTeamId,
    string? WinnerTeamName,
    bool IsFinished);

public record SeasonSummaryDto(
    Guid Id,
    string Name,
    GameMode GameMode,
    Guid? UserTeamId,
    string? UserTeamName,
    bool IsFinished,
    DateTime CreatedAt,
    IReadOnlyList<CompetitionSummaryDto> Competitions);

public record PlayerDto(
    Guid Id,
    string Name,
    Position Position,
    PlayerRole Role,
    int ShirtNumber,
    int Age,
    string Nationality,
    int Pace,
    int Shooting,
    int Passing,
    int Dribbling,
    int Defending,
    int Physical,

    // Goalkeeping; zero for an outfield player, whose six above say everything.
    int Diving,
    int Handling,
    int Kicking,
    int Reflexes,
    int Speed,
    int Positioning,
    int Overall,
    bool IsStarter,
    int LineupSlot,
    double? PitchX,
    double? PitchY,
    PlayerRole? LineupRole,
    int Condition,
    int MatchesInjured,
    int MatchesSuspended,
    int YellowCards,
    bool IsAvailable);

public record TeamDto(
    Guid Id,
    string Name,
    string Country,
    string? LogoUrl,
    string Formation,
    double AttackRating,
    double DefenseRating,
    double TeamOverall,
    IReadOnlyList<PlayerDto> Players);

public record MatchEventDto(
    int Minute,
    MatchEventType EventType,
    Guid TeamId,
    string Description,
    Guid? PlayerId,
    Guid? AssistPlayerId);

public record MatchDto(
    Guid Id,
    Guid CompetitionId,
    string CompetitionName,
    CompetitionKind Kind,
    int WeekNumber,
    string? GroupName,
    CupRound? Round,
    int? Leg,
    Guid? TieId,
    int? HomePenalties,
    int? AwayPenalties,
    Guid HomeTeamId,
    string HomeTeamName,
    string? HomeTeamLogoUrl,
    Guid AwayTeamId,
    string AwayTeamName,
    string? AwayTeamLogoUrl,
    int HomeGoals,
    int AwayGoals,
    bool IsPlayed,
    bool IsUserMatch,
    IReadOnlyList<MatchEventDto> Events);

public record StandingsRowDto(
    int Rank,
    Guid TeamId,
    string TeamName,
    string? LogoUrl,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points);

public record SimulateWeekResult(
    Guid CompetitionId,
    int WeekNumber,
    IReadOnlyList<MatchDto> SimulatedMatches,
    MatchDto? PendingUserMatch,
    int NextWeek,
    bool CompetitionFinished);
