using TactIQ.Domain.Entities;

namespace TactIQ.Application.Seeding;

public interface ITeamFactory
{
    /// <summary>Whether every club of a league has a squad to play with.</summary>
    bool HasLeague(string country);

    /// <summary>Every club of one domestic league.</summary>
    List<Team> CreateLeagueTeams(Guid seasonId, string country);

    /// <summary>An arbitrary set of clubs, used by tests and smaller leagues.</summary>
    List<Team> CreateTeams(Guid seasonId, int teamCount);
}
