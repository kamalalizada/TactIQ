using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Scheduling;
using TactIQ.Application.Seeding;
using TactIQ.Application.Simulation;
using TactIQ.Application.Standings;
using TactIQ.Application.Finances;
using TactIQ.Application.Statistics;
using TactIQ.Application.Transfers;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Seasons;

public class SeasonService(
    IAppDbContext db,
    IScheduleGenerator scheduleGenerator,
    ITeamFactory teamFactory,
    IMatchEngine matchEngine,
    IStandingsCalculator standingsCalculator,
    IStatisticsCalculator statisticsCalculator) : ISeasonService
{
    /// <summary>The domestic leagues every season is made up of.</summary>
    private static readonly (string Country, string Name)[] Leagues =
    [
        ("England", "Premier League"),
        ("Spain", "LaLiga"),
        ("Turkey", "Super Lig")
    ];

    public async Task<SeasonSummaryDto> CreateAsync(string userId, CreateSeasonRequest request, CancellationToken ct = default)
    {
        var season = new Season
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            GameMode = request.GameMode,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (country, name) in Leagues)
        {
            var teams = teamFactory.CreateLeagueTeams(season.Id, country);
            season.Teams.AddRange(teams);

            var competition = new Competition
            {
                Id = Guid.NewGuid(),
                SeasonId = season.Id,
                Name = name,
                Country = country,
                Kind = CompetitionKind.League,
                TeamCount = teams.Count,
                CurrentWeek = 1
            };

            competition.Entries = teams
                .Select(t => new CompetitionEntry { CompetitionId = competition.Id, TeamId = t.Id })
                .ToList();

            competition.Matches = scheduleGenerator
                .Generate(teams.Select(t => t.Id).ToList())
                .Select(f => new Match
                {
                    CompetitionId = competition.Id,
                    WeekNumber = f.WeekNumber,
                    HomeTeamId = f.HomeTeamId,
                    AwayTeamId = f.AwayTeamId
                })
                .ToList();

            season.Competitions.Add(competition);
        }

        if (request.GameMode == GameMode.ManagerMode)
        {
            season.UserTeamId = request.UserTeamId is { } requested && season.Teams.Any(t => t.Id == requested)
                ? requested
                : season.Teams[Random.Shared.Next(season.Teams.Count)].Id;
        }

        db.Seasons.Add(season);
        await db.SaveChangesAsync(ct);

        return await BuildSummaryAsync(season, ct);
    }

    public async Task<IReadOnlyList<SeasonSummaryDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        var seasons = await db.Seasons
            .AsNoTracking()
            .Include(s => s.Competitions)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var summaries = new List<SeasonSummaryDto>(seasons.Count);
        foreach (var season in seasons)
            summaries.Add(await BuildSummaryAsync(season, ct));

        return summaries;
    }

    public async Task<SeasonSummaryDto?> GetAsync(string userId, Guid seasonId, CancellationToken ct = default)
    {
        var season = await db.Seasons
            .AsNoTracking()
            .Include(s => s.Competitions)
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.UserId == userId, ct);

        return season is null ? null : await BuildSummaryAsync(season, ct);
    }

    public async Task<SeasonSummaryDto> SetUserTeamAsync(
        string userId,
        Guid seasonId,
        Guid teamId,
        CancellationToken ct = default)
    {
        var season = await EnsureSeasonAsync(userId, seasonId, tracked: true, ct);

        if (season.GameMode != GameMode.ManagerMode)
            throw new InvalidOperationException("Only a Manager Mode season has a club to pick.");

        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId && t.SeasonId == seasonId, ct)
            ?? throw new KeyNotFoundException($"Team {teamId} is not part of this season.");

        // Switching after kick-off would leave played matches credited to a club
        // the manager no longer runs, so the choice is locked once a week is in.
        var alreadyStarted = await db.Matches
            .AnyAsync(m => m.Competition.SeasonId == seasonId && m.IsPlayed, ct);

        if (alreadyStarted)
            throw new InvalidOperationException("The club can only be chosen before the season starts.");

        season.UserTeamId = team.Id;
        await db.SaveChangesAsync(ct);

        return await BuildSummaryAsync(season, ct);
    }

    public async Task<TeamDto> SetLineupAsync(
        string userId,
        Guid seasonId,
        string formation,
        IReadOnlyList<LineupSpotRequest> starters,
        CancellationToken ct = default)
    {
        var season = await EnsureSeasonAsync(userId, seasonId, tracked: true, ct);

        if (season.UserTeamId is not { } teamId)
            throw new InvalidOperationException("Only a managed club has a line-up to set.");

        if (!FormationRules.IsKnown(formation))
            throw new ArgumentException($"Unknown formation '{formation}'.", nameof(formation));

        var starterIds = starters.Select(s => s.PlayerId).ToList();

        if (starterIds.Distinct().Count() != FormationRules.StarterCount
            || starters.Count != FormationRules.StarterCount)
            throw new InvalidOperationException(
                $"A line-up needs exactly {FormationRules.StarterCount} different players.");

        if (starters.Any(s => !PitchZones.IsOnPitch(s.X, s.Y)))
            throw new ArgumentException("Every starter has to stand on the pitch.", nameof(starters));

        // The spot decides the job, so this is where a side with two keepers,
        // two left-backs or a back line of two is turned away.
        var roles = starters.ToDictionary(s => s.PlayerId, s => PitchZones.RoleAt(s.X, s.Y));

        if (LineupRules.Problem(roles.Values) is { } problem)
            throw new InvalidOperationException(problem);

        var team = await db.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Id == teamId && t.SeasonId == seasonId, ct)
            ?? throw new KeyNotFoundException("The managed club was not found in this season.");

        var chosen = team.Players.Where(p => starterIds.Contains(p.Id)).ToList();

        if (chosen.Count != FormationRules.StarterCount)
            throw new InvalidOperationException("Every starter must belong to your squad.");

        if (chosen.FirstOrDefault(p => !p.IsAvailable) is { } unavailable)
            throw new InvalidOperationException($"{unavailable.Name} is not available to play.");

        // Anyone may stand anywhere the rules above allow. A centre-back pushed
        // into midfield plays there, below his rating; that cost is the
        // punishment, rather than refusing the team sheet outright.
        //
        // The slot is still kept in the order the ids arrived, for anything
        // that reads the line-up by shirt rather than by spot.
        var spots = starters.ToDictionary(s => s.PlayerId);

        foreach (var player in team.Players)
        {
            if (!spots.TryGetValue(player.Id, out var spot))
            {
                player.LeaveLineup();
                continue;
            }

            player.IsStarter = true;
            player.LineupSlot = starterIds.IndexOf(player.Id) + 1;
            player.PitchX = spot.X;
            player.PitchY = spot.Y;
            player.LineupRole = roles[player.Id];
        }

        team.Formation = formation;
        await db.SaveChangesAsync(ct);

        return team.ToDto();
    }

    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(string userId, Guid seasonId, string? country, CancellationToken ct = default)
    {
        await EnsureSeasonAsync(userId, seasonId, ct);

        var query = db.Teams
            .AsNoTracking()
            .Include(t => t.Players)
            .Where(t => t.SeasonId == seasonId);

        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(t => t.Country == country);

        var teams = await query.ToListAsync(ct);

        return teams
            .OrderBy(t => t.Country)
            .ThenBy(t => t.Name)
            .Select(t => t.ToDto())
            .ToList();
    }

    public async Task<IReadOnlyList<StandingsRowDto>> GetStandingsAsync(string userId, Guid competitionId, CancellationToken ct = default)
    {
        var competition = await OwnedCompetitionAsync(userId, competitionId, ct);

        var teams = await EntrantsAsync(competition, ct);
        var matches = await db.Matches
            .AsNoTracking()
            .Where(m => m.CompetitionId == competitionId)
            .ToListAsync(ct);

        var logos = teams.ToDictionary(t => t.Id, t => t.LogoUrl);

        return standingsCalculator
            .Calculate(teams, matches)
            .Select(r => r.ToDto(logos.GetValueOrDefault(r.TeamId)))
            .ToList();
    }

    public async Task<IReadOnlyList<MatchDto>> GetFixturesAsync(string userId, Guid competitionId, int? week, CancellationToken ct = default)
    {
        var competition = await OwnedCompetitionAsync(userId, competitionId, ct);

        var query = db.Matches
            .AsNoTracking()
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Events)
            .Where(m => m.CompetitionId == competitionId);

        if (week is { } w)
            query = query.Where(m => m.WeekNumber == w);

        var matches = await query.OrderBy(m => m.WeekNumber).ThenBy(m => m.Id).ToListAsync(ct);
        var userTeamId = await UserTeamIdAsync(competition.SeasonId, ct);

        return matches.Select(m => m.ToDto(userTeamId)).ToList();
    }

    public async Task<SimulateWeekResult> SimulateWeekAsync(
        string userId,
        Guid competitionId,
        bool includeUserMatch = false,
        CancellationToken ct = default)
    {
        var competition = await OwnedCompetitionAsync(userId, competitionId, tracked: true, ct);

        if (competition.Kind != CompetitionKind.League)
            throw new InvalidOperationException("Only a domestic league is played week by week.");

        if (competition.IsFinished)
            throw new InvalidOperationException("This league has already finished.");

        var userTeamId = await UserTeamIdAsync(competition.SeasonId, ct);
        var result = await PlayWeekAsync(competition, userTeamId, includeUserMatch, ct);

        await PayIfFinishedAsync(competition, ct);
        await db.SaveChangesAsync(ct);

        var season = await EnsureSeasonAsync(userId, competition.SeasonId, tracked: true, ct);
        await AdvanceSeasonWeekAsync(season, ct);

        return result;
    }

    public async Task<IReadOnlyList<SimulateWeekResult>> SimulateAllLeaguesAsync(
        string userId,
        Guid seasonId,
        bool includeUserMatch = false,
        CancellationToken ct = default)
    {
        var season = await EnsureSeasonAsync(userId, seasonId, tracked: true, ct);

        var leagues = await db.Competitions
            .Where(c => c.SeasonId == seasonId && c.Kind == CompetitionKind.League)
            .ToListAsync(ct);

        var results = new List<SimulateWeekResult>();

        foreach (var competition in leagues.Where(c => !c.IsFinished))
            results.Add(await PlayWeekAsync(competition, season.UserTeamId, includeUserMatch, ct));

        foreach (var competition in leagues)
            await PayIfFinishedAsync(competition, ct);

        await db.SaveChangesAsync(ct);
        await AdvanceSeasonWeekAsync(season, ct);

        return results;
    }

    /// <summary>
    /// Hands out the end-of-season money the week a league finishes. It is
    /// safe to call on a league that has already been paid, which is what lets
    /// it sit on the simulation path without a check at every call site.
    /// </summary>
    private Task PayIfFinishedAsync(Competition competition, CancellationToken ct) =>
        new SeasonPayout(db, standingsCalculator).PayLeagueAsync(competition, ct);

    /// <summary>
    /// Moves the save on a week and lets the market run.
    ///
    /// The season carries its own week because the transfer window is one thing
    /// for the whole save: a manager following three leagues at once should not
    /// find the market open in one of them and shut in another.
    /// </summary>
    private async Task AdvanceSeasonWeekAsync(Season season, CancellationToken ct)
    {
        season.CurrentWeek++;

        var totalWeeks = await db.Competitions
            .Where(c => c.SeasonId == season.Id && c.Kind == CompetitionKind.League)
            .Select(c => c.TeamCount > 1 ? 2 * (c.TeamCount - 1) : 0)
            .FirstOrDefaultAsync(ct);

        await db.SaveChangesAsync(ct);

        await new TransferMarketRunner(db)
            .RunWeekAsync(season, totalWeeks, Random.Shared, ct);
    }

    /// <summary>How many of each kind a search returns; a longer list is noise.</summary>
    private const int SearchLimit = 6;

    public async Task<SearchResultDto> SearchAsync(
        string userId, Guid seasonId, string query, CancellationToken ct = default)
    {
        var season = await EnsureSeasonAsync(userId, seasonId, ct);

        var term = query?.Trim() ?? string.Empty;

        // A single letter matches half the game, which is no use to anybody.
        if (term.Length < 2)
            return new SearchResultDto([], []);

        var lowered = term.ToLowerInvariant();

        var teams = await db.Teams
            .AsNoTracking()
            .Include(t => t.Players)
            .Where(t => t.SeasonId == seasonId && t.Name.ToLower().Contains(lowered))
            .OrderBy(t => t.Name)
            .Take(SearchLimit)
            .ToListAsync(ct);

        var players = await db.Players
            .AsNoTracking()
            .Include(p => p.Team)
            .Where(p => p.Team.SeasonId == seasonId && p.Name.ToLower().Contains(lowered))
            // The best match first: a manager searching a name wants the player
            // he has heard of, not the reserve who shares a surname.
            .OrderByDescending(p => p.Overall)
            .Take(SearchLimit)
            .ToListAsync(ct);

        return new SearchResultDto(
            teams
                .Select(t => new TeamHitDto(t.Id, t.Name, t.Country, t.LogoUrl, t.TeamOverall))
                .ToList(),
            players
                .Select(p => new PlayerHitDto(
                    p.Id, p.Name, p.Position, p.Role, p.Age, p.Overall,
                    p.TeamId, p.Team.Name, p.Team.LogoUrl,
                    p.TeamId == season.UserTeamId))
                .ToList());
    }

    public async Task<LeagueStatisticsDto> GetStatisticsAsync(string userId, Guid seasonId, Guid? competitionId, CancellationToken ct = default)
    {
        await EnsureSeasonAsync(userId, seasonId, ct);

        var teamQuery = db.Teams
            .AsNoTracking()
            .Include(t => t.Players)
            .Where(t => t.SeasonId == seasonId);

        var matchQuery = db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Events)
            .Where(m => m.Competition.SeasonId == seasonId && m.IsPlayed);

        if (competitionId is { } id)
        {
            // Narrow the squads too, or players from other leagues appear in the
            // charts on nothing but an appearance count.
            teamQuery = teamQuery.Where(t => db.CompetitionEntries.Any(e => e.CompetitionId == id && e.TeamId == t.Id));
            matchQuery = matchQuery.Where(m => m.CompetitionId == id);
        }

        return statisticsCalculator.Calculate(
            await teamQuery.ToListAsync(ct),
            await matchQuery.ToListAsync(ct));
    }

    public async Task<HeadToHeadSummary> GetHeadToHeadAsync(string userId, Guid seasonId, Guid teamAId, Guid teamBId, CancellationToken ct = default)
    {
        await EnsureSeasonAsync(userId, seasonId, ct);

        var teams = await db.Teams
            .AsNoTracking()
            .Where(t => t.SeasonId == seasonId && (t.Id == teamAId || t.Id == teamBId))
            .ToListAsync(ct);

        var teamA = teams.FirstOrDefault(t => t.Id == teamAId)
                    ?? throw new KeyNotFoundException($"Team {teamAId} was not found.");
        var teamB = teams.FirstOrDefault(t => t.Id == teamBId)
                    ?? throw new KeyNotFoundException($"Team {teamBId} was not found.");

        var matches = await db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.Competition.SeasonId == seasonId && m.IsPlayed)
            .ToListAsync(ct);

        return statisticsCalculator.HeadToHead(teamA, teamB, matches);
    }

    public async Task<bool> DeleteAsync(string userId, Guid seasonId, CancellationToken ct = default)
    {
        var season = await db.Seasons.FirstOrDefaultAsync(s => s.Id == seasonId && s.UserId == userId, ct);
        if (season is null)
            return false;

        db.Seasons.Remove(season);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Plays every fixture of the competition's current week, leaving the
    /// manager's own match for them to play live.</summary>
    private async Task<SimulateWeekResult> PlayWeekAsync(
        Competition competition,
        Guid? userTeamId,
        bool includeUserMatch,
        CancellationToken ct)
    {
        var week = competition.CurrentWeek;

        var matches = await db.Matches
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam).ThenInclude(t => t.Players)
            .Include(m => m.AwayTeam).ThenInclude(t => t.Players)
            .Include(m => m.Events)
            .Where(m => m.CompetitionId == competition.Id && m.WeekNumber == week)
            .ToListAsync(ct);

        var simulated = new List<Match>();
        Match? pendingUserMatch = null;

        foreach (var match in matches)
        {
            // The manager's own match is normally left for them to play out
            // live; when they ask for it to be settled, it is simulated too.
            if (IsUserMatch(match, userTeamId) && !includeUserMatch)
            {
                pendingUserMatch = match;
                continue;
            }

            if (!match.IsPlayed)
                ApplyResult(match, matchEngine.SimulateInstant(match.HomeTeam, match.AwayTeam));

            simulated.Add(match);
        }

        var weekComplete = pendingUserMatch is null || pendingUserMatch.IsPlayed;
        if (weekComplete)
        {
            competition.CurrentWeek = week + 1;
            await RecoverSquadsAsync(competition, ct);
        }

        return new SimulateWeekResult(
            competition.Id,
            week,
            simulated.Select(m => m.ToDto(userTeamId)).ToList(),
            pendingUserMatch?.IsPlayed == false ? pendingUserMatch.ToDto(userTeamId) : null,
            competition.CurrentWeek,
            competition.IsFinished);
    }

    /// <summary>
    /// Ticks down bans and knocks and gives everyone some condition back, for
    /// the clubs that just played.
    ///
    /// Only this competition's clubs: a season runs three leagues, and
    /// recovering all of them each time a league played a week healed every
    /// injury three times over. A four-match knock was gone before the manager
    /// saw it.
    /// </summary>
    private async Task RecoverSquadsAsync(Competition competition, CancellationToken ct)
    {
        var squads = await db.Players
            .Where(p => db.CompetitionEntries
                .Any(e => e.CompetitionId == competition.Id && e.TeamId == p.TeamId))
            .ToListAsync(ct);

        SquadAftermath.RecoverWeek(squads);
    }

    private static bool IsUserMatch(Match match, Guid? userTeamId) =>
        userTeamId is not null && (match.HomeTeamId == userTeamId || match.AwayTeamId == userTeamId);

    internal static void ApplyResult(Match match, MatchSimulationResult result)
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

        PayResult(match);
    }

    /// <summary>
    /// Pays both clubs what the result was worth.
    ///
    /// Only league matches pay per result: a cup run is paid for the round it
    /// reaches, not match by match, so a club cannot earn twice for the same
    /// tie. The money goes in as the match is settled, wherever it was settled
    /// from — a simulated week, a cup round, or the manager's own live match.
    /// </summary>
    public static void PayResult(Match match)
    {
        if (match.Competition is not { Kind: CompetitionKind.League } league)
            return;

        match.HomeTeam.Budget += PrizeMoney.ForResult(
            league.Country, match.HomeGoals, match.AwayGoals);

        match.AwayTeam.Budget += PrizeMoney.ForResult(
            league.Country, match.AwayGoals, match.HomeGoals);
    }

    private async Task<SeasonSummaryDto> BuildSummaryAsync(Season season, CancellationToken ct)
    {
        var winnerIds = season.Competitions
            .Where(c => c.WinnerTeamId is not null)
            .Select(c => c.WinnerTeamId!.Value)
            .ToList();

        if (season.UserTeamId is { } userTeam)
            winnerIds.Add(userTeam);

        var names = winnerIds.Count == 0
            ? []
            : await db.Teams
                .AsNoTracking()
                .Where(t => winnerIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var competitions = season.Competitions
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Country)
            .Select(c => c.ToSummary(c.WinnerTeamId is { } id ? names.GetValueOrDefault(id) : null))
            .ToList();

        var userTeamName = season.UserTeamId is { } team ? names.GetValueOrDefault(team) : null;

        return season.ToSummary(userTeamName, competitions);
    }

    private async Task<List<Team>> EntrantsAsync(Competition competition, CancellationToken ct) =>
        await db.Teams
            .AsNoTracking()
            .Where(t => db.CompetitionEntries.Any(e => e.CompetitionId == competition.Id && e.TeamId == t.Id))
            .ToListAsync(ct);

    private async Task<Guid?> UserTeamIdAsync(Guid seasonId, CancellationToken ct) =>
        await db.Seasons
            .AsNoTracking()
            .Where(s => s.Id == seasonId)
            .Select(s => s.UserTeamId)
            .FirstOrDefaultAsync(ct);

    private Task<Season> EnsureSeasonAsync(string userId, Guid seasonId, CancellationToken ct) =>
        EnsureSeasonAsync(userId, seasonId, tracked: false, ct);

    private async Task<Season> EnsureSeasonAsync(string userId, Guid seasonId, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.Seasons : db.Seasons.AsNoTracking();

        return await query.FirstOrDefaultAsync(s => s.Id == seasonId && s.UserId == userId, ct)
               ?? throw new KeyNotFoundException($"Season {seasonId} was not found.");
    }

    private Task<Competition> OwnedCompetitionAsync(string userId, Guid competitionId, CancellationToken ct) =>
        OwnedCompetitionAsync(userId, competitionId, tracked: false, ct);

    private async Task<Competition> OwnedCompetitionAsync(string userId, Guid competitionId, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.Competitions : db.Competitions.AsNoTracking();

        return await query.FirstOrDefaultAsync(c => c.Id == competitionId && c.Season.UserId == userId, ct)
               ?? throw new KeyNotFoundException($"Competition {competitionId} was not found.");
    }
}
