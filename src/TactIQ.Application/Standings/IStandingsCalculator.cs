using TactIQ.Domain.Entities;

namespace TactIQ.Application.Standings;

public interface IStandingsCalculator
{
    IReadOnlyList<StandingsRow> Calculate(IEnumerable<Team> teams, IEnumerable<Match> matches);
}
