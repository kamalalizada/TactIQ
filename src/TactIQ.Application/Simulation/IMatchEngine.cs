using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

public interface IMatchEngine
{
    /// <summary>Plays a whole match at once, for every fixture the manager is not in.</summary>
    MatchSimulationResult SimulateInstant(Team home, Team away);

    /// <summary>Prepares a team for a match that will be played minute by minute.</summary>
    MatchSide CreateSide(Team team, bool isHome, TacticalStyle style);

    /// <summary>Advances a live match by one minute.</summary>
    IReadOnlyList<SimulatedEvent> PlayMinute(MatchSide home, MatchSide away, int minute);

    /// <summary>Condition, cards and injuries to carry back into the league.</summary>
    IReadOnlyList<PlayerAftermath> Aftermath(MatchSide side);

    TeamStrength ComputeStrength(Team team, bool isHome, TacticalStyle style, double formModifier);

    double GoalChancePerMinute(TeamStrength attacking, TeamStrength defending);
}
