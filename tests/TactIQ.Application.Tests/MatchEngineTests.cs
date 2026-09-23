using TactIQ.Application.Simulation;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class MatchEngineTests
{
    private readonly MatchEngine _engine = new(new Random(12345));

    [Fact]
    public void GoalsMatchTheGeneratedGoalEvents()
    {
        var home = TestTeamFactory.Create("Home", 75);
        var away = TestTeamFactory.Create("Away", 75);

        var result = _engine.SimulateInstant(home, away);
        var goals = result.Events.Where(e => e.EventType == MatchEventType.Goal).ToList();

        Assert.Equal(result.HomeGoals, goals.Count(e => e.TeamId == home.Id));
        Assert.Equal(result.AwayGoals, goals.Count(e => e.TeamId == away.Id));
        Assert.All(goals, e => Assert.NotNull(e.PlayerId));
    }

    [Fact]
    public void EventsAreChronologicalAndWithinRegularTime()
    {
        var result = _engine.SimulateInstant(
            TestTeamFactory.Create("Home", 80),
            TestTeamFactory.Create("Away", 80));

        Assert.Equal(result.Events.OrderBy(e => e.Minute), result.Events);
        Assert.All(result.Events, e => Assert.InRange(e.Minute, 1, EngineConstants.RegularTimeMinutes));
    }

    [Fact]
    public void StrongerTeamWinsMoreOftenAcrossManySimulations()
    {
        var engine = new MatchEngine(new Random(999));
        var strong = TestTeamFactory.Create("Strong", 88);
        var weak = TestTeamFactory.Create("Weak", 58);

        var strongWins = 0;
        var weakWins = 0;

        for (var i = 0; i < 500; i++)
        {
            var result = engine.SimulateInstant(strong, weak);
            if (result.HomeGoals > result.AwayGoals) strongWins++;
            else if (result.AwayGoals > result.HomeGoals) weakWins++;
        }

        Assert.True(strongWins > weakWins * 2, $"strong={strongWins}, weak={weakWins}");
    }

    [Fact]
    public void HomeAdvantageLiftsTheAttackOnly()
    {
        var team = TestTeamFactory.Create("Team", 70);

        var atHome = _engine.ComputeStrength(team, true, TacticalStyle.Balanced, 0);
        var away = _engine.ComputeStrength(team, false, TacticalStyle.Balanced, 0);

        // Boosting both would cancel out in every attack-versus-defence ratio.
        Assert.True(atHome.Attack > away.Attack);
        Assert.Equal(away.Defense, atHome.Defense, 3);
    }

    [Fact]
    public void DefensiveStyleTradesAttackForDefense()
    {
        var team = TestTeamFactory.Create("Team", 70);

        var balanced = _engine.ComputeStrength(team, false, TacticalStyle.Balanced, 0);
        var defensive = _engine.ComputeStrength(team, false, TacticalStyle.Defensive, 0);

        Assert.True(defensive.Defense > balanced.Defense);
        Assert.True(defensive.Attack < balanced.Attack);
    }

    [Fact]
    public void AttackingStyleTradesDefenseForAttack()
    {
        var team = TestTeamFactory.Create("Team", 70);

        var balanced = _engine.ComputeStrength(team, false, TacticalStyle.Balanced, 0);
        var attacking = _engine.ComputeStrength(team, false, TacticalStyle.Attacking, 0);

        Assert.True(attacking.Attack > balanced.Attack);
        Assert.True(attacking.Defense < balanced.Defense);
    }

    [Fact]
    public void PerMinuteChanceIsAReasonableProbability()
    {
        var attacking = _engine.ComputeStrength(TestTeamFactory.Create("A", 80), true, TacticalStyle.Balanced, 0);
        var defending = _engine.ComputeStrength(TestTeamFactory.Create("D", 70), false, TacticalStyle.Balanced, 0);

        var chance = _engine.GoalChancePerMinute(attacking, defending);

        Assert.InRange(chance, 0.0, 0.2);
    }

    [Fact]
    public void TacticalStyleShiftsGoalChanceInTheExpectedDirection()
    {
        var attacker = TestTeamFactory.Create("A", 75);
        var defender = TestTeamFactory.Create("D", 75);

        var defending = _engine.ComputeStrength(defender, false, TacticalStyle.Balanced, 0);

        var balancedChance = _engine.GoalChancePerMinute(
            _engine.ComputeStrength(attacker, true, TacticalStyle.Balanced, 0), defending);
        var attackingChance = _engine.GoalChancePerMinute(
            _engine.ComputeStrength(attacker, true, TacticalStyle.Attacking, 0), defending);

        Assert.True(attackingChance > balancedChance);
    }
}
