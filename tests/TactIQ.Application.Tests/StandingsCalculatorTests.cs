using TactIQ.Application.Standings;
using TactIQ.Domain.Entities;

namespace TactIQ.Application.Tests;

public class StandingsCalculatorTests
{
    private readonly StandingsCalculator _calculator = new();

    private static Match Played(Team home, Team away, int homeGoals, int awayGoals) => new()
    {
        Id = Guid.NewGuid(),
        HomeTeamId = home.Id,
        AwayTeamId = away.Id,
        HomeGoals = homeGoals,
        AwayGoals = awayGoals,
        IsPlayed = true
    };

    [Fact]
    public void AwardsThreePointsForWinAndOneForDraw()
    {
        var a = TestTeamFactory.Create("Alpha", 70);
        var b = TestTeamFactory.Create("Beta", 70);

        var rows = _calculator.Calculate([a, b], [Played(a, b, 2, 1), Played(b, a, 1, 1)]);

        var alpha = rows.Single(r => r.TeamId == a.Id);
        var beta = rows.Single(r => r.TeamId == b.Id);

        Assert.Equal(4, alpha.Points);
        Assert.Equal(1, beta.Points);
        Assert.Equal(2, alpha.Played);
    }

    [Fact]
    public void IgnoresUnplayedMatches()
    {
        var a = TestTeamFactory.Create("Alpha", 70);
        var b = TestTeamFactory.Create("Beta", 70);

        var scheduled = new Match { HomeTeamId = a.Id, AwayTeamId = b.Id, IsPlayed = false };
        var rows = _calculator.Calculate([a, b], [scheduled]);

        Assert.All(rows, r => Assert.Equal(0, r.Played));
    }

    [Fact]
    public void AccumulatesGoalsFromBothHomeAndAwayFixtures()
    {
        var a = TestTeamFactory.Create("Alpha", 70);
        var b = TestTeamFactory.Create("Beta", 70);

        var rows = _calculator.Calculate([a, b], [Played(a, b, 3, 0), Played(b, a, 2, 1)]);
        var alpha = rows.Single(r => r.TeamId == a.Id);

        Assert.Equal(4, alpha.GoalsFor);
        Assert.Equal(2, alpha.GoalsAgainst);
        Assert.Equal(2, alpha.GoalDifference);
    }

    [Fact]
    public void BreaksTiesByGoalDifferenceThenGoalsForThenName()
    {
        var byGd = TestTeamFactory.Create("Zulu", 70);
        var byGf = TestTeamFactory.Create("Yankee", 70);
        var filler = TestTeamFactory.Create("Filler", 70);

        // Both win once: Zulu 5-0 (GD +5), Yankee 2-0 (GD +2).
        var rows = _calculator.Calculate(
            [byGd, byGf, filler],
            [Played(byGd, filler, 5, 0), Played(byGf, filler, 2, 0)]);

        Assert.Equal(byGd.Id, rows[0].TeamId);
        Assert.Equal(byGf.Id, rows[1].TeamId);
    }

    [Fact]
    public void FallsBackToAlphabeticalOrderWhenFullyTied()
    {
        var zulu = TestTeamFactory.Create("Zulu", 70);
        var alpha = TestTeamFactory.Create("Alpha", 70);

        var rows = _calculator.Calculate([zulu, alpha], []);

        Assert.Equal("Alpha", rows[0].TeamName);
        Assert.Equal(1, rows[0].Rank);
        Assert.Equal(2, rows[1].Rank);
    }

    [Fact]
    public void RanksAreSequentialStartingAtOne()
    {
        var teams = Enumerable.Range(0, 8)
            .Select(i => TestTeamFactory.Create($"Team{i}", 70))
            .ToList();

        var rows = _calculator.Calculate(teams, []);

        Assert.Equal(Enumerable.Range(1, 8), rows.Select(r => r.Rank));
    }
}
