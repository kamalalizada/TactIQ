using TactIQ.Domain.Enums;

namespace TactIQ.Application.Live;

public enum LiveMatchStatus
{
    Running = 0,
    Paused = 1,
    Finished = 2
}

public record StartLiveRequest(
    TacticalStyle Style = TacticalStyle.Balanced,
    string Formation = "4-4-2",
    int SpeedMultiplier = 1);

public record SubstitutionRequest(Guid PlayerOutId, Guid PlayerInId);

public record TacticsUpdateRequest(
    TacticalStyle? Style,
    string? Formation,
    SubstitutionRequest? Substitution);

public record SetSpeedRequest(int SpeedMultiplier);

public record LiveEventDto(
    int Minute,
    MatchEventType EventType,
    Guid TeamId,
    string Description,
    Guid? PlayerId = null,
    Guid? AssistPlayerId = null);

/// <summary>A single broadcast frame: where the match stands right now.</summary>
public record LiveStateDto(
    Guid MatchId,
    Guid LeagueId,
    int Minute,
    LiveMatchStatus Status,
    Guid HomeTeamId,
    string HomeTeamName,
    int HomeGoals,
    Guid AwayTeamId,
    string AwayTeamName,
    int AwayGoals,
    TacticalStyle UserStyle,
    string UserFormation,
    int SubstitutionsUsed,
    int SubstitutionsRemaining,
    int SpeedMultiplier,
    IReadOnlyList<LiveEventDto> Events);
