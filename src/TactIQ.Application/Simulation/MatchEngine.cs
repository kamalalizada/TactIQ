using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

public class MatchEngine : IMatchEngine
{
    private readonly Random _random;

    public MatchEngine(Random? random = null) => _random = random ?? Random.Shared;

    public MatchSimulationResult SimulateInstant(Team home, Team away)
    {
        var homeStrength = ComputeStrength(home, true, TacticalStyle.Balanced, RandomForm());
        var awayStrength = ComputeStrength(away, false, TacticalStyle.Balanced, RandomForm());

        var homeGoals = SamplePoisson(ExpectedGoals(homeStrength, awayStrength));
        var awayGoals = SamplePoisson(ExpectedGoals(awayStrength, homeStrength));

        var events = BuildGoalEvents(home.Id, homeGoals)
            .Concat(BuildGoalEvents(away.Id, awayGoals))
            .OrderBy(e => e.Minute)
            .ToList();

        return new MatchSimulationResult(homeGoals, awayGoals, events);
    }

    public TeamStrength ComputeStrength(Team team, bool isHome, TacticalStyle style, double formModifier)
    {
        var attack = team.AttackRating;
        var defense = team.DefenseRating;

        if (isHome)
        {
            attack *= EngineConstants.HomeAdvantage;
            defense *= EngineConstants.HomeAdvantage;
        }

        (attack, defense) = style switch
        {
            TacticalStyle.Defensive => (
                attack * EngineConstants.DefensiveAttackPenalty,
                defense * EngineConstants.DefensiveDefenseBonus),
            TacticalStyle.Attacking => (
                attack * EngineConstants.AttackingAttackBonus,
                defense * EngineConstants.AttackingDefensePenalty),
            _ => (attack, defense)
        };

        return new TeamStrength(
            Math.Max(1.0, attack + formModifier),
            Math.Max(1.0, defense + formModifier));
    }

    public double GoalChancePerMinute(TeamStrength attacking, TeamStrength defending) =>
        ExpectedGoals(attacking, defending) / EngineConstants.RegularTimeMinutes;

    private static double ExpectedGoals(TeamStrength attacking, TeamStrength defending) =>
        attacking.Attack / defending.Defense * EngineConstants.BaseGoalFactor;

    private double RandomForm() =>
        (_random.NextDouble() * 2 - 1) * EngineConstants.FormSwing;

    private int SamplePoisson(double lambda)
    {
        // Knuth's algorithm.
        var limit = Math.Exp(-lambda);
        var product = _random.NextDouble();
        var count = 0;

        while (product > limit)
        {
            count++;
            product *= _random.NextDouble();
        }

        return count;
    }

    private IEnumerable<SimulatedEvent> BuildGoalEvents(Guid teamId, int goals) =>
        Enumerable.Range(0, goals)
            .Select(_ => new SimulatedEvent(
                _random.Next(1, EngineConstants.RegularTimeMinutes + 1),
                MatchEventType.Goal,
                teamId));
}
