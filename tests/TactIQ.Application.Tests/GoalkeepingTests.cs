using TactIQ.Application.Seeding;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class GoalkeepingTests
{
    private static Player Keeper(int reflexes, int positioning, int handling, int diving) => new()
    {
        Name = "Keeper",
        Position = Position.GK,
        Role = PlayerRole.GK,
        Reflexes = reflexes,
        Positioning = positioning,
        Handling = handling,
        Diving = diving
    };

    [Fact]
    public void AKeeperIsRatedOnKeepingGoal()
    {
        var good = Keeper(reflexes: 86, positioning: 83, handling: 82, diving: 85);
        var ordinary = Keeper(reflexes: 70, positioning: 68, handling: 66, diving: 69);

        Assert.True(good.Goalkeeping > ordinary.Goalkeeping);
        Assert.InRange(good.Goalkeeping, 80, 90);
    }

    [Fact]
    public void ReflexesMatterMostOfAll()
    {
        // Getting to the shot is the larger part of the job; the rest decides
        // what happens once he has.
        var shotStopper = Keeper(reflexes: 90, positioning: 70, handling: 70, diving: 70);
        var sweeper = Keeper(reflexes: 70, positioning: 90, handling: 90, diving: 90);

        Assert.True(shotStopper.Goalkeeping < sweeper.Goalkeeping);

        // But reflexes alone move the number more than any single other.
        var baseline = Keeper(reflexes: 70, positioning: 70, handling: 70, diving: 70);
        var moreReflexes = Keeper(reflexes: 80, positioning: 70, handling: 70, diving: 70);
        var moreHandling = Keeper(reflexes: 70, positioning: 70, handling: 80, diving: 70);

        Assert.True(
            moreReflexes.Goalkeeping - baseline.Goalkeeping
            > moreHandling.Goalkeeping - baseline.Goalkeeping);
    }

    [Fact]
    public void AnOutfieldPlayerInGoalFallsBackOnWhatHeHas()
    {
        // Nobody should read as a zero: a defender pressed into goal is poor,
        // not literally unable to keep.
        var defender = new Player
        {
            Name = "Emergency",
            Position = Position.DEF,
            Defending = 84,
            Physical = 80,
            Pace = 72
        };

        Assert.True(defender.Goalkeeping > 0);
        Assert.InRange(defender.Goalkeeping, 70, 85);
    }

    [Fact]
    public void ASeededKeeperCarriesHisOwnAttributes()
    {
        // Read from the seed rather than by building a league: a league with a
        // newly promoted club whose squad has not been entered yet cannot be
        // created at all, and this test is about one keeper's card.
        var barcelona = SquadData.All.Single(squad => squad.Name == "FC Barcelona");
        var keeper = barcelona.Players.Single(p => p.Name == "Joan García");

        Assert.Equal(86, keeper.Reflexes);
        Assert.Equal(82, keeper.Handling);
        Assert.Equal(83, keeper.Positioning);
    }

    [Fact]
    public void EverySeededKeeperCanActuallyKeepGoal()
    {
        // A keeper left at zero would let in everything, so the whole seed is
        // checked rather than the one club that prompted this.
        var clubs = SquadData.Countries
            .Where(SquadData.IsLeagueReady)
            .SelectMany(country => new TeamFactory(new Random(7))
                .CreateLeagueTeams(Guid.NewGuid(), country))
            .ToList();

        var keepers = clubs
            .SelectMany(club => club.Players)
            .Where(player => player.Position == Position.GK)
            .ToList();

        Assert.NotEmpty(keepers);
        Assert.All(keepers, keeper => Assert.True(
            keeper.Goalkeeping >= 40,
            $"{keeper.Name} would keep goal at {keeper.Goalkeeping}."));
    }

    [Fact]
    public void OutfieldPlayersCarryNoKeepingAttributes()
    {
        var clubs = new TeamFactory(new Random(3)).CreateTeams(Guid.NewGuid(), 20);

        var outfield = clubs
            .SelectMany(club => club.Players)
            .Where(player => player.Position != Position.GK);

        Assert.All(outfield, player => Assert.Equal(0, player.Reflexes));
    }
}
