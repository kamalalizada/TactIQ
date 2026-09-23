using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Seeding;

public class TeamFactory(Random? random = null) : ITeamFactory
{
    private readonly Random _random = random ?? Random.Shared;

    public List<Team> CreateTeams(Guid leagueId, int teamCount)
    {
        var available = SquadData.All;

        if (teamCount > available.Count)
            throw new ArgumentOutOfRangeException(nameof(teamCount), $"At most {available.Count} teams are supported.");

        return available
            .OrderBy(_ => _random.Next())
            .Take(teamCount)
            .Select(squad => CreateTeam(leagueId, squad))
            .ToList();
    }

    private static Team CreateTeam(Guid leagueId, Squad squad)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            LeagueId = leagueId,
            Name = squad.Name
        };

        team.Players = squad.Players
            .Select(p => new Player
            {
                Id = Guid.NewGuid(),
                TeamId = team.Id,
                Name = p.Name,
                Position = Enum.Parse<Position>(p.Position),
                ShirtNumber = p.ShirtNumber,
                IsStarter = p.IsStarter,
                Overall = p.Overall,
                Pace = p.Pace,
                Shooting = p.Shooting,
                Passing = p.Passing,
                Dribbling = p.Dribbling,
                Defending = p.Defending,
                Physical = p.Physical
            })
            .ToList();

        return team;
    }
}
