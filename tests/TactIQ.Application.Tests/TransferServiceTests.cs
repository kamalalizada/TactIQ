using TactIQ.Application.Transfers;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

/// <summary>
/// A season with two clubs: the one the user manages and one to do business
/// with. Both are full squads, so the selling limits never get in the way of
/// what a test is actually asking about.
/// </summary>
internal sealed class TransferWorld : IDisposable
{
    public const string UserId = "manager-1";
    private const int LeagueTeams = 20;

    public TestDbContext Db { get; }
    public TransferService Service { get; }
    public Season Season { get; }
    public Team Mine { get; }
    public Team Theirs { get; }

    public TransferWorld(int week = 0)
    {
        Db = TestDbContext.Create();

        Season = new Season
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Name = "Test",
            GameMode = GameMode.ManagerMode,
            CreatedAt = DateTime.UtcNow,
            CurrentWeek = week
        };

        Mine = Club("Mənim Klubum", budget: 100_000_000m, level: 78);
        Theirs = Club("Rəqib", budget: 20_000_000m, level: 74);
        Season.UserTeamId = Mine.Id;

        Db.Seasons.Add(Season);
        Db.Teams.AddRange(Mine, Theirs);
        Db.Competitions.Add(new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = Season.Id,
            Name = "Liqa",
            Country = "England",
            Kind = CompetitionKind.League,
            TeamCount = LeagueTeams,
            CurrentWeek = Math.Max(1, week)
        });
        Db.SaveChanges();

        Service = new TransferService(Db);
    }

    /// <summary>The league this world runs, which sets the window calendar.</summary>
    public static int TotalWeeks => 2 * (LeagueTeams - 1);

    public Player TheirPlayer(Position position = Position.FWD) =>
        Theirs.Players.First(p => p.Position == position);

    /// <summary>A squad big enough that any one player may be sold.</summary>
    private Team Club(string name, decimal budget, int level)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            SeasonId = Season.Id,
            Name = name,
            Country = "England",
            Budget = budget,
            Formation = "4-4-2"
        };

        var layout = new[]
        {
            (Position.GK, 3), (Position.DEF, 7), (Position.MID, 6), (Position.FWD, 5)
        };

        foreach (var (position, count) in layout)
        {
            for (var i = 0; i < count; i++)
            {
                team.Players.Add(new Player
                {
                    Id = Guid.NewGuid(),
                    TeamId = team.Id,
                    Name = $"{name} {position}{i}",
                    Position = position,
                    Role = position == Position.GK ? PlayerRole.GK : PlayerRole.CM,
                    ShirtNumber = team.Players.Count + 1,
                    Age = 25,
                    Nationality = "Test",
                    // The first of each line is the best, so a test can pick a
                    // star or a reserve by index.
                    Overall = level - i,
                    Pace = 70, Shooting = 70, Passing = 70,
                    Dribbling = 70, Defending = 70, Physical = 70,
                    IsStarter = i == 0
                });
            }
        }

        return team;
    }

    public void Dispose() => Db.Dispose();
}

public class TransferServiceTests
{
    [Fact]
    public async Task TheWindowIsOpenBeforeTheSeasonStarts()
    {
        using var world = new TransferWorld(week: 0);

        var window = await world.Service.GetWindowAsync(TransferWorld.UserId, world.Season.Id);

        Assert.True(window.IsOpen);
        Assert.Equal(TransferWindow.Summer, window.Window);
        Assert.Equal(world.Mine.Budget, window.Budget);
    }

    [Fact]
    public async Task TheWindowIsShutOnceTheSeasonIsUnderWay()
    {
        using var world = new TransferWorld(week: 8);

        var window = await world.Service.GetWindowAsync(TransferWorld.UserId, world.Season.Id);

        Assert.False(window.IsOpen);
        Assert.NotNull(window.OpensAtWeek);
    }

    [Fact]
    public async Task TheMarketListsOtherClubsPlayersButNotYourOwn()
    {
        using var world = new TransferWorld();

        var market = await world.Service.SearchMarketAsync(
            TransferWorld.UserId, world.Season.Id, new MarketQuery(Take: 100));

        Assert.NotEmpty(market);
        Assert.All(market, p => Assert.NotEqual(world.Mine.Id, p.TeamId));
    }

    [Fact]
    public async Task TheMarketNarrowsToWhatTheManagerAsksFor()
    {
        using var world = new TransferWorld();

        var keepers = await world.Service.SearchMarketAsync(
            TransferWorld.UserId, world.Season.Id, new MarketQuery(Position: Position.GK));

        Assert.NotEmpty(keepers);
        Assert.All(keepers, p => Assert.Equal(Position.GK, p.Position));
    }

    [Fact]
    public async Task EveryPlayerOnTheMarketIsPriced()
    {
        using var world = new TransferWorld();

        var market = await world.Service.SearchMarketAsync(
            TransferWorld.UserId, world.Season.Id, new MarketQuery(Take: 100));

        Assert.All(market, p => Assert.True(p.Value > 0));
    }

    [Fact]
    public async Task AGoodOfferSignsThePlayerAndMovesTheMoney()
    {
        using var world = new TransferWorld();
        var target = world.TheirPlayer();
        var asking = AiTransferPolicy.AskingPrice(target, world.Theirs);

        var myBudget = world.Mine.Budget;
        var theirBudget = world.Theirs.Budget;

        var offer = await world.Service.MakeOfferAsync(
            TransferWorld.UserId, world.Season.Id, new MakeOfferRequest(target.Id, asking));

        Assert.Equal(OfferStatus.Accepted, offer.Status);

        var moved = await world.Db.Players.FindAsync(target.Id);
        Assert.Equal(world.Mine.Id, moved!.TeamId);

        // A new signing has no place in the shape until the manager gives him one.
        Assert.False(moved.IsStarter);
        Assert.Equal(0, moved.LineupSlot);

        Assert.Equal(myBudget - asking, (await world.Db.Teams.FindAsync(world.Mine.Id))!.Budget);
        Assert.Equal(theirBudget + asking, (await world.Db.Teams.FindAsync(world.Theirs.Id))!.Budget);
    }

    [Fact]
    public async Task ACompletedTransferIsWrittenIntoTheRecord()
    {
        using var world = new TransferWorld();
        var target = world.TheirPlayer();

        await world.Service.MakeOfferAsync(
            TransferWorld.UserId,
            world.Season.Id,
            new MakeOfferRequest(target.Id, AiTransferPolicy.AskingPrice(target, world.Theirs)));

        var history = await world.Service.GetHistoryAsync(TransferWorld.UserId, world.Season.Id);
        var move = Assert.Single(history);

        Assert.Equal(target.Name, move.PlayerName);
        Assert.Equal(world.Theirs.Name, move.FromTeamName);
        Assert.Equal(world.Mine.Name, move.ToTeamName);
    }

    [Fact]
    public async Task AnOfferBelowWhatTheClubWantsIsTurnedDown()
    {
        using var world = new TransferWorld();
        var target = world.TheirPlayer();
        var asking = AiTransferPolicy.AskingPrice(target, world.Theirs);

        var offer = await world.Service.MakeOfferAsync(
            TransferWorld.UserId, world.Season.Id, new MakeOfferRequest(target.Id, asking / 2));

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.NotNull(offer.ResponseReason);

        var stayed = await world.Db.Players.FindAsync(target.Id);
        Assert.Equal(world.Theirs.Id, stayed!.TeamId);
    }

    [Fact]
    public async Task NothingCanBeBoughtWhileTheWindowIsShut()
    {
        using var world = new TransferWorld(week: 8);
        var target = world.TheirPlayer();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Service.MakeOfferAsync(
                TransferWorld.UserId, world.Season.Id, new MakeOfferRequest(target.Id, 50_000_000m)));
    }

    [Fact]
    public async Task AClubCannotBidMoreThanItHas()
    {
        using var world = new TransferWorld();
        var target = world.TheirPlayer();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Service.MakeOfferAsync(
                TransferWorld.UserId,
                world.Season.Id,
                new MakeOfferRequest(target.Id, world.Mine.Budget + 1m)));
    }

    [Fact]
    public async Task AManagerCannotBidForHisOwnPlayer()
    {
        using var world = new TransferWorld();
        var mine = world.Mine.Players.First();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Service.MakeOfferAsync(
                TransferWorld.UserId, world.Season.Id, new MakeOfferRequest(mine.Id, 1_000_000m)));
    }

    [Fact]
    public async Task TheSameBidCannotBeMadeTwiceWhileItIsStillOpen()
    {
        using var world = new TransferWorld();
        var target = world.TheirPlayer();

        // A bid that is answered at once leaves nothing pending, so this needs
        // an offer the selling club will sit on: one from another club.
        world.Db.TransferOffers.Add(new TransferOffer
        {
            Id = Guid.NewGuid(),
            SeasonId = world.Season.Id,
            PlayerId = target.Id,
            FromTeamId = world.Theirs.Id,
            ToTeamId = world.Mine.Id,
            Amount = 1_000_000m,
            Status = OfferStatus.Pending,
            WeekMade = 0,
            CreatedAt = DateTime.UtcNow
        });
        await world.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Service.MakeOfferAsync(
                TransferWorld.UserId, world.Season.Id, new MakeOfferRequest(target.Id, 2_000_000m)));
    }

    [Fact]
    public async Task AManagerCanTurnDownABidForOneOfHisPlayers()
    {
        using var world = new TransferWorld();
        var wanted = world.Mine.Players.First(p => p.Position == Position.FWD);

        var offer = new TransferOffer
        {
            Id = Guid.NewGuid(),
            SeasonId = world.Season.Id,
            PlayerId = wanted.Id,
            FromTeamId = world.Mine.Id,
            ToTeamId = world.Theirs.Id,
            Amount = 40_000_000m,
            Status = OfferStatus.Pending,
            WeekMade = 0,
            CreatedAt = DateTime.UtcNow
        };

        world.Db.TransferOffers.Add(offer);
        await world.Db.SaveChangesAsync();

        var answered = await world.Service.RespondToOfferAsync(
            TransferWorld.UserId, world.Season.Id, offer.Id, accept: false);

        Assert.Equal(OfferStatus.Rejected, answered.Status);
        Assert.Equal(world.Mine.Id, (await world.Db.Players.FindAsync(wanted.Id))!.TeamId);
    }

    [Fact]
    public async Task AManagerWhoAcceptsABidSellsThePlayerAndTakesTheMoney()
    {
        using var world = new TransferWorld();
        var wanted = world.Mine.Players.Last(p => p.Position == Position.FWD);
        const decimal fee = 15_000_000m;

        var offer = new TransferOffer
        {
            Id = Guid.NewGuid(),
            SeasonId = world.Season.Id,
            PlayerId = wanted.Id,
            FromTeamId = world.Mine.Id,
            ToTeamId = world.Theirs.Id,
            Amount = fee,
            Status = OfferStatus.Pending,
            WeekMade = 0,
            CreatedAt = DateTime.UtcNow
        };

        world.Db.TransferOffers.Add(offer);
        await world.Db.SaveChangesAsync();

        var budget = world.Mine.Budget;

        var answered = await world.Service.RespondToOfferAsync(
            TransferWorld.UserId, world.Season.Id, offer.Id, accept: true);

        Assert.Equal(OfferStatus.Accepted, answered.Status);
        Assert.Equal(world.Theirs.Id, (await world.Db.Players.FindAsync(wanted.Id))!.TeamId);
        Assert.Equal(budget + fee, (await world.Db.Teams.FindAsync(world.Mine.Id))!.Budget);
    }

    [Fact]
    public async Task AnUnansweredBidCanBePulled()
    {
        using var world = new TransferWorld();
        var target = world.TheirPlayer();

        var offer = new TransferOffer
        {
            Id = Guid.NewGuid(),
            SeasonId = world.Season.Id,
            PlayerId = target.Id,
            FromTeamId = world.Theirs.Id,
            ToTeamId = world.Mine.Id,
            Amount = 5_000_000m,
            Status = OfferStatus.Pending,
            WeekMade = 0,
            CreatedAt = DateTime.UtcNow
        };

        world.Db.TransferOffers.Add(offer);
        await world.Db.SaveChangesAsync();

        var pulled = await world.Service.WithdrawOfferAsync(
            TransferWorld.UserId, world.Season.Id, offer.Id);

        Assert.Equal(OfferStatus.Withdrawn, pulled.Status);
    }

    [Fact]
    public async Task AManagerSeesBothWhatHeHasBidAndWhatHeHasBeenOffered()
    {
        using var world = new TransferWorld();

        world.Db.TransferOffers.AddRange(
            new TransferOffer
            {
                Id = Guid.NewGuid(),
                SeasonId = world.Season.Id,
                PlayerId = world.Mine.Players.First().Id,
                FromTeamId = world.Mine.Id,
                ToTeamId = world.Theirs.Id,
                Amount = 1_000_000m,
                WeekMade = 0,
                CreatedAt = DateTime.UtcNow
            },
            new TransferOffer
            {
                Id = Guid.NewGuid(),
                SeasonId = world.Season.Id,
                PlayerId = world.TheirPlayer().Id,
                FromTeamId = world.Theirs.Id,
                ToTeamId = world.Mine.Id,
                Amount = 2_000_000m,
                WeekMade = 0,
                CreatedAt = DateTime.UtcNow
            });
        await world.Db.SaveChangesAsync();

        var offers = await world.Service.GetOffersAsync(TransferWorld.UserId, world.Season.Id);

        Assert.Equal(2, offers.Count);
        Assert.Contains(offers, o => o.IsIncoming);
        Assert.Contains(offers, o => !o.IsIncoming);
    }

    [Fact]
    public async Task AnotherManagersSeasonIsNotVisible()
    {
        using var world = new TransferWorld();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            world.Service.GetWindowAsync("someone-else", world.Season.Id));
    }
}
