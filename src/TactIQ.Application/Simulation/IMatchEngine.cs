using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

public interface IMatchEngine
{
    MatchSimulationResult SimulateInstant(Team home, Team away);

    TeamStrength ComputeStrength(Team team, bool isHome, TacticalStyle style, double formModifier);

    double GoalChancePerMinute(TeamStrength attacking, TeamStrength defending);
}
