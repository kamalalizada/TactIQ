using TactIQ.Domain.Enums;

namespace TactIQ.Application.Leagues;

public record CreateLeagueRequest(string SeasonName, GameMode GameMode, Guid? UserTeamId);

public record LeagueSummaryDto(
    Guid Id,
    string SeasonName,
    GameMode GameMode,
    Guid? UserTeamId,
    int TeamCount,
    int CurrentWeek,
    int TotalWeeks,
    bool IsFinished,
    DateTime CreatedAt);

public record PlayerDto(
    Guid Id,
    string Name,
    Position Position,
    int ShirtNumber,
    int Pace,
    int Shooting,
    int Passing,
    int Dribbling,
    int Defending,
    int Physical,
    int Overall,
    bool IsStarter,
    int Condition,
    int MatchesInjured,
    int MatchesSuspended,
    int YellowCards,
    bool IsAvailable);

public record TeamDto(
    Guid Id,
    string Name,
    string? LogoUrl,
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
    Guid LeagueId,
    CompetitionType Competition,
    CupRound? Round,
    int? Leg,
    Guid? TieId,
    int? HomePenalties,
    int? AwayPenalties,
    int WeekNumber,
    Guid HomeTeamId,
    string HomeTeamName,
    Guid AwayTeamId,
    string AwayTeamName,
    int HomeGoals,
    int AwayGoals,
    bool IsPlayed,
    bool IsUserMatch,
    IReadOnlyList<MatchEventDto> Events);

public record StandingsRowDto(
    int Rank,
    Guid TeamId,
    string TeamName,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points);

public record SimulateWeekResult(
    int WeekNumber,
    IReadOnlyList<MatchDto> SimulatedMatches,
    MatchDto? PendingUserMatch,
    int NextWeek,
    bool SeasonFinished);
