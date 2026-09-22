using TactIQ.Application.Seeding;
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
    public void EveryTeamHasFullSquadOfElevenStartersAndEightSubstitutes()
    {
        Assert.All(Create(), team =>
        {
            Assert.Equal(SquadRules.SquadSize, team.Players.Count);
            Assert.Equal(SquadRules.StarterCount, team.Players.Count(p => p.IsStarter));
            Assert.Equal(SquadRules.BenchCount, team.Players.Count(p => !p.IsStarter));
        });
    }

    [Fact]
    public void StartingElevenUsesOneKeeperAndCoversEveryOutfieldLine()
    {
        Assert.All(Create(), team =>
        {
            var starters = team.Players.Where(p => p.IsStarter).ToList();

            Assert.Equal(1, starters.Count(p => p.Position == Position.GK));
            Assert.Equal(4, starters.Count(p => p.Position == Position.DEF));
            Assert.Equal(4, starters.Count(p => p.Position == Position.MID));
            Assert.Equal(2, starters.Count(p => p.Position == Position.FWD));
        });
    }

    [Fact]
    public void BenchCoversEveryPositionSoAnySubstitutionIsPossible()
    {
        Assert.All(Create(), team =>
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
            LeagueId = team.LeagueId,
            Name = team.Name,
            Players = team.Players.Where(p => p.IsStarter).ToList()
        };

        Assert.Equal(benchOnly.AttackRating, team.AttackRating, 6);
        Assert.Equal(benchOnly.DefenseRating, team.DefenseRating, 6);
        Assert.Equal(benchOnly.TeamOverall, team.TeamOverall, 6);
    }

    [Fact]
    public void AllPlayersBelongToTheirOwnTeamAndLeague()
    {
        var leagueId = Guid.NewGuid();
        var teams = _factory.CreateTeams(leagueId, 8);

        Assert.All(teams, team =>
        {
            Assert.Equal(leagueId, team.LeagueId);
            Assert.All(team.Players, p => Assert.Equal(team.Id, p.TeamId));
        });
    }

    [Fact]
    public void RejectsMoreTeamsThanThereAreClubs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _factory.CreateTeams(Guid.NewGuid(), 99));
    }
}
