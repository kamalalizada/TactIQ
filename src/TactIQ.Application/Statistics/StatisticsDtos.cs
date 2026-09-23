using TactIQ.Domain.Enums;

namespace TactIQ.Application.Statistics;

public record PlayerStatRow(
    int Rank,
    Guid PlayerId,
    string PlayerName,
    Position Position,
    Guid TeamId,
    string TeamName,
    int Goals,
    int Assists,
    int YellowCards,
    int RedCards,
    int MatchesPlayed);

public record TeamFormRow(
    Guid TeamId,
    string TeamName,
    /// <summary>Most recent result first: "W", "D" or "L".</summary>
    IReadOnlyList<string> Form,
    int GoalsFor,
    int GoalsAgainst,
    int PointsFromForm);

public record HeadToHeadMatch(
    Guid MatchId,
    int WeekNumber,
    string HomeTeamName,
    int HomeGoals,
    int AwayGoals,
    string AwayTeamName);

public record HeadToHeadSummary(
    Guid TeamAId,
    string TeamAName,
    Guid TeamBId,
    string TeamBName,
    int TeamAWins,
    int Draws,
    int TeamBWins,
    int TeamAGoals,
    int TeamBGoals,
    IReadOnlyList<HeadToHeadMatch> Matches);

public record LeagueStatisticsDto(
    IReadOnlyList<PlayerStatRow> TopScorers,
    IReadOnlyList<PlayerStatRow> TopAssists,
    IReadOnlyList<PlayerStatRow> MostBooked,
    IReadOnlyList<TeamFormRow> Form,
    int MatchesPlayed,
    int TotalGoals,
    double GoalsPerMatch,
    int CleanSheets,
    string? BiggestWin);
