using TactIQ.Application.Scheduling;

namespace TactIQ.Application.Tests;

public class RoundRobinScheduleGeneratorTests
{
    private readonly RoundRobinScheduleGenerator _generator = new();

    private static List<Guid> Teams(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    [Theory]
    [InlineData(8, 14, 56)]
    [InlineData(20, 38, 380)]
    [InlineData(4, 6, 12)]
    public void GeneratesCorrectWeekAndMatchCounts(int teamCount, int expectedWeeks, int expectedMatches)
    {
        var fixtures = _generator.Generate(Teams(teamCount));

        Assert.Equal(expectedMatches, fixtures.Count);
        Assert.Equal(expectedWeeks, fixtures.Select(f => f.WeekNumber).Distinct().Count());
        Assert.All(
            fixtures.GroupBy(f => f.WeekNumber),
            week => Assert.Equal(teamCount / 2, week.Count()));
    }

    [Fact]
    public void EveryTeamPlaysExactlyOncePerWeek()
    {
        var teams = Teams(8);
        var fixtures = _generator.Generate(teams);

        foreach (var week in fixtures.GroupBy(f => f.WeekNumber))
        {
            var appearing = week.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToList();
            Assert.Equal(teams.Count, appearing.Distinct().Count());
        }
    }

    [Fact]
    public void EveryPairMeetsTwiceOnceHomeAndOnceAway()
    {
        var teams = Teams(8);
        var fixtures = _generator.Generate(teams);

        var pairings = fixtures
            .GroupBy(f => f.HomeTeamId.CompareTo(f.AwayTeamId) < 0
                ? (f.HomeTeamId, f.AwayTeamId)
                : (f.AwayTeamId, f.HomeTeamId))
            .ToList();

        Assert.Equal(teams.Count * (teams.Count - 1) / 2, pairings.Count);
        Assert.All(pairings, pair =>
        {
            Assert.Equal(2, pair.Count());
            Assert.Equal(2, pair.Select(f => f.HomeTeamId).Distinct().Count());
        });
    }

    [Fact]
    public void SecondHalfMirrorsFirstHalfWithVenuesSwapped()
    {
        var fixtures = _generator.Generate(Teams(8));

        var firstHalf = fixtures.Where(f => f.WeekNumber <= 7)
            .Select(f => (f.WeekNumber, f.HomeTeamId, f.AwayTeamId));
        var secondHalfMirrored = fixtures.Where(f => f.WeekNumber > 7)
            .Select(f => (WeekNumber: f.WeekNumber - 7, HomeTeamId: f.AwayTeamId, AwayTeamId: f.HomeTeamId));

        Assert.Equal(firstHalf.OrderBy(x => x).ToList(), secondHalfMirrored.OrderBy(x => x).ToList());
    }

    [Fact]
    public void EachTeamHasBalancedHomeAndAwayGames()
    {
        var teams = Teams(8);
        var fixtures = _generator.Generate(teams);

        foreach (var team in teams)
        {
            var home = fixtures.Count(f => f.HomeTeamId == team);
            var away = fixtures.Count(f => f.AwayTeamId == team);
            Assert.Equal(home, away);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void RejectsInvalidTeamCounts(int teamCount)
    {
        Assert.Throws<ArgumentException>(() => _generator.Generate(Teams(teamCount)));
    }
}
