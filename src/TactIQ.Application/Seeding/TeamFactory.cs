using TactIQ.Application.Transfers;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Seeding;

public class TeamFactory(Random? random = null) : ITeamFactory
{
    private readonly Random _random = random ?? Random.Shared;

    public bool HasLeague(string country) => SquadData.IsLeagueReady(country);

    /// <summary>Every club of one domestic league, ready to be added to a season.</summary>
    public List<Team> CreateLeagueTeams(Guid seasonId, string country)
    {
        var squads = SquadData.ForCountry(country);

        if (squads.Count == 0)
            throw new ArgumentException($"No clubs are available for '{country}'.", nameof(country));

        if (!squads.All(SquadData.IsPlayable))
            throw new InvalidOperationException($"Not every club in '{country}' has a full squad yet.");

        return squads.Select(squad => CreateTeam(seasonId, squad)).ToList();
    }

    public List<Team> CreateTeams(Guid seasonId, int teamCount)
    {
        var available = SquadData.Playable;

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
            LogoUrl = squad.LogoUrl,
            Formation = squad.Formation
        };

        team.Players = squad.Players
            .Select(p => new Player
            {
                Id = Guid.NewGuid(),
                TeamId = team.Id,
                Name = p.Name,
                Position = Enum.Parse<Position>(p.Position),
                Role = Enum.Parse<PlayerRole>(p.Role),
                LineupSlot = p.LineupSlot,
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
                Physical = p.Physical,
                MarketValue = p.MarketValue ?? 0m
            })
            .ToList();

        // A club's spending power follows the squad it already has, so the rich
        // clubs of the strong leagues start the window able to buy and the rest
        // have to sell before they do.
        team.Budget = ClubFinances.StartingBudget(team.Players);

        return team;
    }
}
