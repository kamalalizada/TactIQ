using TactIQ.Application.Simulation;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class RoleFitnessTests
{
    [Theory]
    [InlineData(PlayerRole.GK)]
    [InlineData(PlayerRole.CB)]
    [InlineData(PlayerRole.CM)]
    [InlineData(PlayerRole.ST)]
    public void APlayerInTheirOwnRoleIsAtFullStrength(PlayerRole role)
    {
        Assert.Equal(RoleFitness.Native, RoleFitness.For(role, role));
    }

    [Fact]
    public void TheFurtherFromTheirRoleThePlayerStandsTheMoreTheyLose()
    {
        // A left-back is nearly himself at right-back, less so elsewhere in
        // defence, and poor as a centre-forward.
        var acrossTheBack = RoleFitness.For(PlayerRole.LB, PlayerRole.RB);
        var inTheMiddle = RoleFitness.For(PlayerRole.LB, PlayerRole.CB);
        var upFront = RoleFitness.For(PlayerRole.LB, PlayerRole.ST);

        Assert.True(acrossTheBack > inTheMiddle);
        Assert.True(inTheMiddle > upFront);
        Assert.True(upFront < RoleFitness.Native);
    }

    [Fact]
    public void GoalkeepingIsADifferentSportInBothDirections()
    {
        var outfieldInGoal = RoleFitness.For(PlayerRole.ST, PlayerRole.GK);
        var keeperOutfield = RoleFitness.For(PlayerRole.GK, PlayerRole.ST);

        Assert.Equal(outfieldInGoal, keeperOutfield);
        Assert.True(outfieldInGoal < RoleFitness.For(PlayerRole.LB, PlayerRole.ST));
    }

    [Fact]
    public void AStrikerPlayedAtCentreBackIsRatedBelowHisOwnAbility()
    {
        var striker = new Player
        {
            Name = "Out of position",
            Position = Position.FWD,
            Role = PlayerRole.ST,
            Overall = 90,
            Pace = 90,
            Shooting = 90,
            Passing = 90,
            Dribbling = 90,
            Defending = 90,
            Physical = 90
        };

        var atHome = new MatchPlayer(striker);
        var misplaced = new MatchPlayer(striker) { PlayedRole = PlayerRole.CB };

        Assert.Equal(90, atHome.Overall);
        Assert.True(misplaced.Overall < atHome.Overall);
        Assert.True(misplaced.Shooting < atHome.Shooting);
    }

    [Fact]
    public void TheShapeDecidesWhichJobEachShirtFills()
    {
        var roles = FormationShape.Of("4-3-3");

        Assert.Equal(11, roles.Count);
        Assert.Equal(PlayerRole.GK, roles[0]);
        // A front three takes a winger either side of the centre-forward.
        Assert.Equal(PlayerRole.LW, roles[8]);
        Assert.Equal(PlayerRole.ST, roles[9]);
        Assert.Equal(PlayerRole.RW, roles[10]);
    }

    [Fact]
    public void ASubstituteWhoStandsNowhereKeepsHisOwnRole()
    {
        var onTheBench = FormationShape.RoleForSlot("4-4-2", 0, PlayerRole.CAM);

        Assert.Equal(PlayerRole.CAM, onTheBench);
    }

    [Fact]
    public void FieldingASideInItsOwnShapeBeatsFieldingItInAnother()
    {
        // The same eleven, set up as a 4-4-2 and then asked to play 3-4-3: the
        // full-backs become centre-backs and two midfielders become wingers, so
        // the side is worth less without a single change of personnel.
        var asBuilt = new MatchSide(
            TestTeamFactory.Create("Home", 80), isHome: true, TacticalStyle.Balanced, form: 0);

        var misshapen = TestTeamFactory.Create("Home", 80);
        misshapen.Formation = "3-4-3";
        var reshaped = new MatchSide(misshapen, isHome: true, TacticalStyle.Balanced, form: 0);

        Assert.True(reshaped.Attack < asBuilt.Attack);
    }

    [Fact]
    public void EveryShapeTheManagerMayPickPlacesElevenPlayers()
    {
        foreach (var formation in FormationShape.Available)
        {
            var roles = FormationShape.Of(formation);

            Assert.Equal(11, roles.Count);
            Assert.Single(roles, r => r == PlayerRole.GK);
        }
    }
}
