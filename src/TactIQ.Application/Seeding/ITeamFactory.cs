using TactIQ.Domain.Entities;

namespace TactIQ.Application.Seeding;

public interface ITeamFactory
{
    /// <summary>Every club of one domestic league.</summary>
    List<Team> CreateLeagueTeams(Guid seasonId, string country);

    /// <summary>An arbitrary set of clubs, used by tests and smaller leagues.</summary>
    List<Team> CreateTeams(Guid seasonId, int teamCount);
}
