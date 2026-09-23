using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Statistics;

public class StatisticsCalculator : IStatisticsCalculator
{
    public LeagueStatisticsDto Calculate(
        IReadOnlyList<Team> teams,
        IReadOnlyList<Match> matches,
        int formLength = 5,
        int chartSize = 10)
    {
        var played = matches.Where(m => m.IsPlayed).ToList();
        var tallies = BuildTallies(teams, played);

        var scorers = Rank(tallies, t => t.Goals, chartSize);
        var assists = Rank(tallies, t => t.Assists, chartSize);
        var booked = Rank(tallies, t => t.YellowCards * 2 + t.RedCards * 5, chartSize);

        var totalGoals = played.Sum(m => m.HomeGoals + m.AwayGoals);
        var cleanSheets = played.Count(m => m.HomeGoals == 0) + played.Count(m => m.AwayGoals == 0);

        return new LeagueStatisticsDto(
            scorers,
            assists,
            booked,
            BuildForm(teams, played, formLength),
            played.Count,
            totalGoals,
            played.Count == 0 ? 0 : Math.Round(totalGoals / (double)played.Count, 2),
            cleanSheets,
            DescribeBiggestWin(played));
    }

    public HeadToHeadSummary HeadToHead(Team teamA, Team teamB, IReadOnlyList<Match> matches)
    {
        var meetings = matches
            .Where(m => m.IsPlayed)
            .Where(m => (m.HomeTeamId == teamA.Id && m.AwayTeamId == teamB.Id)
                     || (m.HomeTeamId == teamB.Id && m.AwayTeamId == teamA.Id))
            .OrderBy(m => m.WeekNumber)
            .ToList();

        int aWins = 0, bWins = 0, draws = 0, aGoals = 0, bGoals = 0;

        foreach (var m in meetings)
        {
            var (forA, forB) = m.HomeTeamId == teamA.Id
                ? (m.HomeGoals, m.AwayGoals)
                : (m.AwayGoals, m.HomeGoals);

            aGoals += forA;
            bGoals += forB;

            if (forA > forB) aWins++;
            else if (forB > forA) bWins++;
            else draws++;
        }

        return new HeadToHeadSummary(
            teamA.Id, teamA.Name,
            teamB.Id, teamB.Name,
            aWins, draws, bWins,
            aGoals, bGoals,
            meetings.Select(m => new HeadToHeadMatch(
                m.Id,
                m.WeekNumber,
                m.HomeTeam?.Name ?? string.Empty,
                m.HomeGoals,
                m.AwayGoals,
                m.AwayTeam?.Name ?? string.Empty)).ToList());
    }

    private static List<Tally> BuildTallies(IReadOnlyList<Team> teams, List<Match> played)
    {
        var byPlayer = teams
            .SelectMany(t => t.Players.Select(p => (Player: p, Team: t)))
            .ToDictionary(x => x.Player.Id, x => new Tally(x.Player, x.Team));

        foreach (var match in played)
        {
            foreach (var e in match.Events)
            {
                if (e.PlayerId is { } playerId && byPlayer.TryGetValue(playerId, out var tally))
                {
                    switch (e.EventType)
                    {
                        case MatchEventType.Goal: tally.Goals++; break;
                        case MatchEventType.YellowCard: tally.YellowCards++; break;
                        case MatchEventType.RedCard: tally.RedCards++; break;
                    }
                }

                if (e.AssistPlayerId is { } assistId && byPlayer.TryGetValue(assistId, out var assister))
                    assister.Assists++;
            }

            // Anyone in the squad of a team that played is counted as available for
            // that match; the engine does not record who was actually on the pitch.
            foreach (var teamId in new[] { match.HomeTeamId, match.AwayTeamId })
            {
                foreach (var tally in byPlayer.Values.Where(t => t.Team.Id == teamId))
                    tally.MatchesPlayed++;
            }
        }

        return byPlayer.Values.ToList();
    }

    private static IReadOnlyList<PlayerStatRow> Rank(
        List<Tally> tallies,
        Func<Tally, int> score,
        int size) =>
        tallies
            .Where(t => score(t) > 0)
            .OrderByDescending(score)
            .ThenByDescending(t => t.Goals)
            .ThenBy(t => t.Player.Name, StringComparer.OrdinalIgnoreCase)
            .Take(size)
            .Select((t, i) => new PlayerStatRow(
                i + 1,
                t.Player.Id,
                t.Player.Name,
                t.Player.Position,
                t.Team.Id,
                t.Team.Name,
                t.Goals,
                t.Assists,
                t.YellowCards,
                t.RedCards,
                t.MatchesPlayed))
            .ToList();

    private static IReadOnlyList<TeamFormRow> BuildForm(
        IReadOnlyList<Team> teams,
        List<Match> played,
        int formLength) =>
        teams.Select(team =>
        {
            var recent = played
                .Where(m => m.HomeTeamId == team.Id || m.AwayTeamId == team.Id)
                .OrderByDescending(m => m.WeekNumber)
                .Take(formLength)
                .ToList();

            var form = new List<string>();
            int scored = 0, conceded = 0, points = 0;

            foreach (var m in recent)
            {
                var (forTeam, against) = m.HomeTeamId == team.Id
                    ? (m.HomeGoals, m.AwayGoals)
                    : (m.AwayGoals, m.HomeGoals);

                scored += forTeam;
                conceded += against;

                if (forTeam > against) { form.Add("W"); points += 3; }
                else if (forTeam == against) { form.Add("D"); points += 1; }
                else form.Add("L");
            }

            return new TeamFormRow(team.Id, team.Name, form, scored, conceded, points);
        })
        .OrderByDescending(f => f.PointsFromForm)
        .ThenByDescending(f => f.GoalsFor - f.GoalsAgainst)
        .ToList();

    private static string? DescribeBiggestWin(List<Match> played)
    {
        var biggest = played
            .Where(m => m.HomeGoals != m.AwayGoals)
            .OrderByDescending(m => Math.Abs(m.HomeGoals - m.AwayGoals))
            .ThenByDescending(m => m.HomeGoals + m.AwayGoals)
            .FirstOrDefault();

        return biggest is null
            ? null
            : $"{biggest.HomeTeam?.Name} {biggest.HomeGoals}-{biggest.AwayGoals} {biggest.AwayTeam?.Name}";
    }

    private sealed class Tally(Player player, Team team)
    {
        public Player Player { get; } = player;
        public Team Team { get; } = team;
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public int MatchesPlayed { get; set; }
    }
}
