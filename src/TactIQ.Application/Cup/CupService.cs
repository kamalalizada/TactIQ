using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Scheduling;
using TactIQ.Application.Seasons;
using TactIQ.Application.Simulation;
using TactIQ.Application.Standings;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Cup;

public class CupService(
    IAppDbContext db,
    IMatchEngine matchEngine,
    IScheduleGenerator scheduleGenerator,
    IStandingsCalculator standingsCalculator) : ICupService
{
    private const string CompetitionName = "Champions League";

    private readonly Random _random = new();

    public async Task<CupBracketDto> StartAsync(string userId, Guid seasonId, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, ct);

        var existing = season.Competitions.FirstOrDefault(c => c.Kind == CompetitionKind.ChampionsLeague);
        if (existing is not null)
            return await GetBracketAsync(userId, seasonId, ct);

        var leagues = season.Competitions.Where(c => c.Kind == CompetitionKind.League).ToList();
        if (leagues.Count == 0 || leagues.Any(l => !l.IsFinished))
            throw new InvalidOperationException("Every domestic league must finish before the Champions League starts.");

        var qualifiers = await QualifiersAsync(leagues, ct);

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            Name = CompetitionName,
            Kind = CompetitionKind.ChampionsLeague,
            TeamCount = qualifiers.Count,
            CurrentRound = CupRound.GroupStage,
            CurrentWeek = 1
        };

        var groups = CupRules.DrawGroups(qualifiers, _random);

        foreach (var (name, teamIds) in groups)
        {
            competition.Entries.AddRange(teamIds.Select(id => new CompetitionEntry
            {
                CompetitionId = competition.Id,
                TeamId = id,
                GroupName = name
            }));

            // Each group is its own small double round-robin.
            competition.Matches.AddRange(scheduleGenerator
                .Generate(teamIds)
                .Select(f => new Match
                {
                    CompetitionId = competition.Id,
                    WeekNumber = f.WeekNumber,
                    GroupName = name,
                    Round = CupRound.GroupStage,
                    HomeTeamId = f.HomeTeamId,
                    AwayTeamId = f.AwayTeamId
                }));
        }

        season.Competitions.Add(competition);
        await db.SaveChangesAsync(ct);

        return await GetBracketAsync(userId, seasonId, ct);
    }

    public async Task<CupBracketDto> GetBracketAsync(string userId, Guid seasonId, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, ct);

        var competition = season.Competitions.FirstOrDefault(c => c.Kind == CompetitionKind.ChampionsLeague);
        if (competition is null)
        {
            return new CupBracketDto(Guid.Empty, CompetitionName, false, false, null, null, null, [], []);
        }

        var matches = await CupMatchesAsync(competition.Id, ct);
        var entries = await db.CompetitionEntries
            .AsNoTracking()
            .Where(e => e.CompetitionId == competition.Id)
            .ToListAsync(ct);

        var teams = await EntrantTeamsAsync(competition.Id, ct);
        var teamById = teams.ToDictionary(t => t.Id);

        var groups = BuildGroups(entries, matches, teamById);

        var ties = matches
            .Where(m => m.TieId is not null)
            .GroupBy(m => m.TieId!.Value)
            .Select(g => BuildTie(g.Key, g.ToList(), season.UserTeamId, teamById))
            .OrderBy(t => t.Round)
            .ToList();

        var winnerName = competition.WinnerTeamId is { } winnerId
            ? teamById.GetValueOrDefault(winnerId)?.Name
            : null;

        return new CupBracketDto(
            competition.Id,
            competition.Name,
            true,
            competition.IsFinished,
            competition.CurrentRound,
            competition.WinnerTeamId,
            winnerName,
            groups,
            ties);
    }

    public async Task<CupRoundResult> PlayRoundAsync(string userId, Guid seasonId, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, tracked: true, ct);

        var competition = season.Competitions.FirstOrDefault(c => c.Kind == CompetitionKind.ChampionsLeague)
                          ?? throw new InvalidOperationException("The Champions League has not been drawn yet.");

        if (competition.CurrentRound is not { } round)
            throw new InvalidOperationException("The Champions League is already finished.");

        var matches = await CupMatchesAsync(competition.Id, tracked: true, ct);

        return round == CupRound.GroupStage
            ? await PlayGroupWeekAsync(season, competition, matches, ct)
            : await PlayKnockoutRoundAsync(season, competition, matches, round, ct);
    }

    /// <summary>Group games run a week at a time so the manager can play their own.</summary>
    private async Task<CupRoundResult> PlayGroupWeekAsync(
        Season season,
        Competition competition,
        List<Match> matches,
        CancellationToken ct)
    {
        var week = competition.CurrentWeek;
        var thisWeek = matches.Where(m => m.Round == CupRound.GroupStage && m.WeekNumber == week).ToList();

        var simulated = new List<Match>();
        Match? pending = null;

        foreach (var match in thisWeek)
        {
            if (IsUserMatch(match, season.UserTeamId))
            {
                pending = match;
                continue;
            }

            if (!match.IsPlayed)
                SeasonService.ApplyResult(match, matchEngine.SimulateInstant(match.HomeTeam, match.AwayTeam));

            simulated.Add(match);
        }

        var weekComplete = pending is null || pending.IsPlayed;
        CupRound? nextRound = CupRound.GroupStage;

        if (weekComplete)
        {
            competition.CurrentWeek = week + 1;

            var groupsFinished = matches
                .Where(m => m.Round == CupRound.GroupStage)
                .All(m => m.IsPlayed);

            if (groupsFinished)
                nextRound = await StartKnockoutAsync(competition, matches, ct);
        }

        await db.SaveChangesAsync(ct);

        return new CupRoundResult(
            CupRound.GroupStage,
            simulated.Select(m => m.ToDto(season.UserTeamId)).ToList(),
            pending?.IsPlayed == false ? pending.ToDto(season.UserTeamId) : null,
            nextRound,
            competition.IsFinished,
            competition.WinnerTeamId,
            null);
    }

    private async Task<CupRoundResult> PlayKnockoutRoundAsync(
        Season season,
        Competition competition,
        List<Match> matches,
        CupRound round,
        CancellationToken ct)
    {
        var roundMatches = matches.Where(m => m.Round == round).ToList();

        var simulated = new List<Match>();
        Match? pending = null;

        foreach (var match in roundMatches.OrderBy(m => m.Leg ?? 1))
        {
            if (match.IsPlayed)
            {
                simulated.Add(match);
                continue;
            }

            if (IsUserMatch(match, season.UserTeamId))
            {
                pending ??= match;
                continue;
            }

            SeasonService.ApplyResult(match, matchEngine.SimulateInstant(match.HomeTeam, match.AwayTeam));
            simulated.Add(match);
        }

        CupRound? nextRound = round;

        if (roundMatches.All(m => m.IsPlayed))
        {
            SettleLevelTies(roundMatches);
            nextRound = AdvanceWinners(competition, round, roundMatches);
        }

        await db.SaveChangesAsync(ct);

        var teams = await EntrantTeamsAsync(competition.Id, ct);
        var winnerName = competition.WinnerTeamId is { } id
            ? teams.FirstOrDefault(t => t.Id == id)?.Name
            : null;

        return new CupRoundResult(
            round,
            simulated.Select(m => m.ToDto(season.UserTeamId)).ToList(),
            pending?.ToDto(season.UserTeamId),
            nextRound,
            competition.IsFinished,
            competition.WinnerTeamId,
            winnerName);
    }

    /// <summary>Takes the top two of every group into the quarter-finals.</summary>
    private async Task<CupRound?> StartKnockoutAsync(Competition competition, List<Match> matches, CancellationToken ct)
    {
        var entries = await db.CompetitionEntries
            .Where(e => e.CompetitionId == competition.Id)
            .ToListAsync(ct);

        var teams = await EntrantTeamsAsync(competition.Id, ct);
        var teamById = teams.ToDictionary(t => t.Id);

        var qualifiers = new List<Guid>();

        foreach (var group in entries.GroupBy(e => e.GroupName))
        {
            var groupTeams = group.Select(e => teamById[e.TeamId]).ToList();
            var groupMatches = matches.Where(m => m.GroupName == group.Key).ToList();

            var table = standingsCalculator.Calculate(groupTeams, groupMatches);
            var through = table.Take(CupRules.QualifiersPerGroup).Select(r => r.TeamId).ToList();

            qualifiers.AddRange(through);

            foreach (var entry in group.Where(e => !through.Contains(e.TeamId)))
                entry.Eliminated = true;
        }

        CreateRound(competition, CupRules.Draw(qualifiers, CupRound.QuarterFinal, _random));
        competition.CurrentRound = CupRound.QuarterFinal;

        return CupRound.QuarterFinal;
    }

    private CupRound? AdvanceWinners(Competition competition, CupRound round, List<Match> roundMatches)
    {
        var winners = roundMatches
            .Where(m => m.TieId is not null)
            .GroupBy(m => m.TieId!.Value)
            .Select(g => CupRules.Resolve(g.ToList()).WinnerTeamId)
            .ToList();

        var next = CupRules.NextRound(round);

        if (next is null)
        {
            competition.WinnerTeamId = winners.Single();
            competition.CurrentRound = null;
            return null;
        }

        CreateRound(competition, CupRules.Draw(winners, next.Value, _random));
        competition.CurrentRound = next;
        return next;
    }

    /// <summary>Gives every level tie a shoot-out so a winner can be read off it.</summary>
    private void SettleLevelTies(List<Match> roundMatches)
    {
        foreach (var tie in roundMatches.Where(m => m.TieId is not null).GroupBy(m => m.TieId!.Value))
        {
            var legs = tie.ToList();
            var decider = legs.OrderByDescending(m => m.Leg ?? 1).First();

            if (decider.WentToPenalties)
                continue;

            var firstLeg = legs.OrderBy(m => m.Leg ?? 1).First();
            var (home, away) = Aggregate(legs, firstLeg.HomeTeamId);

            if (home != away)
                continue;

            var (homePens, awayPens) = CupRules.TakePenalties(decider.HomeTeam, decider.AwayTeam, _random);
            decider.HomePenalties = homePens;
            decider.AwayPenalties = awayPens;
        }
    }

    private static void CreateRound(Competition competition, IReadOnlyList<CupTie> ties)
    {
        foreach (var tie in ties)
        {
            competition.Matches.Add(NewCupMatch(competition.Id, tie, leg: 1,
                homeTeamId: tie.FirstLegHomeTeamId, awayTeamId: tie.FirstLegAwayTeamId));

            if (CupRules.IsTwoLegged(tie.Round))
            {
                // Second leg flips the venue.
                competition.Matches.Add(NewCupMatch(competition.Id, tie, leg: 2,
                    homeTeamId: tie.FirstLegAwayTeamId, awayTeamId: tie.FirstLegHomeTeamId));
            }
        }
    }

    private static Match NewCupMatch(Guid competitionId, CupTie tie, int leg, Guid homeTeamId, Guid awayTeamId) => new()
    {
        CompetitionId = competitionId,
        Round = tie.Round,
        Leg = leg,
        TieId = tie.TieId,
        WeekNumber = (int)tie.Round * 2 + leg,
        HomeTeamId = homeTeamId,
        AwayTeamId = awayTeamId
    };

    /// <summary>The leading clubs of every league, filling the bracket evenly.</summary>
    private async Task<List<Team>> QualifiersAsync(List<Competition> leagues, CancellationToken ct)
    {
        var perLeague = CupRules.EntrantCount / leagues.Count;
        var remainder = CupRules.EntrantCount % leagues.Count;

        var qualifiers = new List<Team>();

        // Stronger leagues, by average club rating, get the spare places.
        var ordered = new List<(Competition Competition, List<Team> Teams)>();

        foreach (var league in leagues)
        {
            var teams = await db.Teams
                .Include(t => t.Players)
                .Where(t => db.CompetitionEntries.Any(e => e.CompetitionId == league.Id && e.TeamId == t.Id))
                .ToListAsync(ct);

            ordered.Add((league, teams));
        }

        ordered = ordered
            .OrderByDescending(x => x.Teams.Count == 0 ? 0 : x.Teams.Average(t => t.TeamOverall))
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var (league, teams) = ordered[i];
            var take = perLeague + (i < remainder ? 1 : 0);

            var matches = await db.Matches
                .AsNoTracking()
                .Where(m => m.CompetitionId == league.Id)
                .ToListAsync(ct);

            var table = standingsCalculator.Calculate(teams, matches);
            var topIds = table.Take(take).Select(r => r.TeamId).ToHashSet();

            qualifiers.AddRange(teams.Where(t => topIds.Contains(t.Id)));
        }

        return qualifiers;
    }

    private static IReadOnlyList<GroupDto> BuildGroups(
        List<CompetitionEntry> entries,
        List<Match> matches,
        Dictionary<Guid, Team> teamById)
    {
        var groups = new List<GroupDto>();

        foreach (var group in entries.Where(e => e.GroupName is not null).GroupBy(e => e.GroupName!).OrderBy(g => g.Key))
        {
            var groupTeams = group.Select(e => teamById[e.TeamId]).ToList();
            var groupMatches = matches.Where(m => m.GroupName == group.Key).ToList();

            var calculator = new StandingsCalculator();
            var table = calculator.Calculate(groupTeams, groupMatches);

            groups.Add(new GroupDto(group.Key, table.Select(r => new GroupRowDto(
                r.Rank,
                r.TeamId,
                r.TeamName,
                teamById.GetValueOrDefault(r.TeamId)?.LogoUrl,
                r.Played,
                r.Won,
                r.Drawn,
                r.Lost,
                r.GoalsFor,
                r.GoalsAgainst,
                r.GoalDifference,
                r.Points,
                r.Rank <= CupRules.QualifiersPerGroup)).ToList()));
        }

        return groups;
    }

    private static CupTieDto BuildTie(
        Guid tieId,
        List<Match> legs,
        Guid? userTeamId,
        Dictionary<Guid, Team> teamById)
    {
        var firstLeg = legs.OrderBy(m => m.Leg ?? 1).First();
        var complete = legs.All(m => m.IsPlayed);

        var (homeAggregate, awayAggregate) = Aggregate(legs.Where(m => m.IsPlayed).ToList(), firstLeg.HomeTeamId);

        Guid? winner = null;
        var onPenalties = false;

        if (complete)
        {
            try
            {
                var outcome = CupRules.Resolve(legs);
                winner = outcome.WinnerTeamId;
                onPenalties = outcome.DecidedOnPenalties;
            }
            catch (InvalidOperationException)
            {
                // Level with no shoot-out recorded yet; the round is not settled.
            }
        }

        return new CupTieDto(
            tieId,
            firstLeg.Round ?? CupRound.QuarterFinal,
            firstLeg.HomeTeamId,
            teamById.GetValueOrDefault(firstLeg.HomeTeamId)?.Name ?? string.Empty,
            teamById.GetValueOrDefault(firstLeg.HomeTeamId)?.LogoUrl,
            firstLeg.AwayTeamId,
            teamById.GetValueOrDefault(firstLeg.AwayTeamId)?.Name ?? string.Empty,
            teamById.GetValueOrDefault(firstLeg.AwayTeamId)?.LogoUrl,
            homeAggregate,
            awayAggregate,
            complete,
            winner,
            onPenalties,
            legs.OrderBy(m => m.Leg ?? 1).Select(m => m.ToDto(userTeamId)).ToList());
    }

    private static (int Home, int Away) Aggregate(List<Match> legs, Guid homeTeamId)
    {
        var home = 0;
        var away = 0;

        foreach (var leg in legs)
        {
            if (leg.HomeTeamId == homeTeamId)
            {
                home += leg.HomeGoals;
                away += leg.AwayGoals;
            }
            else
            {
                home += leg.AwayGoals;
                away += leg.HomeGoals;
            }
        }

        return (home, away);
    }

    private static bool IsUserMatch(Match match, Guid? userTeamId) =>
        userTeamId is not null && (match.HomeTeamId == userTeamId || match.AwayTeamId == userTeamId);

    private Task<Season> OwnedSeasonAsync(string userId, Guid seasonId, CancellationToken ct) =>
        OwnedSeasonAsync(userId, seasonId, tracked: false, ct);

    private async Task<Season> OwnedSeasonAsync(string userId, Guid seasonId, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.Seasons : db.Seasons.AsNoTracking();

        return await query
            .Include(s => s.Competitions)
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.UserId == userId, ct)
        ?? throw new KeyNotFoundException($"Season {seasonId} was not found.");
    }

    private Task<List<Match>> CupMatchesAsync(Guid competitionId, CancellationToken ct) =>
        CupMatchesAsync(competitionId, tracked: false, ct);

    private async Task<List<Match>> CupMatchesAsync(Guid competitionId, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.Matches : db.Matches.AsNoTracking();

        return await query
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam).ThenInclude(t => t.Players)
            .Include(m => m.AwayTeam).ThenInclude(t => t.Players)
            .Include(m => m.Events)
            .Where(m => m.CompetitionId == competitionId)
            .ToListAsync(ct);
    }

    private async Task<List<Team>> EntrantTeamsAsync(Guid competitionId, CancellationToken ct) =>
        await db.Teams
            .AsNoTracking()
            .Where(t => db.CompetitionEntries.Any(e => e.CompetitionId == competitionId && e.TeamId == t.Id))
            .ToListAsync(ct);
}
