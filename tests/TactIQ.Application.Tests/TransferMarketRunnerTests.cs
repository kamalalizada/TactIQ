using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Transfers;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class TransferMarketRunnerTests
{
    private const string UserId = "manager-1";
    private const int LeagueTeams = 20;
    private static int TotalWeeks => 2 * (LeagueTeams - 1);

    /// <summary>
    /// A league of clubs of differing strength, so there is business worth
    /// doing: a strong club with money, a weak one with players it can spare.
    /// </summary>
    private static (TestDbContext Db, Season Season, List<Team> Clubs) League(
        int week = 0, bool withManager = true)
    {
        var db = TestDbContext.Create();

        var season = new Season
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Name = "Test",
            GameMode = GameMode.ManagerMode,
            CreatedAt = DateTime.UtcNow,
            CurrentWeek = week
        };

        var clubs = new List<Team>();

        for (var i = 0; i < 8; i++)
        {
            var level = 84 - i * 3;
            var club = new Team
            {
                Id = Guid.NewGuid(),
                SeasonId = season.Id,
                Name = $"Klub {i}",
                Country = "England",
                Budget = 60_000_000m,
                Formation = "4-4-2"
            };

            var layout = new[]
            {
                (Position.GK, 3), (Position.DEF, 7), (Position.MID, 6), (Position.FWD, 5)
            };

            foreach (var (position, count) in layout)
            {
                for (var n = 0; n < count; n++)
                    club.Players.Add(new Player
                    {
                        Id = Guid.NewGuid(),
                        TeamId = club.Id,
                        Name = $"K{i} {position}{n}",
                        Position = position,
                        Role = position == Position.GK ? PlayerRole.GK : PlayerRole.CM,
                        ShirtNumber = club.Players.Count + 1,
                        Age = 25,
                        Nationality = "Test",
                        Overall = level - n,
                        Pace = 70, Shooting = 70, Passing = 70,
                        Dribbling = 70, Defending = 70, Physical = 70,
                        IsStarter = n == 0
                    });
            }

            clubs.Add(club);
        }

        if (withManager)
            season.UserTeamId = clubs[0].Id;

        db.Seasons.Add(season);
        db.Teams.AddRange(clubs);
        db.Competitions.Add(new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            Name = "Liqa",
            Country = "England",
            Kind = CompetitionKind.League,
            TeamCount = LeagueTeams,
            CurrentWeek = Math.Max(1, week)
        });
        db.SaveChanges();

        return (db, season, clubs);
    }

    [Fact]
    public async Task ClubsDoBusinessWithEachOtherWhileTheWindowIsOpen()
    {
        var (db, season, _) = League();
        using var _db = db;

        await new TransferMarketRunner(db).RunWeekAsync(season, TotalWeeks, new Random(1), default);

        Assert.NotEmpty(await db.Transfers.ToListAsync());
    }

    [Fact]
    public async Task NothingMovesWhileTheWindowIsShut()
    {
        var (db, season, _) = League(week: 8);
        using var _db = db;

        await new TransferMarketRunner(db).RunWeekAsync(season, TotalWeeks, new Random(1), default);

        Assert.Empty(await db.Transfers.ToListAsync());
    }

    [Fact]
    public async Task EveryTransferMovesTheMoneyAsWellAsThePlayer()
    {
        var (db, season, clubs) = League();
        using var _db = db;

        var before = clubs.ToDictionary(c => c.Id, c => c.Budget);

        await new TransferMarketRunner(db).RunWeekAsync(season, TotalWeeks, new Random(5), default);

        foreach (var move in await db.Transfers.ToListAsync())
        {
            var seller = await db.Teams.FindAsync(move.FromTeamId);
            var buyer = await db.Teams.FindAsync(move.ToTeamId);

            Assert.True(seller!.Budget > before[seller.Id] - move.Amount);
            Assert.True(buyer!.Budget < before[buyer.Id] + move.Amount);

            // Nobody spends money they do not have.
            Assert.True(buyer.Budget >= 0);
        }
    }

    [Fact]
    public async Task NoClubIsLeftUnableToFieldASide()
    {
        var (db, season, _) = League();
        using var _db = db;

        var runner = new TransferMarketRunner(db);
        var random = new Random(11);

        // Run both weeks of the summer window, so the squads are churned.
        for (var week = 0; week <= TransferWindowRules.SummerLastWeek; week++)
        {
            season.CurrentWeek = week;
            await runner.RunWeekAsync(season, TotalWeeks, random, default);
        }

        var squads = await db.Players
            .GroupBy(p => p.TeamId)
            .Select(g => new { TeamId = g.Key, Size = g.Count() })
            .ToListAsync();

        Assert.All(squads, squad => Assert.True(
            squad.Size >= SquadLimits.MinimumSquad,
            $"A club was left with {squad.Size} players."));
    }

    [Fact]
    public async Task ClubsComeInForTheManagersPlayers()
    {
        var (db, season, clubs) = League();
        using var _db = db;

        await new TransferMarketRunner(db).RunWeekAsync(season, TotalWeeks, new Random(3), default);

        var mine = clubs[0].Id;
        var approaches = await db.TransferOffers
            .Where(o => o.FromTeamId == mine && o.Status == OfferStatus.Pending)
            .ToListAsync();

        Assert.NotEmpty(approaches);

        // A bid the manager is meant to answer is left open, and is for one of
        // his own players.
        var wanted = await db.Players.FindAsync(approaches[0].PlayerId);
        Assert.Equal(mine, wanted!.TeamId);
    }

    [Fact]
    public async Task TheManagersOwnSquadIsNeverSoldOutFromUnderHim()
    {
        var (db, season, clubs) = League();
        using var _db = db;

        var mine = clubs[0].Id;
        var before = await db.Players.CountAsync(p => p.TeamId == mine);

        await new TransferMarketRunner(db).RunWeekAsync(season, TotalWeeks, new Random(9), default);

        // Clubs may bid, but only the manager can agree to a sale.
        Assert.Equal(before, await db.Players.CountAsync(p => p.TeamId == mine));
    }

    [Fact]
    public async Task UnansweredBidsLapseWhenTheWindowShuts()
    {
        var (db, season, clubs) = League();
        using var _db = db;

        db.TransferOffers.Add(new TransferOffer
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            PlayerId = clubs[0].Players[0].Id,
            FromTeamId = clubs[0].Id,
            ToTeamId = clubs[1].Id,
            Amount = 5_000_000m,
            Status = OfferStatus.Pending,
            WeekMade = 0,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        season.CurrentWeek = 8;
        await new TransferMarketRunner(db).RunWeekAsync(season, TotalWeeks, new Random(1), default);

        var offer = await db.TransferOffers.FirstAsync();
        Assert.Equal(OfferStatus.Expired, offer.Status);
    }

    [Fact]
    public async Task EveryMoveMakesTheBuyingClubBetterInThatPosition()
    {
        var (db, season, clubs) = League(withManager: false);
        using var _db = db;

        var strength = clubs.ToDictionary(
            c => c.Id,
            c => c.Players.GroupBy(p => p.Position)
                          .ToDictionary(g => g.Key, g => g.Max(p => p.Overall)));

        await new TransferMarketRunner(db).RunWeekAsync(season, TotalWeeks, new Random(21), default);

        foreach (var move in await db.Transfers.ToListAsync())
        {
            var signing = await db.Players.FindAsync(move.PlayerId);
            var had = strength[move.ToTeamId].GetValueOrDefault(signing!.Position);

            Assert.True(
                signing.Overall > had,
                $"{move.ToTeamName} signed a {signing.Overall} when it already had a {had}.");
        }
    }

    [Fact]
    public async Task ASignedPlayerHasNoPlaceInTheNewShapeUntilHeIsPicked()
    {
        var (db, season, _) = League(withManager: false);
        using var _db = db;

        await new TransferMarketRunner(db).RunWeekAsync(season, TotalWeeks, new Random(2), default);

        foreach (var move in await db.Transfers.ToListAsync())
        {
            var signing = await db.Players.FindAsync(move.PlayerId);

            Assert.False(signing!.IsStarter);
            Assert.Equal(0, signing.LineupSlot);
        }
    }
}
