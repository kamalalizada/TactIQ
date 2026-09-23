using TactIQ.Application.Leagues;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Cup;

public record CupTieDto(
    Guid TieId,
    CupRound Round,
    Guid HomeTeamId,
    string HomeTeamName,
    Guid AwayTeamId,
    string AwayTeamName,
    int HomeAggregate,
    int AwayAggregate,
    bool IsComplete,
    Guid? WinnerTeamId,
    bool DecidedOnPenalties,
    IReadOnlyList<MatchDto> Legs);

public record CupBracketDto(
    bool Started,
    bool Finished,
    CupRound? CurrentRound,
    Guid? WinnerTeamId,
    string? WinnerTeamName,
    IReadOnlyList<CupTieDto> Ties);

public record CupRoundResult(
    CupRound Round,
    IReadOnlyList<MatchDto> SimulatedMatches,
    MatchDto? PendingUserMatch,
    CupRound? NextRound,
    bool CupFinished,
    Guid? WinnerTeamId,
    string? WinnerTeamName);
