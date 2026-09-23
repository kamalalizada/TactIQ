using TactIQ.Application.Statistics;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class StatisticsCalculatorTests
{
    private readonly StatisticsCalculator _calculator = new();

    private static Match Played(Team home, Team away, int week, int homeGoals, int awayGoals, params MatchEvent[] events)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            WeekNumber = week,
            HomeTeamId = home.Id,
            HomeTeam = home,
            AwayTeamId = away.Id,
            AwayTeam = away,
            HomeGoals = homeGoals,
            AwayGoals = awayGoals,
            IsPlayed = true
        };

        match.Events.AddRange(events);
        return match;
    }

    private static MatchEvent Goal(Team team, Player scorer, Player? assist = null, int minute = 30) => new()
    {
        Minute = minute,
        EventType = MatchEventType.Goal,
        TeamId = team.Id,
        PlayerId = scorer.Id,
        AssistPlayerId = assist?.Id
    };

    private static MatchEvent Card(Team team, Player player, MatchEventType type, int minute = 40) => new()
    {
        Minute = minute,
        EventType = type,
        TeamId = team.Id,
        PlayerId = player.Id
    };

    [Fact]
    public void RanksScorersByGoals()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);

        var star = a.Players.First(p => p.Position == Position.FWD);
        var other = b.Players.First(p => p.Position == Position.FWD);

        var matches = new[]
        {
            Played(a, b, 1, 2, 1, Goal(a, star), Goal(a, star), Goal(b, other)),
            Played(b, a, 2, 0, 1, Goal(a, star))
        };

        var stats = _calculator.Calculate([a, b], matches);

        Assert.Equal(star.Id, stats.TopScorers[0].PlayerId);
        Assert.Equal(3, stats.TopScorers[0].Goals);
        Assert.Equal(1, stats.TopScorers[0].Rank);
        Assert.Equal(other.Id, stats.TopScorers[1].PlayerId);
    }

    [Fact]
    public void CreditsAssistsSeparatelyFromGoals()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);

        var scorer = a.Players.First(p => p.Position == Position.FWD);
        var creator = a.Players.First(p => p.Position == Position.MID);

        var stats = _calculator.Calculate([a, b],
            [Played(a, b, 1, 1, 0, Goal(a, scorer, creator))]);

        Assert.Equal(creator.Id, stats.TopAssists[0].PlayerId);
        Assert.Equal(1, stats.TopAssists[0].Assists);
        Assert.DoesNotContain(stats.TopAssists, r => r.PlayerId == scorer.Id);
    }

    [Fact]
    public void RanksDisciplineWithRedCardsWeighedHeavier()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);

        var sentOff = a.Players[1];
        var repeatedlyBooked = b.Players[1];

        var stats = _calculator.Calculate([a, b],
        [
            Played(a, b, 1, 0, 0,
                Card(a, sentOff, MatchEventType.RedCard),
                Card(b, repeatedlyBooked, MatchEventType.YellowCard),
                Card(b, repeatedlyBooked, MatchEventType.YellowCard))
        ]);

        Assert.Equal(sentOff.Id, stats.MostBooked[0].PlayerId);
        Assert.Equal(1, stats.MostBooked[0].RedCards);
        Assert.Equal(2, stats.MostBooked[1].YellowCards);
    }

    [Fact]
    public void FormListsMostRecentResultFirst()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);

        var stats = _calculator.Calculate([a, b],
        [
            Played(a, b, 1, 3, 0),   // Alpha win
            Played(b, a, 2, 1, 1),   // draw
            Played(a, b, 3, 0, 2)    // Alpha loss
        ]);

        var alpha = stats.Form.Single(f => f.TeamId == a.Id);
        Assert.Equal(["L", "D", "W"], alpha.Form);
        Assert.Equal(4, alpha.PointsFromForm);
    }

    [Fact]
    public void FormIsCappedAtTheRequestedLength()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);

        var matches = Enumerable.Range(1, 8)
            .Select(week => Played(a, b, week, 1, 0))
            .ToList();

        var stats = _calculator.Calculate([a, b], matches, formLength: 3);

        Assert.Equal(3, stats.Form.Single(f => f.TeamId == a.Id).Form.Count);
    }

    [Fact]
    public void SummarisesLeagueTotals()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);

        var stats = _calculator.Calculate([a, b],
        [
            Played(a, b, 1, 3, 0),
            Played(b, a, 2, 1, 1)
        ]);

        Assert.Equal(2, stats.MatchesPlayed);
        Assert.Equal(5, stats.TotalGoals);
        Assert.Equal(2.5, stats.GoalsPerMatch);
        Assert.Equal("Alpha 3-0 Beta", stats.BiggestWin);
    }

    [Fact]
    public void IgnoresMatchesThatHaveNotBeenPlayed()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);

        var scheduled = new Match
        {
            WeekNumber = 1,
            HomeTeamId = a.Id,
            HomeTeam = a,
            AwayTeamId = b.Id,
            AwayTeam = b,
            IsPlayed = false
        };

        var stats = _calculator.Calculate([a, b], [scheduled]);

        Assert.Equal(0, stats.MatchesPlayed);
        Assert.Empty(stats.TopScorers);
        Assert.Null(stats.BiggestWin);
    }

    [Fact]
    public void HeadToHeadCountsFromEachTeamsPerspective()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);
        var c = TestTeamFactory.CreateWithBench("Gamma", 75);

        var summary = _calculator.HeadToHead(a, b,
        [
            Played(a, b, 1, 2, 0),   // Alpha win at home
            Played(b, a, 2, 3, 1),   // Beta win at home
            Played(b, a, 3, 1, 1),   // draw
            Played(a, c, 4, 5, 0)    // not between these two
        ]);

        Assert.Equal(1, summary.TeamAWins);
        Assert.Equal(1, summary.TeamBWins);
        Assert.Equal(1, summary.Draws);
        Assert.Equal(4, summary.TeamAGoals);
        Assert.Equal(4, summary.TeamBGoals);
        Assert.Equal(3, summary.Matches.Count);
    }

    [Fact]
    public void HeadToHeadIsEmptyWhenTheyHaveNotMet()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);

        var summary = _calculator.HeadToHead(a, b, []);

        Assert.Empty(summary.Matches);
        Assert.Equal(0, summary.TeamAWins + summary.TeamBWins + summary.Draws);
    }

    [Fact]
    public void ChartSizeLimitsHowManyPlayersAreReturned()
    {
        var a = TestTeamFactory.CreateWithBench("Alpha", 75);
        var b = TestTeamFactory.CreateWithBench("Beta", 75);

        var goals = a.Players.Take(6)
            .Select(p => Goal(a, p))
            .ToArray();

        var stats = _calculator.Calculate([a, b],
            [Played(a, b, 1, 6, 0, goals)], chartSize: 3);

        Assert.Equal(3, stats.TopScorers.Count);
    }
}
