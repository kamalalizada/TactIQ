using TactIQ.Application.Simulation;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class PitchZonesTests
{
    [Theory]
    // The spots the squad screen lays each preset shape out on.
    [InlineData(50, 8, PlayerRole.GK)]
    [InlineData(12, 28, PlayerRole.LB)]
    [InlineData(40.5, 24, PlayerRole.CB)]
    [InlineData(31, 24, PlayerRole.CB)]
    [InlineData(88, 28, PlayerRole.RB)]
    [InlineData(11, 33, PlayerRole.LWB)]
    [InlineData(89, 33, PlayerRole.RWB)]
    [InlineData(50, 42, PlayerRole.CDM)]
    [InlineData(15, 52, PlayerRole.LM)]
    [InlineData(59.5, 52, PlayerRole.CM)]
    [InlineData(85, 52, PlayerRole.RM)]
    [InlineData(50, 64, PlayerRole.CAM)]
    [InlineData(18, 78, PlayerRole.LW)]
    [InlineData(40.5, 84, PlayerRole.ST)]
    [InlineData(82, 78, PlayerRole.RW)]
    public void TheSpotDecidesTheJob(double x, double y, PlayerRole expected)
    {
        Assert.Equal(expected, PitchZones.RoleAt(x, y));
    }

    [Fact]
    public void AWideDefenderPushedOnBecomesAWingBack()
    {
        // The same man on the same touchline: deep he is a full-back, higher up
        // he is the whole flank. This is what lets a manager draw a back three
        // with wing-backs by dragging, rather than picking a preset.
        Assert.Equal(PlayerRole.LB, PitchZones.RoleAt(12, 24));
        Assert.Equal(PlayerRole.LWB, PitchZones.RoleAt(12, 32));

        Assert.Equal(PlayerRole.RB, PitchZones.RoleAt(88, 24));
        Assert.Equal(PlayerRole.RWB, PitchZones.RoleAt(88, 32));

        // A wing-back still defends, so he counts towards the back line.
        Assert.Equal(Position.DEF, RoleFitness.LineOf(PlayerRole.LWB));
        Assert.Equal(Position.DEF, RoleFitness.LineOf(PlayerRole.RWB));
    }

    [Fact]
    public void AWingBackIsCloseToHisFullBackButNotToAWinger()
    {
        // He is the full-back of his flank asked to get forward, so that move
        // is cheap; being sent up to winger is a defender asked to attack.
        Assert.Equal(RoleFitness.Native, RoleFitness.For(PlayerRole.LWB, PlayerRole.LWB));

        var toFullBack = RoleFitness.For(PlayerRole.LWB, PlayerRole.LB);
        var toWinger = RoleFitness.For(PlayerRole.LWB, PlayerRole.LW);
        var acrossTheFlank = RoleFitness.For(PlayerRole.LWB, PlayerRole.RWB);

        Assert.True(toFullBack > toWinger, $"full-back {toFullBack} should beat winger {toWinger}.");
        Assert.True(toWinger < RoleFitness.Native);
        Assert.True(acrossTheFlank < RoleFitness.Native);
    }

    [Fact]
    public void EveryRoleHasASpotThatReadsBackAsItself()
    {
        // The squad screen lays a preset out by role and then reads the roles
        // back off the pitch. A role with no spot of its own would be silently
        // rewritten into a neighbour's job the moment the side was saved.
        foreach (var role in Enum.GetValues<PlayerRole>())
        {
            var found = false;

            for (var x = 2.0; x <= 98 && !found; x += 1)
            for (var y = 2.0; y <= 98 && !found; y += 1)
                found = PitchZones.RoleAt(x, y) == role;

            Assert.True(found, $"No spot on the pitch reads as {role}.");
        }
    }

    [Fact]
    public void ADefenderCarriedIntoTheGoalmouthKeepsGoal()
    {
        Assert.Equal(PlayerRole.GK, PitchZones.RoleAt(50, 4));
    }

    [Fact]
    public void ACentralMidfielderMovedBackOrForwardChangesJob()
    {
        Assert.Equal(PlayerRole.CM, PitchZones.RoleAt(50, 52));
        Assert.Equal(PlayerRole.CDM, PitchZones.RoleAt(50, 44));
        Assert.Equal(PlayerRole.CAM, PitchZones.RoleAt(50, 60));
    }

    [Fact]
    public void DeepButWideIsAFullBackNotAKeeper()
    {
        Assert.Equal(PlayerRole.LB, PitchZones.RoleAt(10, 5));
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(50, 101)]
    public void ASpotOffThePitchIsNotOnIt(double x, double y)
    {
        Assert.False(PitchZones.IsOnPitch(x, y));
    }
}

public class LineupRulesTests
{
    private static readonly PlayerRole[] FourThreeThree =
    [
        PlayerRole.GK,
        PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
        PlayerRole.CM, PlayerRole.CDM, PlayerRole.CM,
        PlayerRole.LW, PlayerRole.ST, PlayerRole.RW
    ];

    [Fact]
    public void EveryShapeTheManagerMayPickIsAllowed()
    {
        foreach (var formation in FormationShape.Available)
            Assert.Null(LineupRules.Problem(FormationShape.Of(formation).ToList()));
    }

    [Fact]
    public void TwoMenInGoalAreTurnedAway()
    {
        var roles = FourThreeThree.ToList();
        roles[2] = PlayerRole.GK;

        Assert.NotNull(LineupRules.Problem(roles));
    }

    [Fact]
    public void ABackLineOfTwoIsTurnedAway()
    {
        var roles = FourThreeThree.ToList();
        roles[1] = PlayerRole.LM;
        roles[4] = PlayerRole.RM;

        Assert.NotNull(LineupRules.Problem(roles));
    }

    [Fact]
    public void ABackThreeIsEnough()
    {
        var roles = FourThreeThree.ToList();
        roles[4] = PlayerRole.RM;

        Assert.Null(LineupRules.Problem(roles));
    }

    [Theory]
    [InlineData(PlayerRole.LB, 4)]
    [InlineData(PlayerRole.RB, 1)]
    [InlineData(PlayerRole.LW, 10)]
    [InlineData(PlayerRole.RW, 8)]
    public void AFlankRoleCannotBeDoubled(PlayerRole doubled, int replacedSlot)
    {
        // Five at the back, so the doubled full-back still leaves enough defenders.
        var roles = FourThreeThree.ToList();
        roles[5] = PlayerRole.CB;
        roles[replacedSlot] = doubled;

        Assert.NotNull(LineupRules.Problem(roles));
    }

    [Fact]
    public void AWingerAndAWideMidfielderMayShareAFlank()
    {
        var roles = FourThreeThree.ToList();
        roles[5] = PlayerRole.LM;

        Assert.Null(LineupRules.Problem(roles));
    }
}
