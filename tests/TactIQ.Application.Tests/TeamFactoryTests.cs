using TactIQ.Application.Seeding;
using TactIQ.Application.Simulation;
using TactIQ.Application.Transfers;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Tests;

public class TeamFactoryTests
{
    // Seeded so squad shape assertions never depend on the day's luck.
    private readonly TeamFactory _factory = new(new Random(20260923));

    private List<Team> Create(int count = 8) => _factory.CreateTeams(Guid.NewGuid(), count);

    [Fact]
    public void CreatesRequestedNumberOfTeamsWithDistinctNames()
    {
        var teams = Create();

        Assert.Equal(8, teams.Count);
        Assert.Equal(8, teams.Select(t => t.Name).Distinct().Count());
    }

    [Fact]
    public void EveryTeamHasElevenStartersAndASquadTheMarketWouldAllow()
    {
        // Real squads differ in size, so a seeded one only has to sit within the
        // limits a club is held to once the window opens.
        Assert.All(Create(58), team =>
        {
            Assert.InRange(team.Players.Count, SquadLimits.MinimumSquad, SquadLimits.MaximumSquad);
            Assert.Equal(SquadRules.StarterCount, team.Players.Count(p => p.IsStarter));
        });
    }

    [Fact]
    public void StartingElevenIsOneKeeperAndTenOutfieldPlayers()
    {
        // Line counts are not checked against the shape. A club carries
        // wingers rather than wide midfielders, so the flanks of a midfield
        // five are filled by forwards — as they are in any football game. What
        // each player then costs for the job is covered by the fitness test.
        Assert.All(Create(), team =>
        {
            var starters = team.Players.Where(p => p.IsStarter).ToList();

            Assert.Equal(SquadRules.StarterCount, starters.Count);
            Assert.Equal(1, starters.Count(p => p.Position == Position.GK));
        });
    }

    [Fact]
    public void BenchCoversEveryPositionSoAnySubstitutionIsPossible()
    {
        Assert.All(Create(58), team =>
        {
            var bench = team.Players.Where(p => !p.IsStarter).ToList();

            Assert.All(
                Enum.GetValues<Position>(),
                position => Assert.Contains(bench, p => p.Position == position));
        });
    }

    [Fact]
    public void ShirtNumbersAreUniqueWithinASquad()
    {
        Assert.All(Create(), team =>
            Assert.Equal(team.Players.Count, team.Players.Select(p => p.ShirtNumber).Distinct().Count()));
    }

    [Fact]
    public void PlayerNamesAreUniqueWithinASquad()
    {
        Assert.All(Create(), team =>
            Assert.Equal(team.Players.Count, team.Players.Select(p => p.Name).Distinct().Count()));
    }

    [Fact]
    public void AttributesStayWithinTheRatingScale()
    {
        Assert.All(Create().SelectMany(t => t.Players), player =>
        {
            foreach (var attribute in new[]
                     {
                         player.Pace, player.Shooting, player.Passing,
                         player.Dribbling, player.Defending, player.Physical
                     })
            {
                Assert.InRange(attribute, 1, 99);
            }
        });
    }

    [Fact]
    public void RatingsIgnoreTheBenchSoSubstitutesDoNotDiluteTeamStrength()
    {
        var team = Create(2)[0];
        var benchOnly = new Team
        {
            Id = team.Id,
            SeasonId = team.SeasonId,
            Name = team.Name,
            Players = team.Players.Where(p => p.IsStarter).ToList()
        };

        Assert.Equal(benchOnly.AttackRating, team.AttackRating, 6);
        Assert.Equal(benchOnly.DefenseRating, team.DefenseRating, 6);
        Assert.Equal(benchOnly.TeamOverall, team.TeamOverall, 6);
    }

    [Fact]
    public void AllPlayersBelongToTheirOwnTeamAndSeason()
    {
        var seasonId = Guid.NewGuid();
        var teams = _factory.CreateTeams(seasonId, 8);

        Assert.All(teams, team =>
        {
            Assert.Equal(seasonId, team.SeasonId);
            Assert.All(team.Players, p => Assert.Equal(team.Id, p.TeamId));
        });
    }

    [Fact]
    public void RejectsMoreTeamsThanThereAreClubs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _factory.CreateTeams(Guid.NewGuid(), 99));
    }

    [Fact]
    public void ALeagueListedAheadOfItsPlayersIsNotOfferedToASeason()
    {
        Assert.True(_factory.HasLeague("England"));
        Assert.False(_factory.HasLeague("Italy"));
        Assert.False(_factory.HasLeague("Germany"));
        Assert.Throws<InvalidOperationException>(() => _factory.CreateLeagueTeams(Guid.NewGuid(), "Italy"));
    }

    [Fact]
    public void EverySeededClubLinesUpInAShapeItsSquadIsBuiltFor()
    {
        Assert.All(SquadData.All, squad =>
        {
            Assert.True(
                FormationShape.IsKnown(squad.Formation),
                $"{squad.Name} lines up in '{squad.Formation}', which is not a shape a manager can pick.");
        });
    }

    [Fact]
    public void EveryStarterStandsInOneOfTheElevenSlotsAndEverySubstituteInNone()
    {
        Assert.All(Create(), team =>
        {
            var starters = team.Players.Where(p => p.IsStarter).Select(p => p.LineupSlot).Order();

            Assert.Equal(Enumerable.Range(1, SquadRules.StarterCount), starters);
            Assert.All(team.Players.Where(p => !p.IsStarter), p => Assert.Equal(0, p.LineupSlot));
        });
    }

    [Fact]
    public void NoSeededStarterIsBadlyOutOfPosition()
    {
        // A seeded side may field a winger wide in midfield, or a holder in the
        // centre — those are neighbouring jobs and cost almost nothing. What it
        // must never do is stand a defender up front or a striker at the back.
        Assert.All(Create(58), team =>
        {
            foreach (var player in team.Players.Where(p => p.IsStarter))
            {
                var job = FormationShape.RoleForSlot(team.Formation, player.LineupSlot, player.Role);
                var fitness = RoleFitness.For(player.Role, job);

                Assert.True(
                    fitness >= 0.87,
                    $"{team.Name}: {player.Name} is a {player.Role} playing {job} ({fitness:0.00}).");
            }
        });
    }

    [Fact]
    public void EverySeededClubFieldsARealBackLine()
    {
        Assert.All(Create(58), team =>
        {
            var shape = FormationShape.Of(team.Formation);

            // Every shirt in the back line is worn by someone who plays that
            // job — a back three has no full-backs to check, which is the point
            // of playing one. The line a player is filed under does not matter
            // here: what matters is that nobody is standing in for a defender.
            var back = team.Players
                .Where(p => p.IsStarter && p.LineupSlot >= 1
                            && RoleFitness.LineOf(shape[p.LineupSlot - 1]) == Position.DEF)
                .ToList();

            Assert.Equal(shape.Count(role => RoleFitness.LineOf(role) == Position.DEF), back.Count);

            Assert.All(back, player =>
                Assert.Equal(shape[player.LineupSlot - 1], player.Role));
        });
    }
}
