using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Leagues;
using TactIQ.Application.Simulation;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Cup;

public class CupService(IAppDbContext db, IMatchEngine matchEngine) : ICupService
{
    private readonly Random _random = new();

    public async Task<CupBracketDto> StartAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await OwnedLeagueAsync(userId, leagueId, ct);

        if (!league.IsFinished)
            throw new InvalidOperationException("The cup starts once the league season is over.");

        if (league.CupStarted)
            return await GetBracketAsync(userId, leagueId, ct);

        var teamIds = await db.Teams
            .Where(t => t.LeagueId == leagueId)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (teamIds.Count != CupRules.EntrantCount)
            throw new InvalidOperationException($"The cup needs exactly {CupRules.EntrantCount} teams.");

        CreateRound(league, CupRules.Draw(teamIds, CupRound.QuarterFinal, _random));
        league.CurrentCupRound = CupRound.QuarterFinal;

        await db.SaveChangesAsync(ct);
        return await GetBracketAsync(userId, leagueId, ct);
    }

    public async Task<CupBracketDto> GetBracketAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await OwnedLeagueAsync(userId, leagueId, ct);
        var matches = await CupMatchesAsync(leagueId, ct);

        var ties = matches
            .Where(m => m.TieId is not null)
            .GroupBy(m => m.TieId!.Value)
            .Select(g => BuildTie(g.Key, g.ToList(), league.UserTeamId))
            .OrderBy(t => t.Round)
            .ToList();

        var winnerName = league.CupWinnerTeamId is { } winnerId
            ? matches.SelectMany(m => new[] { m.HomeTeam, m.AwayTeam })
                     .FirstOrDefault(t => t?.Id == winnerId)?.Name
            : null;

        return new CupBracketDto(
            league.CupStarted,
            league.CupFinished,
            league.CurrentCupRound,
            league.CupWinnerTeamId,
            winnerName,
            ties);
    }

    public async Task<CupRoundResult> PlayRoundAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await OwnedLeagueAsync(userId, leagueId, ct);

        if (!league.CupStarted)
            throw new InvalidOperationException("The cup has not been drawn yet.");

        if (league.CurrentCupRound is not { } round)
            throw new InvalidOperationException("The cup is already finished.");

        var matches = await CupMatchesAsync(leagueId, ct);
        var roundMatches = matches.Where(m => m.Round == round).ToList();

        var simulated = new List<Match>();
        Match? pendingUserMatch = null;

        foreach (var match in roundMatches.OrderBy(m => m.Leg ?? 1))
        {
            if (match.IsPlayed)
            {
                simulated.Add(match);
                continue;
            }

            if (IsUserMatch(match, league.UserTeamId))
            {
                // The manager plays their own tie live, exactly as in the league.
                pendingUserMatch ??= match;
                continue;
            }

            Play(match);
            simulated.Add(match);
        }

        var roundComplete = roundMatches.All(m => m.IsPlayed);
        CupRound? nextRound = round;

        if (roundComplete)
        {
            SettleLevelTies(roundMatches);
            nextRound = await AdvanceAsync(league, round, roundMatches, ct);
        }

        await db.SaveChangesAsync(ct);

        var winnerName = league.CupWinnerTeamId is { } id
            ? roundMatches.SelectMany(m => new[] { m.HomeTeam, m.AwayTeam })
                          .FirstOrDefault(t => t?.Id == id)?.Name
            : null;

        return new CupRoundResult(
            round,
            simulated.Select(m => m.ToDto(league.UserTeamId)).ToList(),
            pendingUserMatch?.ToDto(league.UserTeamId),
            nextRound,
            league.CupFinished,
            league.CupWinnerTeamId,
            winnerName);
    }

    /// <summary>Called once every leg of a round is in, to move the winners on.</summary>
    private async Task<CupRound?> AdvanceAsync(
        League league,
        CupRound round,
        List<Match> roundMatches,
        CancellationToken ct)
    {
        var winners = roundMatches
            .Where(m => m.TieId is not null)
            .GroupBy(m => m.TieId!.Value)
            .Select(g => CupRules.Resolve(g.ToList()).WinnerTeamId)
            .ToList();

        var next = CupRules.NextRound(round);

        if (next is null)
        {
            league.CupWinnerTeamId = winners.Single();
            league.CurrentCupRound = null;
            return null;
        }

        CreateRound(league, CupRules.Draw(winners, next.Value, _random));
        league.CurrentCupRound = next;

        await Task.CompletedTask;
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
            var homeAggregate = 0;
            var awayAggregate = 0;

            foreach (var leg in legs)
            {
                if (leg.HomeTeamId == firstLeg.HomeTeamId)
                {
                    homeAggregate += leg.HomeGoals;
                    awayAggregate += leg.AwayGoals;
                }
                else
                {
                    homeAggregate += leg.AwayGoals;
                    awayAggregate += leg.HomeGoals;
                }
            }

            if (homeAggregate != awayAggregate)
                continue;

            var (home, away) = CupRules.TakePenalties(decider.HomeTeam, decider.AwayTeam, _random);
            decider.HomePenalties = home;
            decider.AwayPenalties = away;
        }
    }

    private void CreateRound(League league, IReadOnlyList<CupTie> ties)
    {
        foreach (var tie in ties)
        {
            league.Matches.Add(NewCupMatch(league.Id, tie, leg: 1,
                homeTeamId: tie.FirstLegHomeTeamId, awayTeamId: tie.FirstLegAwayTeamId));

            if (CupRules.IsTwoLegged(tie.Round))
            {
                // Second leg flips the venue.
                league.Matches.Add(NewCupMatch(league.Id, tie, leg: 2,
                    homeTeamId: tie.FirstLegAwayTeamId, awayTeamId: tie.FirstLegHomeTeamId));
            }
        }
    }

    private static Match NewCupMatch(Guid leagueId, CupTie tie, int leg, Guid homeTeamId, Guid awayTeamId) => new()
    {
        LeagueId = leagueId,
        Competition = CompetitionType.Cup,
        Round = tie.Round,
        Leg = leg,
        TieId = tie.TieId,
        WeekNumber = (int)tie.Round * 2 + leg,
        HomeTeamId = homeTeamId,
        AwayTeamId = awayTeamId
    };

    private void Play(Match match)
    {
        var result = matchEngine.SimulateInstant(match.HomeTeam, match.AwayTeam);

        match.HomeGoals = result.HomeGoals;
        match.AwayGoals = result.AwayGoals;
        match.IsPlayed = true;

        foreach (var e in result.Events)
        {
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

    private static CupTieDto BuildTie(Guid tieId, List<Match> legs, Guid? userTeamId)
    {
        var firstLeg = legs.OrderBy(m => m.Leg ?? 1).First();
        var complete = legs.All(m => m.IsPlayed);

        var homeAggregate = 0;
        var awayAggregate = 0;

        foreach (var leg in legs.Where(m => m.IsPlayed))
        {
            if (leg.HomeTeamId == firstLeg.HomeTeamId)
            {
                homeAggregate += leg.HomeGoals;
                awayAggregate += leg.AwayGoals;
            }
            else
            {
                homeAggregate += leg.AwayGoals;
                awayAggregate += leg.HomeGoals;
            }
        }

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
                // Level with no shoot-out recorded yet; the round has not been settled.
            }
        }

        return new CupTieDto(
            tieId,
            firstLeg.Round ?? CupRound.QuarterFinal,
            firstLeg.HomeTeamId,
            firstLeg.HomeTeam?.Name ?? string.Empty,
            firstLeg.AwayTeamId,
            firstLeg.AwayTeam?.Name ?? string.Empty,
            homeAggregate,
            awayAggregate,
            complete,
            winner,
            onPenalties,
            legs.OrderBy(m => m.Leg ?? 1).Select(m => m.ToDto(userTeamId)).ToList());
    }

    private static bool IsUserMatch(Match match, Guid? userTeamId) =>
        userTeamId is not null && (match.HomeTeamId == userTeamId || match.AwayTeamId == userTeamId);

    private async Task<League> OwnedLeagueAsync(string userId, Guid leagueId, CancellationToken ct) =>
        await db.Leagues
            .Include(l => l.Matches)
            .FirstOrDefaultAsync(l => l.Id == leagueId && l.UserId == userId, ct)
        ?? throw new KeyNotFoundException($"League {leagueId} was not found.");

    private async Task<List<Match>> CupMatchesAsync(Guid leagueId, CancellationToken ct) =>
        await db.Matches
            .Include(m => m.HomeTeam).ThenInclude(t => t.Players)
            .Include(m => m.AwayTeam).ThenInclude(t => t.Players)
            .Include(m => m.Events)
            .Where(m => m.LeagueId == leagueId && m.Competition == CompetitionType.Cup)
            .ToListAsync(ct);
}
