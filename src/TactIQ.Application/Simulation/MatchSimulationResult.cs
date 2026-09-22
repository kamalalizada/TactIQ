using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

public record SimulatedEvent(int Minute, MatchEventType EventType, Guid TeamId);

public record MatchSimulationResult(
    int HomeGoals,
    int AwayGoals,
    IReadOnlyList<SimulatedEvent> Events);
