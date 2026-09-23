using TactIQ.Application.Simulation;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

/// <summary>Guards the feel of the simulation: scorelines, cards and injuries all
/// have to land in ranges a football fan would recognise.</summary>
public class EngineBalanceTests
{
    private const int Sample = 400;

    private static List<MatchSimulationResult> PlaySeason(int homeLevel = 75, int awayLevel = 75)
    {
        var engine = new MatchEngine(new Random(4242));
        return Enumerable.Range(0, Sample)
            .Select(_ => engine.SimulateInstant(
                TestTeamFactory.CreateWithBench("Home", homeLevel),
                TestTeamFactory.CreateWithBench("Away", awayLevel)))
            .ToList();
    }

    [Fact]
    public void AverageGoalsPerMatchLooksLikeRealFootball()
    {
        var results = PlaySeason();
        var average = results.Average(r => r.HomeGoals + r.AwayGoals);

        Assert.InRange(average, 2.0, 3.6);
    }

    [Fact]
    public void ScorelinesStayWithinPlausibleBounds()
    {
        var results = PlaySeason();

        Assert.All(results, r => Assert.InRange(r.HomeGoals, 0, 9));
        Assert.All(results, r => Assert.InRange(r.AwayGoals, 0, 9));

        // Blowouts should be rare, not routine.
        var blowouts = results.Count(r => Math.Abs(r.HomeGoals - r.AwayGoals) >= 5);
        Assert.True(blowouts < Sample * 0.05, $"{blowouts} blowouts in {Sample} matches");
    }

    [Fact]
    public void HomeSideWinsMoreOftenThanItLoses()
    {
        var results = PlaySeason();

        var homeWins = results.Count(r => r.HomeGoals > r.AwayGoals);
        var awayWins = results.Count(r => r.AwayGoals > r.HomeGoals);

        Assert.True(homeWins > awayWins, $"home={homeWins}, away={awayWins}");
    }

    [Fact]
    public void DrawsHappenOftenEnoughToMatter()
    {
        var results = PlaySeason();
        var draws = results.Count(r => r.HomeGoals == r.AwayGoals);

        Assert.InRange(draws / (double)Sample, 0.12, 0.40);
    }

    [Fact]
    public void CardsAreIssuedAtASensibleRate()
    {
        var results = PlaySeason();

        var yellows = results.Average(r => r.Events.Count(e => e.EventType == MatchEventType.YellowCard));
        var reds = results.Average(r => r.Events.Count(e => e.EventType == MatchEventType.RedCard));

        Assert.InRange(yellows, 0.8, 6.0);
        Assert.InRange(reds, 0.0, 0.5);
    }

    [Fact]
    public void InjuriesAreUncommon()
    {
        var results = PlaySeason();
        var withInjury = results.Count(r => r.Events.Any(e => e.EventType == MatchEventType.Injury));

        Assert.InRange(withInjury / (double)Sample, 0.0, 0.25);
    }

    [Fact]
    public void GoalsAreCreditedToPlayersWhoCouldPlausiblyScore()
    {
        var engine = new MatchEngine(new Random(11));
        var home = TestTeamFactory.CreateWithBench("Home", 78);
        var away = TestTeamFactory.CreateWithBench("Away", 74);

        var forwardOrMidfieldGoals = 0;
        var total = 0;

        for (var i = 0; i < 120; i++)
        {
            foreach (var e in engine.SimulateInstant(home, away).Events
                         .Where(e => e.EventType == MatchEventType.Goal))
            {
                total++;
                var scorer = home.Players.Concat(away.Players).First(p => p.Id == e.PlayerId);
                if (scorer.Position is Position.FWD or Position.MID)
                    forwardOrMidfieldGoals++;
            }
        }

        Assert.True(total > 0, "no goals were scored at all");
        Assert.True(forwardOrMidfieldGoals / (double)total > 0.75,
            $"only {forwardOrMidfieldGoals}/{total} goals came from attacking players");
    }

    [Fact]
    public void PlayersFinishMatchesTired()
    {
        var engine = new MatchEngine(new Random(5));
        var result = engine.SimulateInstant(
            TestTeamFactory.CreateWithBench("Home", 75),
            TestTeamFactory.CreateWithBench("Away", 75));

        var starters = result.Aftermath.Where(a => a.Condition < 100).ToList();

        Assert.NotEmpty(starters);
        Assert.All(starters, a => Assert.InRange(a.Condition, 20, 99));
    }

    [Fact]
    public void StrongerSquadWinsTheLongRun()
    {
        var results = PlaySeason(homeLevel: 86, awayLevel: 64);

        var homeWins = results.Count(r => r.HomeGoals > r.AwayGoals);
        Assert.True(homeWins > Sample * 0.6, $"the better side won only {homeWins}/{Sample}");
    }
}
