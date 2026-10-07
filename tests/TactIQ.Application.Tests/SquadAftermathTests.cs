using TactIQ.Application.Simulation;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class SquadAftermathTests
{
    private static Player Player(int condition = 100) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test",
        Position = Position.MID,
        Overall = 75,
        Age = 25,
        Condition = condition
    };

    [Fact]
    public void AKnockKeepsAPlayerOutForMoreThanOneWeek()
    {
        // A four-match injury recovered three times a week was gone before the
        // manager ever saw it, because a season runs three leagues at once.
        var player = Player();

        SquadAftermath.Apply([player], [Aftermath(player, injured: true)]);

        var weeksOut = player.MatchesInjured;
        Assert.True(weeksOut >= 1);

        SquadAftermath.RecoverWeek([player]);

        Assert.Equal(weeksOut - 1, player.MatchesInjured);
    }

    [Fact]
    public void ASendingOffCarriesABan()
    {
        var player = Player();

        SquadAftermath.Apply([player], [Aftermath(player, sentOff: true)]);

        Assert.True(player.MatchesSuspended > 0);
        Assert.False(player.IsAvailable);
    }

    [Fact]
    public void ARunOfBookingsEarnsABanOfItsOwn()
    {
        var player = Player();

        for (var match = 0; match < EngineConstants.YellowCardsForBan; match++)
            SquadAftermath.Apply([player], [Aftermath(player, yellowCards: 1)]);

        Assert.True(player.MatchesSuspended > 0);
        Assert.True(player.YellowCards < EngineConstants.YellowCardsForBan);
    }

    [Fact]
    public void BansAndKnocksTickDownAWeekAtATime()
    {
        var player = Player();
        player.MatchesInjured = 3;
        player.MatchesSuspended = 2;

        SquadAftermath.RecoverWeek([player]);

        Assert.Equal(2, player.MatchesInjured);
        Assert.Equal(1, player.MatchesSuspended);
    }

    [Fact]
    public void RecoveryNeverRunsPastZero()
    {
        var player = Player();

        for (var week = 0; week < 5; week++)
            SquadAftermath.RecoverWeek([player]);

        Assert.Equal(0, player.MatchesInjured);
        Assert.Equal(0, player.MatchesSuspended);
        Assert.True(player.IsAvailable);
    }

    [Fact]
    public void ATiredPlayerGetsSomeConditionBackEachWeek()
    {
        var player = Player(condition: 60);

        SquadAftermath.RecoverWeek([player]);

        Assert.True(player.Condition > 60);
        Assert.True(player.Condition <= 100);
    }

    [Fact]
    public void ConditionNeverClimbsPastFull()
    {
        var player = Player(condition: 98);

        SquadAftermath.RecoverWeek([player]);
        SquadAftermath.RecoverWeek([player]);

        Assert.Equal(100, player.Condition);
    }

    [Fact]
    public void AHeavilyUsedPlayerStaysPlayable()
    {
        // Condition has a floor: a player run into the ground is weak, not
        // unusable for the rest of the season.
        var player = Player();

        SquadAftermath.Apply([player], [Aftermath(player, condition: 0)]);

        Assert.True(player.Condition >= 40);
    }

    private static PlayerAftermath Aftermath(
        Player player,
        int condition = 90,
        int yellowCards = 0,
        bool sentOff = false,
        bool injured = false) =>
        new(player.Id, condition, injured, sentOff, yellowCards);
}
