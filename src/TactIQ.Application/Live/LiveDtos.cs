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
/// <summary>A player as they stand in this match, not as the squad stores them.</summary>
public record LivePlayerDto(
    Guid Id,
    string Name,
    Position Position,
    /// <summary>The role the player belongs in.</summary>
    PlayerRole Role,
    /// <summary>The role being filled tonight, which may not be their own.</summary>
    PlayerRole PlayedRole,
    int ShirtNumber,
    /// <summary>Tonight's rating, already reduced if the role is not theirs.</summary>
    int Overall,
    /// <summary>Remaining stamina, 0-100, which falls as the match runs.</summary>
    int Stamina,
    bool OnPitch,
    bool SentOff,
    bool Injured,
    int YellowCards);

public record LiveStateDto(
    Guid MatchId,
    Guid SeasonId,
    Guid CompetitionId,
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
    /// <summary>The managed side as it stands right now, for the bench sheet.</summary>
    IReadOnlyList<LivePlayerDto> Squad,
    IReadOnlyList<LiveEventDto> Events);
