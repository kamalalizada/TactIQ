using TactIQ.Application.Finances;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class PrizeMoneyTests
{
    [Fact]
    public void WinningPaysMoreThanDrawingAndLosingPaysNothing()
    {
        var win = PrizeMoney.ForResult("England", 2, 1);
        var draw = PrizeMoney.ForResult("England", 1, 1);
        var loss = PrizeMoney.ForResult("England", 0, 3);

        Assert.True(win > draw);
        Assert.True(draw > 0);
        Assert.Equal(0m, loss);
    }

    [Fact]
    public void TheRicherLeaguePaysMore()
    {
        // Television money is what separates the leagues, so the same result is
        // worth several times as much in England as in Turkey.
        var england = PrizeMoney.ForResult("England", 1, 0);
        var spain = PrizeMoney.ForResult("Spain", 1, 0);
        var turkey = PrizeMoney.ForResult("Turkey", 1, 0);

        Assert.True(england > spain);
        Assert.True(spain > turkey);
    }

    [Fact]
    public void AnUnknownLeagueStillPaysSomething()
    {
        Assert.True(PrizeMoney.ForResult("Atlantis", 1, 0) > 0);
        Assert.True(PrizeMoney.ForLeaguePosition("Atlantis", 1, 20) > 0);
    }

    [Fact]
    public void FinishingHigherPaysMoreAllTheWayDownTheTable()
    {
        var prizes = Enumerable
            .Range(1, 20)
            .Select(place => PrizeMoney.ForLeaguePosition("England", place, 20))
            .ToList();

        // Every place is worth more than the one below it, so a club in
        // mid-table is still playing for something.
        for (var i = 1; i < prizes.Count; i++)
            Assert.True(prizes[i] < prizes[i - 1], $"Place {i + 1} paid no less than place {i}.");

        Assert.True(prizes[^1] > 0);
    }

    [Fact]
    public void WinningTheLeagueIsWorthManyTimesFinishingLast()
    {
        var champion = PrizeMoney.ForLeaguePosition("England", 1, 20);
        var bottom = PrizeMoney.ForLeaguePosition("England", 20, 20);

        Assert.True(champion > bottom * 5);
    }

    [Fact]
    public void APositionOutsideTheTableIsStillAnswered()
    {
        // Nothing should throw if a table comes back short or a rank is odd.
        Assert.True(PrizeMoney.ForLeaguePosition("Spain", 0, 20) > 0);
        Assert.True(PrizeMoney.ForLeaguePosition("Spain", 99, 20) > 0);
        Assert.Equal(0m, PrizeMoney.ForLeaguePosition("Spain", 1, 1));
    }

    [Fact]
    public void EachRoundOfTheCupIsWorthMoreThanTheLast()
    {
        var group = PrizeMoney.ForCupRound(CupRound.GroupStage);
        var last16 = PrizeMoney.ForCupRound(CupRound.RoundOf16);
        var quarter = PrizeMoney.ForCupRound(CupRound.QuarterFinal);
        var semi = PrizeMoney.ForCupRound(CupRound.SemiFinal);
        var final = PrizeMoney.ForCupRound(CupRound.Final);

        Assert.True(quarter > last16);
        Assert.True(semi > quarter);
        Assert.True(final > semi);
        Assert.True(group > 0);
    }

    [Fact]
    public void WinningTheFinalPaysMoreThanLosingIt()
    {
        var won = PrizeMoney.ForCupRound(CupRound.Final, won: true);
        var lost = PrizeMoney.ForCupRound(CupRound.Final, won: false);

        Assert.True(won > lost);
    }

    [Fact]
    public void ACupRunIsWorthEverythingBankedAlongTheWay()
    {
        var knockedOutEarly = PrizeMoney.ForCupRun(CupRound.GroupStage, won: false);
        var semiFinalist = PrizeMoney.ForCupRun(CupRound.SemiFinal, won: false);
        var winner = PrizeMoney.ForCupRun(CupRound.Final, won: true);

        Assert.True(semiFinalist > knockedOutEarly);
        Assert.True(winner > semiFinalist);
    }

    [Fact]
    public void EuropeIsWorthMoreThanWinningADomesticLeague()
    {
        // This is why a club chases a European place: the run pays better than
        // the title that qualified it.
        var europeanRun = PrizeMoney.ForCupRun(CupRound.Final, won: true);
        var premierLeagueTitle = PrizeMoney.ForLeaguePosition("England", 1, 20);

        Assert.True(europeanRun > premierLeagueTitle);
    }

    [Fact]
    public void PrizesAreQuotedInRoundFigures()
    {
        foreach (var place in new[] { 1, 7, 13, 20 })
        {
            var prize = PrizeMoney.ForLeaguePosition("England", place, 20);
            Assert.Equal(0m, prize % 100_000m);
        }
    }

    [Fact]
    public void ASeasonOfResultsIsWorthLessThanWhereYouFinish()
    {
        // Winning every match of a 38-week league still earns less than the
        // end-of-season purse, so the table is what a club plays for.
        var perfectSeason = PrizeMoney.ForResult("England", 1, 0) * 38;
        var title = PrizeMoney.ForLeaguePosition("England", 1, 20);

        Assert.True(title > perfectSeason);
    }
}
