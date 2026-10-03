using TactIQ.Application.Seeding;
using TactIQ.Application.Transfers;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class TransferWindowTests
{
    private const int LeagueWeeks = 38;

    [Fact]
    public void TheMarketIsOpenBeforeASeasonStarts()
    {
        Assert.Equal(TransferWindow.Summer, TransferWindowRules.At(0, LeagueWeeks));
        Assert.Equal(TransferWindow.Summer, TransferWindowRules.At(1, LeagueWeeks));
    }

    [Fact]
    public void TheMarketShutsOnceTheSeasonIsUnderWay()
    {
        Assert.Equal(TransferWindow.Closed, TransferWindowRules.At(2, LeagueWeeks));
        Assert.Equal(TransferWindow.Closed, TransferWindowRules.At(10, LeagueWeeks));
    }

    [Fact]
    public void ItOpensAgainAtTheHalfwayPoint()
    {
        var opens = TransferWindowRules.WinterOpensAt(LeagueWeeks);

        Assert.Equal(TransferWindow.Winter, TransferWindowRules.At(opens, LeagueWeeks));
        Assert.Equal(TransferWindow.Closed, TransferWindowRules.At(opens - 1, LeagueWeeks));
        Assert.Equal(
            TransferWindow.Closed,
            TransferWindowRules.At(opens + TransferWindowRules.WinterLength, LeagueWeeks));
    }

    [Fact]
    public void AManagerIsToldWhenTheWindowCloses()
    {
        Assert.Equal(TransferWindowRules.SummerLastWeek, TransferWindowRules.ClosesAfter(0, LeagueWeeks));
        Assert.Null(TransferWindowRules.ClosesAfter(10, LeagueWeeks));
    }

    [Fact]
    public void AManagerIsToldWhenItOpensAgain()
    {
        var winter = TransferWindowRules.WinterOpensAt(LeagueWeeks);

        Assert.Equal(winter, TransferWindowRules.OpensAt(10, LeagueWeeks));

        // Once winter has been and gone, the squad is settled for the season.
        Assert.Null(TransferWindowRules.OpensAt(LeagueWeeks - 1, LeagueWeeks));

        // While a window is open there is nothing to wait for.
        Assert.Null(TransferWindowRules.OpensAt(0, LeagueWeeks));
    }

    [Fact]
    public void EveryWeekOfASeasonHasAnAnswer()
    {
        // The calendar is read on every dashboard, so no week may fall through
        // it — and the two windows must not run into each other.
        var open = 0;

        for (var week = 0; week <= LeagueWeeks; week++)
        {
            var window = TransferWindowRules.At(week, LeagueWeeks);
            if (window != TransferWindow.Closed)
                open++;
        }

        Assert.Equal(TransferWindowRules.SummerLastWeek + 1 + TransferWindowRules.WinterLength, open);
    }
}

public class SquadLimitsTests
{
    private static Team Club(int keepers = 2, int defenders = 6, int midfielders = 5, int forwards = 6)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = "Test" };

        void Add(Position position, int count)
        {
            for (var i = 0; i < count; i++)
                team.Players.Add(new Player
                {
                    Id = Guid.NewGuid(),
                    TeamId = team.Id,
                    Name = $"{position}{i}",
                    Position = position,
                    Overall = 75,
                    Age = 25
                });
        }

        Add(Position.GK, keepers);
        Add(Position.DEF, defenders);
        Add(Position.MID, midfielders);
        Add(Position.FWD, forwards);

        return team;
    }

    [Fact]
    public void AFullSquadMaySellASpareForward()
    {
        var club = Club();
        var spare = club.Players.Last(p => p.Position == Position.FWD);

        Assert.Null(SquadLimits.WhyCannotSell(club, spare));
    }

    [Fact]
    public void AClubWillNotSellItsSecondKeeper()
    {
        var club = Club();
        var keeper = club.Players.First(p => p.Position == Position.GK);

        // Two is the floor, so neither of them can go.
        Assert.NotNull(SquadLimits.WhyCannotSell(club, keeper));
    }

    [Fact]
    public void AClubStrippedToTheBoneCannotSellAtAll()
    {
        var club = Club(keepers: 2, defenders: 5, midfielders: 5, forwards: 4);

        while (club.Players.Count > SquadLimits.MinimumSquad)
            club.Players.RemoveAt(club.Players.Count - 1);

        Assert.All(club.Players, player => Assert.NotNull(SquadLimits.WhyCannotSell(club, player)));
    }

    [Fact]
    public void AClubStopsBuyingOnceItsSquadIsFull()
    {
        var club = Club();
        Assert.Null(SquadLimits.WhyCannotBuy(club));

        while (club.Players.Count < SquadLimits.MaximumSquad)
            club.Players.Add(new Player { Id = Guid.NewGuid(), Position = Position.MID });

        Assert.NotNull(SquadLimits.WhyCannotBuy(club));
    }

    [Fact]
    public void EverySeededClubCanDoSomeBusinessWithoutBreakingItsSquad()
    {
        // A club that could not sell a single player would sit out the window
        // entirely, so the floors are checked against the squads as seeded.
        var factory = new TeamFactory(new Random(4242));
        var teams = factory.CreateTeams(Guid.NewGuid(), 58);

        Assert.All(teams, team =>
            Assert.Contains(team.Players, player => SquadLimits.CanSell(team, player)));
    }

    [Fact]
    public void EverySeededClubStartsWithMoneyToSpend()
    {
        var factory = new TeamFactory(new Random(4242));

        Assert.All(factory.CreateTeams(Guid.NewGuid(), 58), team =>
            Assert.True(team.Budget >= 2_000_000m, $"{team.Name} has no budget."));
    }
}
