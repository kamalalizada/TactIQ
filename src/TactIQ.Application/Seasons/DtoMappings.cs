using TactIQ.Application.Standings;
using TactIQ.Domain.Entities;

namespace TactIQ.Application.Seasons;

public static class DtoMappings
{
    public static CompetitionSummaryDto ToSummary(this Competition competition, string? winnerName = null) => new(
        competition.Id,
        competition.Name,
        competition.Country,
        competition.Kind,
        competition.TeamCount,
        competition.CurrentWeek,
        competition.TotalWeeks,
        competition.CurrentRound,
        competition.WinnerTeamId,
        winnerName,
        competition.IsFinished);

    public static SeasonSummaryDto ToSummary(
        this Season season,
        string? userTeamName,
        IReadOnlyList<CompetitionSummaryDto> competitions) => new(
        season.Id,
        season.Name,
        season.GameMode,
        season.UserTeamId,
        userTeamName,
        competitions.Count > 0 && competitions.All(c => c.IsFinished),
        season.CreatedAt,
        competitions);

    public static PlayerDto ToDto(this Player player) => new(
        player.Id,
        player.Name,
        player.Position,
        player.Role,
        player.ShirtNumber,
        player.Age,
        player.Nationality,
        player.Pace,
        player.Shooting,
        player.Passing,
        player.Dribbling,
        player.Defending,
        player.Physical,
        player.Diving,
        player.Handling,
        player.Kicking,
        player.Reflexes,
        player.Speed,
        player.Positioning,
        player.Overall,
        player.IsStarter,
        player.LineupSlot,
        player.PitchX,
        player.PitchY,
        player.LineupRole,
        player.Condition,
        player.MatchesInjured,
        player.MatchesSuspended,
        player.YellowCards,
        player.IsAvailable);

    public static TeamDto ToDto(this Team team) => new(
        team.Id,
        team.Name,
        team.Country,
        team.LogoUrl,
        team.Formation,
        Math.Round(team.AttackRating, 1),
        Math.Round(team.DefenseRating, 1),
        Math.Round(team.TeamOverall, 1),
        team.Players.OrderBy(p => p.Position).ThenBy(p => p.ShirtNumber).Select(ToDto).ToList());

    public static MatchEventDto ToDto(this MatchEvent e) =>
        new(e.Minute, e.EventType, e.TeamId, e.Description, e.PlayerId, e.AssistPlayerId);

    public static MatchDto ToDto(this Match match, Guid? userTeamId) => new(
        match.Id,
        match.CompetitionId,
        match.Competition?.Name ?? string.Empty,
        match.Competition?.Kind ?? default,
        match.WeekNumber,
        match.GroupName,
        match.Round,
        match.Leg,
        match.TieId,
        match.HomePenalties,
        match.AwayPenalties,
        match.HomeTeamId,
        match.HomeTeam?.Name ?? string.Empty,
        match.HomeTeam?.LogoUrl,
        match.AwayTeamId,
        match.AwayTeam?.Name ?? string.Empty,
        match.AwayTeam?.LogoUrl,
        match.HomeGoals,
        match.AwayGoals,
        match.IsPlayed,
        userTeamId is not null && (match.HomeTeamId == userTeamId || match.AwayTeamId == userTeamId),
        match.Events.OrderBy(e => e.Minute).Select(ToDto).ToList());

    public static StandingsRowDto ToDto(this StandingsRow row, string? logoUrl) => new(
        row.Rank,
        row.TeamId,
        row.TeamName,
        logoUrl,
        row.Played,
        row.Won,
        row.Drawn,
        row.Lost,
        row.GoalsFor,
        row.GoalsAgainst,
        row.GoalDifference,
        row.Points);
}
