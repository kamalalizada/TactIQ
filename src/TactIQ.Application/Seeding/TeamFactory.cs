using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Seeding;

public class TeamFactory(Random? random = null) : ITeamFactory
{
    private readonly Random _random = random ?? Random.Shared;

    /// <summary>Every club of one domestic league, ready to be added to a season.</summary>
    public List<Team> CreateLeagueTeams(Guid seasonId, string country)
    {
        var squads = SquadData.ForCountry(country);

        if (squads.Count == 0)
            throw new ArgumentException($"No clubs are available for '{country}'.", nameof(country));

        return squads.Select(squad => CreateTeam(seasonId, squad)).ToList();
    }

    public List<Team> CreateTeams(Guid seasonId, int teamCount)
    {
        var available = SquadData.All;

        if (teamCount > available.Count)
            throw new ArgumentOutOfRangeException(nameof(teamCount), $"At most {available.Count} teams are supported.");

        return available
            .OrderBy(_ => _random.Next())
            .Take(teamCount)
            .Select(squad => CreateTeam(seasonId, squad))
            .ToList();
    }

    private static Team CreateTeam(Guid seasonId, Squad squad)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            Name = squad.Name,
            Country = squad.Country,
            LogoUrl = squad.LogoUrl
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
                Age = p.Age,
                Nationality = p.Nationality,
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
