using TactIQ.Application.Seasons;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Cup;

public record GroupRowDto(
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
    int Points,
    bool Qualified);

public record GroupDto(string Name, IReadOnlyList<GroupRowDto> Table);

public record CupTieDto(
    Guid TieId,
    CupRound Round,
    Guid HomeTeamId,
    string HomeTeamName,
    string? HomeTeamLogoUrl,
    Guid AwayTeamId,
    string AwayTeamName,
    string? AwayTeamLogoUrl,
    int HomeAggregate,
    int AwayAggregate,
    bool IsComplete,
    Guid? WinnerTeamId,
    bool DecidedOnPenalties,
    IReadOnlyList<MatchDto> Legs);

public record CupBracketDto(
    Guid CompetitionId,
    string Name,
    bool Started,
    bool Finished,
    CupRound? CurrentRound,
    Guid? WinnerTeamId,
    string? WinnerTeamName,
    IReadOnlyList<GroupDto> Groups,
    IReadOnlyList<CupTieDto> Ties);

public record CupRoundResult(
    CupRound Round,
    IReadOnlyList<MatchDto> SimulatedMatches,
    MatchDto? PendingUserMatch,
    CupRound? NextRound,
    bool CupFinished,
    Guid? WinnerTeamId,
    string? WinnerTeamName);
