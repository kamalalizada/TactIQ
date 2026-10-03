using TactIQ.Application.Transfers;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class AiTransferPolicyTests
{
    /// <summary>A squad with one outstanding player and a spread of others.</summary>
    private static Team Club(string name = "Rəqib")
    {
        var team = new Team { Id = Guid.NewGuid(), Name = name, Budget = 50_000_000m };

        void Add(Position position, int count, int from)
        {
            for (var i = 0; i < count; i++)
                team.Players.Add(new Player
                {
                    Id = Guid.NewGuid(),
                    TeamId = team.Id,
                    Name = $"{position}{i}",
                    Position = position,
                    Age = 25,
                    Overall = from - i * 3
                });
        }

        Add(Position.GK, 3, 76);
        Add(Position.DEF, 7, 78);
        Add(Position.MID, 6, 80);
        Add(Position.FWD, 5, 88);

        return team;
    }

    private static Player Star(Team club) => club.Players.MaxBy(p => p.Overall)!;
    private static Player Reserve(Team club) => club.Players.MinBy(p => p.Overall)!;

    [Fact]
    public void AClubWantsFarMoreForItsBestPlayerThanHeIsWorthOnPaper()
    {
        var club = Club();
        var star = Star(club);

        var asking = AiTransferPolicy.AskingPrice(star, club);

        Assert.True(asking > PlayerValuation.Of(star) * 1.5m);
    }

    [Fact]
    public void AReserveGoesForCloseToHisValue()
    {
        var club = Club();
        var spare = Reserve(club);

        var asking = AiTransferPolicy.AskingPrice(spare, club);

        Assert.True(asking <= PlayerValuation.Of(spare) * 1.1m);
    }

    [Fact]
    public void TheBestPlayerCostsMoreToPriseOutThanASquadMan()
    {
        var club = Club();

        var forStar = AiTransferPolicy.AskingPrice(Star(club), club)
                      / PlayerValuation.Of(Star(club));
        var forReserve = AiTransferPolicy.AskingPrice(Reserve(club), club)
                         / PlayerValuation.Of(Reserve(club));

        Assert.True(forStar > forReserve);
    }

    [Fact]
    public void MeetingTheAskingPriceBuysThePlayer()
    {
        var club = Club();
        var target = Reserve(club);
        var asking = AiTransferPolicy.AskingPrice(target, club);

        var verdict = AiTransferPolicy.ConsiderOffer(target, club, asking);

        Assert.True(verdict.Accepted);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void FallingShortDoesNot()
    {
        var club = Club();
        var target = Reserve(club);
        var asking = AiTransferPolicy.AskingPrice(target, club);

        var verdict = AiTransferPolicy.ConsiderOffer(target, club, asking - 100_000m);

        Assert.False(verdict.Accepted);
        Assert.NotNull(verdict.Reason);
    }

    [Fact]
    public void AClubWillNotSellWhatItCannotReplaceAtAnyPrice()
    {
        var club = Club();

        // Strip the keepers to the floor: now neither of them is for sale,
        // however much is put on the table.
        var spare = club.Players.Where(p => p.Position == Position.GK).Skip(2).ToList();
        foreach (var player in spare)
            club.Players.Remove(player);

        var keeper = club.Players.First(p => p.Position == Position.GK);
        var verdict = AiTransferPolicy.ConsiderOffer(keeper, club, 500_000_000m);

        Assert.False(verdict.Accepted);
    }

    [Fact]
    public void AClubBidsEnoughToBeTakenSeriouslyButNeverMoreThanItHas()
    {
        var seller = Club();
        var buyer = Club("Alıcı");
        var target = Reserve(seller);
        var random = new Random(7);

        for (var i = 0; i < 20; i++)
        {
            var bid = AiTransferPolicy.BidFor(target, seller, buyer, random);

            Assert.True(bid <= buyer.Budget);
            Assert.True(bid >= Math.Min(buyer.Budget, AiTransferPolicy.AskingPrice(target, seller)));
        }
    }

    [Fact]
    public void AClubOnlyWantsPlayersBetterThanWhatItAlreadyHas()
    {
        var buyer = Club("Alıcı");
        var bestForward = buyer.Players.Where(p => p.Position == Position.FWD).Max(p => p.Overall);

        var upgrade = new Player { Position = Position.FWD, Overall = bestForward + 3, Age = 25 };
        var spare = new Player { Position = Position.FWD, Overall = bestForward - 5, Age = 25 };

        Assert.True(AiTransferPolicy.Wants(upgrade, buyer));
        Assert.False(AiTransferPolicy.Wants(spare, buyer));
    }

    [Fact]
    public void AFullSquadStopsShopping()
    {
        var buyer = Club("Alıcı");

        while (buyer.Players.Count < SquadLimits.MaximumSquad)
            buyer.Players.Add(new Player { Id = Guid.NewGuid(), Position = Position.MID, Overall = 60 });

        var worldBeater = new Player { Position = Position.FWD, Overall = 99, Age = 24 };

        Assert.False(AiTransferPolicy.Wants(worldBeater, buyer));
        Assert.Null(AiTransferPolicy.WeakestLine(buyer));
    }

    [Fact]
    public void AClubShopsWhereItIsWeakest()
    {
        var club = Club();

        // The keepers are the thinnest part of this squad by some way.
        Assert.Equal(Position.GK, AiTransferPolicy.WeakestLine(club));
    }
}
