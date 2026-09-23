using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

public record SimulatedEvent(
    int Minute,
    MatchEventType EventType,
    Guid TeamId,
    Guid? PlayerId = null,
    Guid? AssistPlayerId = null,
    string Description = "");

/// <summary>What a played match leaves behind: the score, its timeline, and the
/// wear it put on the players so the league can carry it into the next week.</summary>
public record MatchSimulationResult(
    int HomeGoals,
    int AwayGoals,
    IReadOnlyList<SimulatedEvent> Events,
    IReadOnlyList<PlayerAftermath> Aftermath);

public record PlayerAftermath(
    Guid PlayerId,
    int Condition,
    bool Injured,
    bool SentOff,
    int YellowCards);
