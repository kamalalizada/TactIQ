using TactIQ.Domain.Entities;

namespace TactIQ.Application.Statistics;

public interface IStatisticsCalculator
{
    LeagueStatisticsDto Calculate(
        IReadOnlyList<Team> teams,
        IReadOnlyList<Match> matches,
        int formLength = 5,
        int chartSize = 10);

    HeadToHeadSummary HeadToHead(Team teamA, Team teamB, IReadOnlyList<Match> matches);
}
