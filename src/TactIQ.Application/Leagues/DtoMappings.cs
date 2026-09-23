using TactIQ.Application.Standings;
using TactIQ.Domain.Entities;

namespace TactIQ.Application.Leagues;

public static class DtoMappings
{
    public static LeagueSummaryDto ToSummary(this League league) => new(
        league.Id,
        league.SeasonName,
        league.GameMode,
        league.UserTeamId,
        league.TeamCount,
        league.CurrentWeek,
        league.TotalWeeks,
        league.IsFinished,
        league.CreatedAt);

    public static PlayerDto ToDto(this Player player) => new(
        player.Id,
        player.Name,
        player.Position,
        player.ShirtNumber,
        player.Pace,
        player.Shooting,
        player.Passing,
        player.Dribbling,
        player.Defending,
        player.Physical,
        player.Overall,
        player.IsStarter,
        player.Condition,
        player.MatchesInjured,
        player.MatchesSuspended,
        player.YellowCards,
        player.IsAvailable);

    public static TeamDto ToDto(this Team team) => new(
        team.Id,
        team.Name,
        team.LogoUrl,
        Math.Round(team.AttackRating, 1),
        Math.Round(team.DefenseRating, 1),
        Math.Round(team.TeamOverall, 1),
        team.Players.OrderBy(p => p.Position).ThenBy(p => p.ShirtNumber).Select(ToDto).ToList());

    public static MatchEventDto ToDto(this MatchEvent e) =>
        new(e.Minute, e.EventType, e.TeamId, e.Description, e.PlayerId, e.AssistPlayerId);

    public static MatchDto ToDto(this Match match, Guid? userTeamId) => new(
        match.Id,
        match.LeagueId,
        match.Competition,
        match.Round,
        match.Leg,
        match.TieId,
        match.HomePenalties,
        match.AwayPenalties,
        match.WeekNumber,
        match.HomeTeamId,
        match.HomeTeam?.Name ?? string.Empty,
        match.AwayTeamId,
        match.AwayTeam?.Name ?? string.Empty,
        match.HomeGoals,
        match.AwayGoals,
        match.IsPlayed,
        userTeamId is not null && (match.HomeTeamId == userTeamId || match.AwayTeamId == userTeamId),
        match.Events.OrderBy(e => e.Minute).Select(ToDto).ToList());

    public static StandingsRowDto ToDto(this StandingsRow row) => new(
        row.Rank,
        row.TeamId,
        row.TeamName,
        row.Played,
        row.Won,
        row.Drawn,
        row.Lost,
        row.GoalsFor,
        row.GoalsAgainst,
        row.GoalDifference,
        row.Points);
}
