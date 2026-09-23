using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Scheduling;
using TactIQ.Application.Seeding;
using TactIQ.Application.Simulation;
using TactIQ.Application.Standings;
using TactIQ.Application.Statistics;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Leagues;

public class LeagueService(
    IAppDbContext db,
    IScheduleGenerator scheduleGenerator,
    ITeamFactory teamFactory,
    IMatchEngine matchEngine,
    IStandingsCalculator standingsCalculator,
    IStatisticsCalculator statisticsCalculator) : ILeagueService
{
    public const int DefaultTeamCount = 8;

    public async Task<LeagueSummaryDto> CreateAsync(string userId, CreateLeagueRequest request, CancellationToken ct = default)
    {
        var league = new League
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SeasonName = request.SeasonName,
            TeamCount = DefaultTeamCount,
            GameMode = request.GameMode,
            CurrentWeek = 1,
            CreatedAt = DateTime.UtcNow
        };

        var teams = teamFactory.CreateTeams(league.Id, league.TeamCount);

        if (request.GameMode == GameMode.ManagerMode)
        {
            league.UserTeamId = request.UserTeamId is { } requested && teams.Any(t => t.Id == requested)
                ? requested
                : teams[Random.Shared.Next(teams.Count)].Id;
        }

        league.Teams = teams;
        league.Matches = scheduleGenerator
            .Generate(teams.Select(t => t.Id).ToList())
            .Select(f => new Match
            {
                Id = Guid.NewGuid(),
                LeagueId = league.Id,
                WeekNumber = f.WeekNumber,
                HomeTeamId = f.HomeTeamId,
                AwayTeamId = f.AwayTeamId
            })
            .ToList();

        db.Leagues.Add(league);
        await db.SaveChangesAsync(ct);

        return league.ToSummary();
    }

    public async Task<IReadOnlyList<LeagueSummaryDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        var leagues = await db.Leagues
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        return leagues.Select(l => l.ToSummary()).ToList();
    }

    public async Task<LeagueSummaryDto?> GetAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await OwnedLeagues(userId).AsNoTracking().FirstOrDefaultAsync(l => l.Id == leagueId, ct);
        return league?.ToSummary();
    }

    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        await EnsureOwnedAsync(userId, leagueId, ct);

        var teams = await db.Teams
            .AsNoTracking()
            .Include(t => t.Players)
            .Where(t => t.LeagueId == leagueId)
            .ToListAsync(ct);

        return teams.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<StandingsRowDto>> GetStandingsAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        await EnsureOwnedAsync(userId, leagueId, ct);

        var teams = await db.Teams.AsNoTracking().Where(t => t.LeagueId == leagueId).ToListAsync(ct);
        // Cup results never count towards the league table.
        var matches = await db.Matches
            .AsNoTracking()
            .Where(m => m.LeagueId == leagueId && m.Competition == CompetitionType.League)
            .ToListAsync(ct);

        return standingsCalculator.Calculate(teams, matches).Select(r => r.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<MatchDto>> GetFixturesAsync(string userId, Guid leagueId, int? week, CancellationToken ct = default)
    {
        var league = await EnsureOwnedAsync(userId, leagueId, ct);

        var query = db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Events)
            .Where(m => m.LeagueId == leagueId && m.Competition == CompetitionType.League);

        if (week is { } w)
            query = query.Where(m => m.WeekNumber == w);

        var matches = await query
            .OrderBy(m => m.WeekNumber)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);

        return matches.Select(m => m.ToDto(league.UserTeamId)).ToList();
    }

    public async Task<SimulateWeekResult> SimulateWeekAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await EnsureOwnedAsync(userId, leagueId, ct);

        if (league.IsFinished)
            throw new InvalidOperationException("The season is already finished.");

        var week = league.CurrentWeek;

        var matches = await db.Matches
            .Include(m => m.HomeTeam).ThenInclude(t => t.Players)
            .Include(m => m.AwayTeam).ThenInclude(t => t.Players)
            .Include(m => m.Events)
            .Where(m => m.LeagueId == leagueId
                     && m.Competition == CompetitionType.League
                     && m.WeekNumber == week)
            .ToListAsync(ct);

        var simulated = new List<Match>();
        Match? pendingUserMatch = null;

        foreach (var match in matches)
        {
            if (IsUserMatch(match, league.UserTeamId))
            {
                // Left unplayed on purpose: the manager plays this one live (Stage 4).
                pendingUserMatch = match;
                continue;
            }

            if (match.IsPlayed)
            {
                simulated.Add(match);
                continue;
            }

            ApplyResult(match, matchEngine.SimulateInstant(match.HomeTeam, match.AwayTeam));
            simulated.Add(match);
        }

        // The week only advances once every match in it is settled.
        var weekComplete = pendingUserMatch is null || pendingUserMatch.IsPlayed;
        if (weekComplete)
        {
            league.CurrentWeek = week + 1;

            // Bans and knocks tick down, and everyone gets some condition back.
            var squads = await db.Players
                .Where(p => db.Teams.Any(t => t.Id == p.TeamId && t.LeagueId == leagueId))
                .ToListAsync(ct);

            SquadAftermath.RecoverWeek(squads);
        }

        await db.SaveChangesAsync(ct);

        return new SimulateWeekResult(
            week,
            simulated.Select(m => m.ToDto(league.UserTeamId)).ToList(),
            pendingUserMatch?.IsPlayed == false ? pendingUserMatch.ToDto(league.UserTeamId) : null,
            league.CurrentWeek,
            league.IsFinished);
    }

    public async Task<LeagueStatisticsDto> GetStatisticsAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        await EnsureOwnedAsync(userId, leagueId, ct);

        var teams = await LoadSquadsAsync(leagueId, ct);
        var matches = await LoadPlayedMatchesAsync(leagueId, ct);

        return statisticsCalculator.Calculate(teams, matches);
    }

    public async Task<HeadToHeadSummary> GetHeadToHeadAsync(
        string userId,
        Guid leagueId,
        Guid teamAId,
        Guid teamBId,
        CancellationToken ct = default)
    {
        await EnsureOwnedAsync(userId, leagueId, ct);

        var teams = await db.Teams
            .AsNoTracking()
            .Where(t => t.LeagueId == leagueId && (t.Id == teamAId || t.Id == teamBId))
            .ToListAsync(ct);

        var teamA = teams.FirstOrDefault(t => t.Id == teamAId)
                    ?? throw new KeyNotFoundException($"Team {teamAId} was not found.");
        var teamB = teams.FirstOrDefault(t => t.Id == teamBId)
                    ?? throw new KeyNotFoundException($"Team {teamBId} was not found.");

        var matches = await LoadPlayedMatchesAsync(leagueId, ct);
        return statisticsCalculator.HeadToHead(teamA, teamB, matches);
    }

    private async Task<List<Team>> LoadSquadsAsync(Guid leagueId, CancellationToken ct) =>
        await db.Teams
            .AsNoTracking()
            .Include(t => t.Players)
            .Where(t => t.LeagueId == leagueId)
            .ToListAsync(ct);

    private async Task<List<Match>> LoadPlayedMatchesAsync(Guid leagueId, CancellationToken ct) =>
        await db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Events)
            .Where(m => m.LeagueId == leagueId && m.IsPlayed)
            .ToListAsync(ct);

    public async Task<bool> DeleteAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await OwnedLeagues(userId).FirstOrDefaultAsync(l => l.Id == leagueId, ct);
        if (league is null)
            return false;

        db.Leagues.Remove(league);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static bool IsUserMatch(Match match, Guid? userTeamId) =>
        userTeamId is not null && (match.HomeTeamId == userTeamId || match.AwayTeamId == userTeamId);

    private static void ApplyResult(Match match, MatchSimulationResult result)
    {
        match.HomeGoals = result.HomeGoals;
        match.AwayGoals = result.AwayGoals;
        match.IsPlayed = true;

        foreach (var e in result.Events)
        {
            // Id and MatchId are left to EF: assigning them makes the change tracker
            // treat these new rows as existing ones and emit UPDATEs instead of INSERTs.
            match.Events.Add(new MatchEvent
            {
                Minute = e.Minute,
                EventType = e.EventType,
                TeamId = e.TeamId,
                PlayerId = e.PlayerId,
                AssistPlayerId = e.AssistPlayerId,
                Description = e.Description
            });
        }

        SquadAftermath.Apply(
            match.HomeTeam.Players.Concat(match.AwayTeam.Players),
            result.Aftermath);
    }

    private IQueryable<League> OwnedLeagues(string userId) =>
        db.Leagues.Where(l => l.UserId == userId);

    private async Task<League> EnsureOwnedAsync(string userId, Guid leagueId, CancellationToken ct)
    {
        var league = await OwnedLeagues(userId).FirstOrDefaultAsync(l => l.Id == leagueId, ct);
        return league ?? throw new KeyNotFoundException($"League {leagueId} was not found.");
    }
}
