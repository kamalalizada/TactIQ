using TactIQ.Application.Seeding;
using TactIQ.Application.Transfers;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class PlayerValuationTests
{
    [Fact]
    public void BetterPlayersCostMore()
    {
        var good = PlayerValuation.Of(80, 25, Position.MID);
        var veryGood = PlayerValuation.Of(85, 25, Position.MID);
        var worldClass = PlayerValuation.Of(90, 25, Position.MID);

        Assert.True(good < veryGood);
        Assert.True(veryGood < worldClass);
    }

    [Fact]
    public void TheGapAtTheTopCostsFarMoreThanTheGapLowerDown()
    {
        // Five rating points between two ordinary players is a modest sum;
        // the same five between two of the best is a club-record fee, because
        // there are only a handful of players at that level to buy.
        var lowerDown = PlayerValuation.Of(75, 25, Position.MID)
                        - PlayerValuation.Of(70, 25, Position.MID);

        var atTheTop = PlayerValuation.Of(90, 25, Position.MID)
                       - PlayerValuation.Of(85, 25, Position.MID);

        Assert.True(atTheTop > lowerDown * 10);
    }

    [Fact]
    public void AYoungPlayerIsWorthMoreThanAnOlderOneOfTheSameAbility()
    {
        var prospect = PlayerValuation.Of(85, 21, Position.FWD);
        var inHisPrime = PlayerValuation.Of(85, 26, Position.FWD);
        var veteran = PlayerValuation.Of(85, 34, Position.FWD);

        Assert.True(prospect > inHisPrime);
        Assert.True(inHisPrime > veteran);

        // A player at the end of his career is a fraction of his prime value:
        // a buying club is paying for the seasons still to come.
        Assert.True(veteran < inHisPrime / 4);
    }

    [Fact]
    public void GoalsCostMoreThanGoalkeeping()
    {
        var keeper = PlayerValuation.Of(85, 25, Position.GK);
        var defender = PlayerValuation.Of(85, 25, Position.DEF);
        var forward = PlayerValuation.Of(85, 25, Position.FWD);

        Assert.True(keeper < defender);
        Assert.True(defender < forward);
    }

    [Fact]
    public void NobodyIsWorthlessAndNobodyIsPricedBeyondReach()
    {
        var worst = PlayerValuation.Of(40, 38, Position.GK);
        var best = PlayerValuation.Of(99, 24, Position.FWD);

        Assert.True(worst > 0);
        Assert.True(best <= 200_000_000m);
    }

    [Fact]
    public void PricesAreQuotedInRoundNumbers()
    {
        // A fee is reported as "€27.7M", never to the euro, so the stored
        // figure is round enough to print without hiding anything.
        foreach (var overall in new[] { 62, 74, 81, 88, 93 })
        {
            var value = PlayerValuation.Of(overall, 26, Position.MID);
            Assert.Equal(0m, value % 100_000m);
        }
    }

    [Fact]
    public void EverySeededPlayerHasASensiblePrice()
    {
        // The whole game is priced from this one formula, so a squad full of
        // free players or a reserve worth nine figures would show up here.
        var players = SquadData.All.SelectMany(squad => squad.Players).ToList();

        Assert.All(players, player =>
        {
            var value = PlayerValuation.Of(
                player.Overall, player.Age, Enum.Parse<Position>(player.Position));

            Assert.InRange(value, 100_000m, 200_000_000m);
        });
    }

    [Fact]
    public void AStrongClubsSquadIsWorthMoreThanAWeakOnes()
    {
        var rich = Worth("Liverpool");
        var modest = Worth("Konyaspor");

        Assert.True(rich > modest * 5);

        static decimal Worth(string club) => SquadData.All
            .First(squad => squad.Name == club)
            .Players
            .Sum(p => PlayerValuation.Of(p.Overall, p.Age, Enum.Parse<Position>(p.Position)));
    }

    [Fact]
    public void APriceCeilingAllowsThroughEveryoneWhoFitsUnderIt()
    {
        // A search by price narrows on rating first, so the ceiling it works
        // out must not exclude anybody who is actually affordable — a budget
        // of five million has to find the players who cost five million.
        foreach (var ceiling in new[] { 1_000_000m, 5_000_000m, 25_000_000m, 90_000_000m })
        {
            var highest = PlayerValuation.HighestRatingUnder(ceiling);

            // Ratings run to 99, so a ceiling that affords the very best has
            // nobody above it to cut out.
            if (highest >= 99)
                continue;

            foreach (var age in new[] { 19, 25, 31, 37 })
            foreach (var position in Enum.GetValues<Position>())
            {
                var affordable = PlayerValuation.Of(highest + 1, age, position) <= ceiling;

                Assert.False(
                    affordable,
                    $"A {highest + 1} at {age} ({position}) costs under {ceiling} but was cut out.");
            }
        }
    }

    [Fact]
    public void ASmallBudgetStillFindsAMarket()
    {
        // The cheapest players in the game must come back for a club with
        // little to spend, or its market reads as empty.
        var ceiling = PlayerValuation.HighestRatingUnder(2_000_000m);

        Assert.True(ceiling > 55, "A two-million budget could not afford anybody.");
    }
}
