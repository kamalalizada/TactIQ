using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Standings;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Finances;

/// <summary>
/// The money a competition pays out once it is over.
///
/// League results pay as they happen, but where a club finishes is only known
/// at the end — and that is the larger sum by far. Paying it the moment the
/// last week is played means the table matters to the bottom of it: a club in
/// mid-table is still playing for a place, not just for pride.
/// </summary>
public class SeasonPayout(IAppDbContext db, IStandingsCalculator standings)
{
    /// <summary>
    /// Pays a finished league out by final position. A competition is paid
    /// once: the flag on the row is what stops a second week of simulation
    /// handing out the same money again.
    /// </summary>
    public async Task PayLeagueAsync(Competition league, CancellationToken ct)
    {
        if (league.Kind != CompetitionKind.League || league.PrizesPaid || !league.IsFinished)
            return;

        var teams = await db.Teams
            .Include(t => t.Players)
            .Where(t => db.CompetitionEntries
                .Any(e => e.CompetitionId == league.Id && e.TeamId == t.Id))
            .ToListAsync(ct);

        var matches = await db.Matches
            .Where(m => m.CompetitionId == league.Id && m.IsPlayed)
            .ToListAsync(ct);

        var table = standings.Calculate(teams, matches);
        var byId = teams.ToDictionary(t => t.Id);

        foreach (var row in table)
        {
            if (byId.TryGetValue(row.TeamId, out var club))
                club.Budget += PrizeMoney.ForLeaguePosition(league.Country, row.Rank, table.Count);
        }

        // The club that finished top is the champion, which the season banner
        // and the cup draw both read.
        league.WinnerTeamId = table.FirstOrDefault()?.TeamId;
        league.PrizesPaid = true;
    }

    /// <summary>
    /// Pays every club still in the Champions League for reaching this round.
    ///
    /// A run is paid round by round as it is survived, so a club knocked out in
    /// the quarter-finals keeps what the group stage and the last sixteen were
    /// worth. The final pays the winner more than the beaten finalist.
    /// </summary>
    public async Task PayCupRoundAsync(
        Competition cup, CupRound round, IReadOnlyCollection<Guid> stillIn, CancellationToken ct)
    {
        if (cup.Kind == CompetitionKind.League || stillIn.Count == 0)
            return;

        var clubs = await db.Teams
            .Where(t => stillIn.Contains(t.Id))
            .ToListAsync(ct);

        foreach (var club in clubs)
        {
            var won = round == CupRound.Final && cup.WinnerTeamId == club.Id;
            club.Budget += PrizeMoney.ForCupRound(round, won);
        }
    }
}
