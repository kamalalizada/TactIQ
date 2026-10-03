using TactIQ.Application.Careers;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class PlayerDevelopmentTests
{
    private static Player Player(int age, int overall, Position position = Position.MID) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test",
        Age = age,
        Overall = overall,
        Position = position,
        Pace = overall,
        Shooting = overall,
        Passing = overall,
        Dribbling = overall,
        Defending = overall,
        Physical = overall
    };

    /// <summary>The average change for a cohort of one age, over many players.</summary>
    private static double AverageDelta(int age, int overall = 75, int runs = 400)
    {
        var random = new Random(age * 7919);
        var deltas = new List<int>();

        for (var i = 0; i < runs; i++)
        {
            var change = PlayerDevelopment.Advance(Player(age, overall), random);
            if (!change.Retired) deltas.Add(change.Delta);
        }

        return deltas.Average();
    }

    [Fact]
    public void EveryPlayerIsAYearOlder()
    {
        var player = Player(24, 80);

        PlayerDevelopment.Advance(player, new Random(1));

        Assert.Equal(25, player.Age);
    }

    [Fact]
    public void TheYoungImproveAndTheOldDecline()
    {
        Assert.True(AverageDelta(19) > 1.5, "A teenager should improve markedly.");
        Assert.True(AverageDelta(22) > 0, "An early-twenties player should still be improving.");
        Assert.True(AverageDelta(33) < -1.5, "A player in his thirties should be falling away.");
    }

    [Fact]
    public void ThereIsAPeakInTheMiddleOfACareer()
    {
        // The years a club pays most for: a player holds what he is rather than
        // gaining or losing much.
        var peak = AverageDelta(26);

        Assert.InRange(peak, -0.5, 1.5);
        Assert.True(AverageDelta(19) > peak);
        Assert.True(AverageDelta(33) < peak);
    }

    [Fact]
    public void DeclineGetsSteeperWithAge()
    {
        Assert.True(AverageDelta(35) < AverageDelta(31));
        Assert.True(AverageDelta(31) < AverageDelta(27));
    }

    [Fact]
    public void RoomAtTheTopIsScarce()
    {
        // The same age gains far less when a player is already among the best:
        // a 90 does not become a 94 in a summer.
        Assert.True(AverageDelta(22, overall: 90) < AverageDelta(22, overall: 70));
    }

    [Fact]
    public void ACareerEndsWithAge()
    {
        var player = Player(PlayerDevelopment.RetirementAge, 80);

        var change = PlayerDevelopment.Advance(player, new Random(1));

        Assert.True(change.Retired);
    }

    [Fact]
    public void APlayerWhoFallsBelowTheStandardRetires()
    {
        // Somebody worn down past the level the game is played at stops, rather
        // than filling a squad place nobody will pick him for.
        var random = new Random(3);
        var player = Player(35, PlayerDevelopment.FloorRating);

        var change = PlayerDevelopment.Advance(player, random);

        Assert.True(change.Retired);
    }

    [Fact]
    public void AttributesFollowTheRating()
    {
        var random = new Random(11);
        var improving = Player(19, 70);
        var declining = Player(35, 80);

        var up = PlayerDevelopment.Advance(improving, random);
        var down = PlayerDevelopment.Advance(declining, random);

        if (up.Improved)
            Assert.True(improving.Passing > 70, "An improved player should be better at the game.");

        if (down.Declined)
        {
            // Pace goes first and physique holds up longest, which is what
            // turns an ageing forward into a different kind of player.
            Assert.True(declining.Pace < 80);
            Assert.True(declining.Physical > declining.Pace);
        }
    }

    [Fact]
    public void NobodyGrowsWithoutLimit()
    {
        var random = new Random(5);
        var player = Player(17, 80);

        for (var year = 0; year < 20; year++)
        {
            var change = PlayerDevelopment.Advance(player, random);
            if (change.Retired) break;

            Assert.InRange(player.Overall, 20, 94);
            Assert.InRange(player.Pace, 20, 99);
        }
    }

    [Fact]
    public void EveryCareerEndsEventually()
    {
        var random = new Random(9);
        var player = Player(20, 85);

        var seasons = 0;
        while (seasons < 40)
        {
            seasons++;
            if (PlayerDevelopment.Advance(player, random).Retired) break;
        }

        Assert.True(seasons < 40, "A career ran for forty seasons.");
        Assert.True(seasons > 5, "A career ended far too soon.");
    }
}

public class YouthAcademyTests
{
    private static Team Club(int standard = 76)
    {
        var club = new Team { Id = Guid.NewGuid(), Name = "Test" };

        for (var i = 0; i < 18; i++)
            club.Players.Add(new Player
            {
                Id = Guid.NewGuid(),
                TeamId = club.Id,
                Overall = standard,
                Age = 26,
                Position = Position.MID
            });

        return club;
    }

    private static Player Retiring(Position position = Position.FWD) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Veteran",
        Position = position,
        Role = PlayerRole.ST,
        ShirtNumber = 9,
        Age = 38,
        Overall = 80,
        Nationality = "Turkey"
    };

    [Fact]
    public void AYouthPlayerTakesThePositionAndShirtOfTheManHeReplaces()
    {
        var club = Club();
        var leaving = Retiring(Position.DEF);

        var kid = YouthAcademy.Promote(club, leaving, new Random(1));

        Assert.Equal(leaving.Position, kid.Position);
        Assert.Equal(leaving.ShirtNumber, kid.ShirtNumber);
        Assert.Equal(club.Id, kid.TeamId);
    }

    [Fact]
    public void AYouthPlayerIsYoungAndRaw()
    {
        var club = Club(standard: 80);
        var random = new Random(2);

        for (var i = 0; i < 20; i++)
        {
            var kid = YouthAcademy.Promote(club, Retiring(), random);

            Assert.InRange(kid.Age, 17, 19);
            Assert.True(kid.Overall < 80, "A youth player should not arrive ready-made.");
            Assert.True(kid.Overall >= 54, "A youth player should still be playable.");
        }
    }

    [Fact]
    public void AStrongClubBringsThroughBetterPlayersThanAWeakOne()
    {
        var random = new Random(4);

        var fromStrong = Average(Club(standard: 85), random);
        var fromWeak = Average(Club(standard: 62), random);

        Assert.True(fromStrong > fromWeak);

        static double Average(Team club, Random random) => Enumerable
            .Range(0, 30)
            .Select(_ => YouthAcademy.Promote(club, Retiring(), random).Overall)
            .Average();
    }

    [Fact]
    public void AYouthPlayerHasNoPlaceInTheSideUntilHeIsPicked()
    {
        var kid = YouthAcademy.Promote(Club(), Retiring(), new Random(6));

        Assert.False(kid.IsStarter);
        Assert.Equal(0, kid.LineupSlot);
        Assert.Equal(100, kid.Condition);
    }

    [Fact]
    public void AttributesSuitThePositionHeIsBroughtThroughFor()
    {
        var random = new Random(8);
        var club = Club();

        var keeper = YouthAcademy.Promote(club, Retiring(Position.GK), random);
        var forward = YouthAcademy.Promote(club, Retiring(Position.FWD), random);
        var defender = YouthAcademy.Promote(club, Retiring(Position.DEF), random);

        Assert.True(keeper.Shooting < keeper.Defending);
        Assert.True(forward.Shooting > forward.Defending);
        Assert.True(defender.Defending > defender.Shooting);
    }

    [Fact]
    public void EveryYouthPlayerIsNamed()
    {
        var club = Club();
        var random = new Random(10);

        for (var i = 0; i < 25; i++)
        {
            var kid = YouthAcademy.Promote(club, Retiring(), random);

            Assert.False(string.IsNullOrWhiteSpace(kid.Name));
            Assert.Contains(' ', kid.Name);
        }
    }
}
