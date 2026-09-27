using TactIQ.Application.Cup;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class CupRulesTests
{
    private static Match Leg(Guid tieId, int leg, Guid homeId, Guid awayId, int homeGoals, int awayGoals) => new()
    {
        Id = Guid.NewGuid(),
        TieId = tieId,
        Leg = leg,
        Round = CupRound.QuarterFinal,
        HomeTeamId = homeId,
        AwayTeamId = awayId,
        HomeGoals = homeGoals,
        AwayGoals = awayGoals,
        IsPlayed = true
    };

    [Fact]
    public void DrawPairsEveryTeamExactlyOnce()
    {
        var teams = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToList();

        var ties = CupRules.Draw(teams, CupRound.QuarterFinal, new Random(1));

        Assert.Equal(4, ties.Count);

        var appearing = ties
            .SelectMany(t => new[] { t.FirstLegHomeTeamId, t.FirstLegAwayTeamId })
            .ToList();

        Assert.Equal(teams.Count, appearing.Distinct().Count());
        Assert.All(ties, t => Assert.NotEqual(t.FirstLegHomeTeamId, t.FirstLegAwayTeamId));
    }

    [Fact]
    public void DrawRejectsAnOddNumberOfTeams()
    {
        var teams = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        Assert.Throws<ArgumentException>(() => CupRules.Draw(teams, CupRound.SemiFinal, new Random(1)));
    }

    [Fact]
    public void RoundsProgressToTheFinalThenStop()
    {
        Assert.Equal(CupRound.SemiFinal, CupRules.NextRound(CupRound.QuarterFinal));
        Assert.Equal(CupRound.Final, CupRules.NextRound(CupRound.SemiFinal));
        Assert.Null(CupRules.NextRound(CupRound.Final));
    }

    [Fact]
    public void OnlyTheFinalIsASingleMatch()
    {
        Assert.True(CupRules.IsTwoLegged(CupRound.QuarterFinal));
        Assert.True(CupRules.IsTwoLegged(CupRound.SemiFinal));
        Assert.False(CupRules.IsTwoLegged(CupRound.Final));
    }

    [Fact]
    public void AggregateDecidesTheTie()
    {
        var tieId = Guid.NewGuid();
        var alpha = Guid.NewGuid();
        var beta = Guid.NewGuid();

        // Alpha 1-2, then Beta 0-3 Alpha: Alpha win 4-2 on aggregate.
        var outcome = CupRules.Resolve(
        [
            Leg(tieId, 1, alpha, beta, 1, 2),
            Leg(tieId, 2, beta, alpha, 0, 3)
        ]);

        Assert.Equal(alpha, outcome.WinnerTeamId);
        Assert.False(outcome.DecidedOnPenalties);
        Assert.Equal(4, outcome.HomeAggregate);
        Assert.Equal(2, outcome.AwayAggregate);
    }

    [Fact]
    public void AwayLegGoalsAreCountedForTheRightSide()
    {
        var tieId = Guid.NewGuid();
        var alpha = Guid.NewGuid();
        var beta = Guid.NewGuid();

        // Beta win 5-1 on aggregate despite losing the first leg.
        var outcome = CupRules.Resolve(
        [
            Leg(tieId, 1, alpha, beta, 1, 0),
            Leg(tieId, 2, beta, alpha, 5, 0)
        ]);

        Assert.Equal(beta, outcome.WinnerTeamId);
        Assert.Equal(1, outcome.HomeAggregate);
        Assert.Equal(5, outcome.AwayAggregate);
    }

    [Fact]
    public void PenaltiesSettleALevelTie()
    {
        var tieId = Guid.NewGuid();
        var alpha = Guid.NewGuid();
        var beta = Guid.NewGuid();

        var secondLeg = Leg(tieId, 2, beta, alpha, 1, 1);
        secondLeg.HomePenalties = 4;
        secondLeg.AwayPenalties = 2;

        var outcome = CupRules.Resolve([Leg(tieId, 1, alpha, beta, 0, 0), secondLeg]);

        // Beta hosted the second leg, so the home shoot-out score is theirs.
        Assert.Equal(beta, outcome.WinnerTeamId);
        Assert.True(outcome.DecidedOnPenalties);
    }

    [Fact]
    public void ALevelTieWithoutPenaltiesIsRejected()
    {
        var tieId = Guid.NewGuid();
        var alpha = Guid.NewGuid();
        var beta = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => CupRules.Resolve(
        [
            Leg(tieId, 1, alpha, beta, 1, 1),
            Leg(tieId, 2, beta, alpha, 0, 0)
        ]));
    }

    [Fact]
    public void ASingleLeggedFinalIsResolvedOnItsOwnScore()
    {
        var tieId = Guid.NewGuid();
        var alpha = Guid.NewGuid();
        var beta = Guid.NewGuid();

        var outcome = CupRules.Resolve([Leg(tieId, 1, alpha, beta, 2, 1)]);

        Assert.Equal(alpha, outcome.WinnerTeamId);
        Assert.False(outcome.DecidedOnPenalties);
    }

    [Fact]
    public void ShootoutAlwaysProducesAWinner()
    {
        var home = TestTeamFactory.CreateWithBench("Home", 75);
        var away = TestTeamFactory.CreateWithBench("Away", 75);

        for (var seed = 0; seed < 200; seed++)
        {
            var (h, a) = CupRules.TakePenalties(home, away, new Random(seed));
            Assert.NotEqual(h, a);
            Assert.InRange(h, 0, 30);
            Assert.InRange(a, 0, 30);
        }
    }

    [Fact]
    public void ShootoutScoresLookLikeRealPenalties()
    {
        var home = TestTeamFactory.CreateWithBench("Home", 78);
        var away = TestTeamFactory.CreateWithBench("Away", 78);

        var totals = Enumerable.Range(0, 300)
            .Select(seed => CupRules.TakePenalties(home, away, new Random(seed)))
            .ToList();

        // Most shoot-outs end inside the first five kicks each.
        var normalLength = totals.Count(t => t.Home <= 6 && t.Away <= 6);
        Assert.True(normalLength > totals.Count * 0.7, $"only {normalLength}/{totals.Count} were normal length");
    }
}
