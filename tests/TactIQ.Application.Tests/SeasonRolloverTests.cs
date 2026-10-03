using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Careers;
using TactIQ.Application.Scheduling;
using TactIQ.Application.Seeding;
using TactIQ.Application.Standings;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

/// <summary>
/// A finished season of four clubs, ready to be rolled into the next one.
/// Small enough to assert against, with every match played so the year can
/// legitimately be closed.
/// </summary>
internal sealed class CareerWorld : IDisposable
{
    public const string UserId = "manager-1";

    public TestDbContext Db { get; }
    public Season Season { get; }
    public Competition League { get; }
    public List<Team> Clubs { get; private set; } = [];
    public SeasonRollover Rollover { get; }
    public CareerService Service { get; }

    public CareerWorld(bool finished = true, int clubCount = 4)
    {
        Db = TestDbContext.Create();

        Season = new Season
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Name = "Career",
            GameMode = GameMode.ManagerMode,
            CreatedAt = DateTime.UtcNow,
            Year = 1
        };

        for (var i = 0; i < clubCount; i++)
            Clubs.Add(Club($"Klub {i}", 80 - i * 4));

        Season.UserTeamId = Clubs[0].Id;

        League = new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = Season.Id,
            Name = "Liqa",
            Country = "England",
            Kind = CompetitionKind.League,
            TeamCount = clubCount,
            // A league is finished once the week passes the last of them.
            CurrentWeek = finished ? 2 * (clubCount - 1) + 1 : 1
        };

        League.Entries = Clubs
            .Select(c => new CompetitionEntry { Id = Guid.NewGuid(), TeamId = c.Id })
            .ToList();

        Db.Seasons.Add(Season);
        Db.Teams.AddRange(Clubs);
        Db.Competitions.Add(League);

        // A played season, so there is a table to write into the record.
        AddPlayedFixtures();

        Db.SaveChanges();
        Db.ChangeTracker.Clear();

        // Re-read, so the rollover works against tracked entities of its own
        // rather than the graph this constructor built.
        Season = Db.Seasons.First(x => x.Id == Season.Id);
        League = Db.Competitions.First(x => x.Id == League.Id);
        Clubs = Db.Teams.Include(t => t.Players).Where(t => t.SeasonId == Season.Id).ToList();

        var schedule = new RoundRobinScheduleGenerator();
        var table = new StandingsCalculator();

        Rollover = new SeasonRollover(Db, schedule, table);
        Service = new CareerService(Db, schedule, table);
    }

    private Team Club(string name, int level)
    {
        var club = new Team
        {
            Id = Guid.NewGuid(),
            SeasonId = Season.Id,
            Name = name,
            Country = "England",
            Formation = "4-4-2",
            Budget = 20_000_000m
        };

        var layout = new[]
        {
            (Position.GK, 3), (Position.DEF, 7), (Position.MID, 6), (Position.FWD, 5)
        };

        var shirt = 1;

        foreach (var (position, count) in layout)
        {
            for (var i = 0; i < count; i++)
                club.Players.Add(new Player
                {
                    Id = Guid.NewGuid(),
                    TeamId = club.Id,
                    Name = $"{name} {position}{i}",
                    Position = position,
                    Role = position == Position.GK ? PlayerRole.GK : PlayerRole.CM,
                    ShirtNumber = shirt++,
                    // A spread of ages, so a rollover retires somebody and
                    // improves somebody else.
                    Age = 18 + i * 4,
                    Nationality = "Test",
                    Overall = level - i,
                    Pace = 70, Shooting = 70, Passing = 70,
                    Dribbling = 70, Defending = 70, Physical = 70,
                    IsStarter = club.Players.Count < 11
                });
        }

        return club;
    }

    /// <summary>Gives the league a full set of results for the table to read.</summary>
    private void AddPlayedFixtures()
    {
        var week = 1;

        for (var home = 0; home < Clubs.Count; home++)
        for (var away = 0; away < Clubs.Count; away++)
        {
            if (home == away) continue;

            Db.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                CompetitionId = League.Id,
                WeekNumber = week++,
                HomeTeamId = Clubs[home].Id,
                AwayTeamId = Clubs[away].Id,
                // The stronger club wins, so the table is predictable.
                HomeGoals = home < away ? 2 : 0,
                AwayGoals = home < away ? 0 : 2,
                IsPlayed = true
            });
        }
    }

    public void Dispose() => Db.Dispose();
}

public class SeasonRolloverTests
{
    [Fact]
    public async Task AFinishedSeasonRollsOnToTheNext()
    {
        using var world = new CareerWorld();

        await world.Rollover.AdvanceAsync(world.Season, new Random(1), default);

        Assert.Equal(2, world.Season.Year);
        Assert.Equal(0, world.Season.CurrentWeek);
    }

    [Fact]
    public async Task ASeasonStillBeingPlayedCannotBeClosed()
    {
        using var world = new CareerWorld(finished: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Rollover.AdvanceAsync(world.Season, new Random(1), default));
    }

    [Fact]
    public async Task TheTableIsWrittenDownBeforeItIsCleared()
    {
        using var world = new CareerWorld();

        await world.Rollover.AdvanceAsync(world.Season, new Random(1), default);

        var record = Assert.Single(await world.Db.SeasonRecords.ToListAsync());

        Assert.Equal(1, record.Year);
        Assert.Equal("Liqa", record.CompetitionName);
        Assert.NotNull(record.ChampionTeamName);
        Assert.NotNull(record.UserRank);
    }

    [Fact]
    public async Task EveryPlayerIsAYearOlder()
    {
        using var world = new CareerWorld();

        var before = await world.Db.Players
            .ToDictionaryAsync(p => p.Id, p => p.Age);

        await world.Rollover.AdvanceAsync(world.Season, new Random(2), default);

        var after = await world.Db.Players.ToListAsync();

        foreach (var player in after)
        {
            // A youth player brought through this summer has no "before".
            if (before.TryGetValue(player.Id, out var was))
                Assert.Equal(was + 1, player.Age);
        }
    }

    [Fact]
    public async Task NoSquadIsLeftShortWhenCareersEnd()
    {
        using var world = new CareerWorld();
        var random = new Random(3);

        // Ten years, which is long enough that everybody seeded has retired.
        for (var year = 0; year < 10; year++)
        {
            await PlayOutAsync(world);
            await world.Rollover.AdvanceAsync(world.Season, random, default);
        }

        var squads = await world.Db.Players
            .GroupBy(p => p.TeamId)
            .Select(g => new { g.Key, Size = g.Count() })
            .ToListAsync();

        Assert.Equal(world.Clubs.Count, squads.Count);
        Assert.All(squads, squad => Assert.Equal(21, squad.Size));
    }

    [Fact]
    public async Task EveryClubStillHasAnElevenAfterwards()
    {
        using var world = new CareerWorld();
        var random = new Random(4);

        for (var year = 0; year < 5; year++)
        {
            await PlayOutAsync(world);
            await world.Rollover.AdvanceAsync(world.Season, random, default);
        }

        var starters = await world.Db.Players
            .Where(p => p.IsStarter)
            .GroupBy(p => p.TeamId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        Assert.All(starters, side => Assert.Equal(SquadRules.StarterCount, side.Count));
    }

    [Fact]
    public async Task TheFixturesAreRedrawnAndTheTableStartsAgain()
    {
        using var world = new CareerWorld();

        await world.Rollover.AdvanceAsync(world.Season, new Random(5), default);

        var matches = await world.Db.Matches.ToListAsync();

        Assert.NotEmpty(matches);
        Assert.All(matches, match => Assert.False(match.IsPlayed));

        var league = await world.Db.Competitions.FirstAsync();
        Assert.Equal(1, league.CurrentWeek);
        Assert.Null(league.WinnerTeamId);
        Assert.False(league.PrizesPaid);
    }

    [Fact]
    public async Task WhatTheManagerBuiltCarriesOver()
    {
        using var world = new CareerWorld();

        var club = world.Clubs[0];
        var budget = club.Budget;
        var formation = club.Formation;
        var squadSize = await world.Db.Players.CountAsync(p => p.TeamId == club.Id);

        await world.Rollover.AdvanceAsync(world.Season, new Random(6), default);

        var after = await world.Db.Teams.FirstAsync(t => t.Id == club.Id);
        var squadAfter = await world.Db.Players.CountAsync(p => p.TeamId == club.Id);

        Assert.Equal(budget, after.Budget);
        Assert.Equal(formation, after.Formation);

        // A retirement is answered by a youth player, so the squad is the size
        // it was however many careers ended.
        Assert.Equal(squadSize, squadAfter);
    }

    [Fact]
    public async Task UnansweredBidsDoNotSurviveTheSummer()
    {
        using var world = new CareerWorld();

        world.Db.TransferOffers.Add(new TransferOffer
        {
            Id = Guid.NewGuid(),
            SeasonId = world.Season.Id,
            PlayerId = world.Clubs[1].Players[0].Id,
            FromTeamId = world.Clubs[1].Id,
            ToTeamId = world.Clubs[0].Id,
            Amount = 1_000_000m,
            Status = OfferStatus.Pending,
            WeekMade = 1,
            CreatedAt = DateTime.UtcNow
        });
        await world.Db.SaveChangesAsync();

        await world.Rollover.AdvanceAsync(world.Season, new Random(7), default);

        var offer = await world.Db.TransferOffers.FirstAsync();
        Assert.Equal(OfferStatus.Expired, offer.Status);
    }

    [Fact]
    public async Task ACareerAccumulatesARecord()
    {
        using var world = new CareerWorld();
        var random = new Random(8);

        for (var year = 0; year < 3; year++)
        {
            await PlayOutAsync(world);
            await world.Rollover.AdvanceAsync(world.Season, random, default);
        }

        var career = await world.Service.GetAsync(CareerWorld.UserId, world.Season.Id);

        Assert.Equal(4, career.CurrentYear);
        Assert.Equal(3, career.History.Count);
        Assert.Equal([3, 2, 1], career.History.Select(r => r.Year));
    }

    [Fact]
    public async Task TheManagerIsToldWhoRetiredAndWhoCameThrough()
    {
        using var world = new CareerWorld();

        // Age the manager's squad to the end of their careers, so there is
        // something to report.
        foreach (var player in world.Clubs[0].Players)
            player.Age = PlayerDevelopment.RetirementAge;

        await world.Db.SaveChangesAsync();

        var summary = await world.Service.AdvanceAsync(CareerWorld.UserId, world.Season.Id);

        Assert.NotEmpty(summary.Retired);
        Assert.All(summary.Retired, r => Assert.NotNull(r.ReplacedBy));
        Assert.Equal(world.Clubs[0].Name, summary.Retired[0].TeamName);
    }

    [Fact]
    public async Task AnotherManagersCareerIsNotVisible()
    {
        using var world = new CareerWorld();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            world.Service.GetAsync("someone-else", world.Season.Id));
    }

    /// <summary>Marks the newly drawn fixtures as played, so the year can close again.</summary>
    private static async Task PlayOutAsync(CareerWorld world)
    {
        var matches = await world.Db.Matches.ToListAsync();

        foreach (var match in matches)
        {
            match.IsPlayed = true;
            match.HomeGoals = 1;
            match.AwayGoals = 0;
        }

        var league = await world.Db.Competitions.FirstAsync(c => c.Kind == CompetitionKind.League);
        league.CurrentWeek = 2 * (league.TeamCount - 1) + 1;

        await world.Db.SaveChangesAsync();
    }
}
