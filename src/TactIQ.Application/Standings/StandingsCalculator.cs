using TactIQ.Domain.Entities;

namespace TactIQ.Application.Standings;

public class StandingsCalculator : IStandingsCalculator
{
    public IReadOnlyList<StandingsRow> Calculate(IEnumerable<Team> teams, IEnumerable<Match> matches)
    {
        var played = matches.Where(m => m.IsPlayed).ToList();

        var rows = teams.Select(team =>
        {
            var home = played.Where(m => m.HomeTeamId == team.Id).ToList();
            var away = played.Where(m => m.AwayTeamId == team.Id).ToList();

            var goalsFor = home.Sum(m => m.HomeGoals) + away.Sum(m => m.AwayGoals);
            var goalsAgainst = home.Sum(m => m.AwayGoals) + away.Sum(m => m.HomeGoals);

            var won = home.Count(m => m.HomeGoals > m.AwayGoals) + away.Count(m => m.AwayGoals > m.HomeGoals);
            var drawn = home.Count(m => m.HomeGoals == m.AwayGoals) + away.Count(m => m.AwayGoals == m.HomeGoals);
            var lost = home.Count + away.Count - won - drawn;

            return new StandingsRow(0, team.Id, team.Name, home.Count + away.Count, won, drawn, lost, goalsFor, goalsAgainst);
        });

        return rows
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.GoalDifference)
            .ThenByDescending(r => r.GoalsFor)
            .ThenBy(r => r.TeamName, StringComparer.OrdinalIgnoreCase)
            .Select((r, i) => r with { Rank = i + 1 })
            .ToList();
    }
}
