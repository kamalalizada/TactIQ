using TactIQ.Domain.Entities;

namespace TactIQ.Application.Seeding;

public interface ITeamFactory
{
    List<Team> CreateTeams(Guid leagueId, int teamCount);
}
