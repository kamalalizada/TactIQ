using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Scheduling;
using TactIQ.Application.Standings;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Careers;

/// <summary>
/// Closing one season and opening the next.
///
/// A save used to end where the table did. Here the career goes on: what
/// happened is written down, every player is a year older, the ones whose
/// careers have ended are replaced from the academy, and the fixtures are
/// redrawn. What carries over is everything a manager built — his squad, his
/// money, his shape and his record.
/// </summary>
public class SeasonRollover(
    IAppDbContext db,
    IScheduleGenerator scheduleGenerator,
    IStandingsCalculator standings)
{
    /// <summary>What one year did to a career, for the manager to read.</summary>
    public record Summary(
        int Year,
        IReadOnlyList<SeasonRecord> Records,
        IReadOnlyList<RetiredPlayer> Retired,
        IReadOnlyList<DevelopedPlayer> Improved,
        IReadOnlyList<DevelopedPlayer> Declined);

    public record RetiredPlayer(string Name, int Age, int Overall, string TeamName, string? ReplacedBy);

    public record DevelopedPlayer(string Name, int Age, int Before, int After, string TeamName);

    /// <summary>
    /// Rolls the save on a year. The season must be finished: a career cannot
    /// move on from a table that is still being played.
    /// </summary>
    public async Task<Summary> AdvanceAsync(Season season, Random random, CancellationToken ct)
    {
        var competitions = await db.Competitions
            .Include(c => c.Entries)
            .Where(c => c.SeasonId == season.Id)
            .ToListAsync(ct);

        if (competitions.Count == 0 || !competitions.All(c => c.IsFinished))
            throw new InvalidOperationException("Mövsüm bitmədən növbəti mövsümə keçmək olmaz.");

        var clubs = await db.Teams
            .Include(t => t.Players)
            .Where(t => t.SeasonId == season.Id)
            .ToListAsync(ct);

        var records = await WriteHistoryAsync(season, competitions, clubs, ct);
        var (retired, improved, declined) = AgeEveryone(clubs, random);

        await RedrawFixturesAsync(season, competitions, clubs, ct);

        season.Year++;
        season.CurrentWeek = 0;

        await db.SaveChangesAsync(ct);

        return new Summary(season.Year - 1, records, retired, improved, declined);
    }

    /// <summary>
    /// Writes down how each league finished before the table is cleared, which
    /// is the only moment the information still exists.
    /// </summary>
    private async Task<List<SeasonRecord>> WriteHistoryAsync(
        Season season,
        List<Competition> competitions,
        List<Team> clubs,
        CancellationToken ct)
    {
        var records = new List<SeasonRecord>();
        var byId = clubs.ToDictionary(c => c.Id);

        foreach (var competition in competitions.Where(c => c.Kind == CompetitionKind.League))
        {
            var entrants = competition.Entries
                .Select(e => byId.GetValueOrDefault(e.TeamId))
                .Where(t => t is not null)
                .Select(t => t!)
                .ToList();

            // Read only: these same rows are deleted a moment later when the
            // fixtures are redrawn, and tracking them twice would leave the
            // save trying to update records it has already removed.
            var matches = await db.Matches
                .AsNoTracking()
                .Where(m => m.CompetitionId == competition.Id && m.IsPlayed)
                .ToListAsync(ct);

            var table = standings.Calculate(entrants, matches);
            var champion = table.FirstOrDefault();
            var user = season.UserTeamId is { } mine
                ? table.FirstOrDefault(row => row.TeamId == mine)
                : null;

            var record = new SeasonRecord
            {
                Id = Guid.NewGuid(),
                SeasonId = season.Id,
                Year = season.Year,
                CompetitionName = competition.Name,
                Country = competition.Country,
                ChampionTeamId = champion?.TeamId,
                ChampionTeamName = champion?.TeamName,
                UserRank = user?.Rank,
                UserPoints = user?.Points,
                UserBudget = season.UserTeamId is { } id && byId.TryGetValue(id, out var club)
                    ? club.Budget
                    : null,
                RecordedAt = DateTime.UtcNow
            };

            db.SeasonRecords.Add(record);
            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Ages every player in the save, retires the ones whose careers have run
    /// out, and brings a youth player through for each of them so no squad is
    /// left short.
    /// </summary>
    private (List<RetiredPlayer>, List<DevelopedPlayer>, List<DevelopedPlayer>) AgeEveryone(
        List<Team> clubs, Random random)
    {
        var retired = new List<RetiredPlayer>();
        var improved = new List<DevelopedPlayer>();
        var declined = new List<DevelopedPlayer>();
        var leaving = new HashSet<Guid>();

        foreach (var club in clubs)
        {
            // Copied, because the squad is rebuilt as it is walked.
            foreach (var player in club.Players.ToList())
            {
                var change = PlayerDevelopment.Advance(player, random);

                if (change.Retired)
                {
                    var replacement = YouthAcademy.Promote(club, player, random);

                    retired.Add(new RetiredPlayer(
                        player.Name, player.Age, change.Before, club.Name, replacement.Name));

                    club.Players.Remove(player);
                    db.Players.Remove(player);

                    // Added through the set, not the squad list alone: the id is
                    // assigned up front, so EF would otherwise take the youth
                    // player for an existing row and try to update nothing.
                    db.Players.Add(replacement);
                    club.Players.Add(replacement);

                    // Nothing may touch him again: a row cannot be both updated
                    // and deleted in the same save.
                    leaving.Add(player.Id);

                    continue;
                }

                var moved = new DevelopedPlayer(
                    player.Name, player.Age, change.Before, change.After, club.Name);

                if (change.Improved) improved.Add(moved);
                else if (change.Declined) declined.Add(moved);
            }

            // A side may be left with fewer than eleven when careers end, so
            // the strongest available fill the gaps. The manager can change it;
            // what matters is that a club always has an eleven to field.
            RestoreElevenIfShort(club, leaving);
        }

        return (retired, improved, declined);
    }

    /// <summary>
    /// Makes sure a club still has eleven starters after a year of retirements,
    /// filling any gap with the best player available in that line.
    /// </summary>
    private static void RestoreElevenIfShort(Team club, HashSet<Guid> leaving)
    {
        // Players on their way out of the game are not part of any side.
        var available = club.Players.Where(p => !leaving.Contains(p.Id)).ToList();

        if (available.Count(p => p.IsStarter) == Seeding.SquadRules.StarterCount)
            return;

        // Rebuild from the shape the club plays, which is what the squad screen
        // and the match engine both read.
        var shape = Simulation.FormationShape.IsKnown(club.Formation)
            ? Simulation.FormationShape.Of(club.Formation)
            : Simulation.FormationShape.Of("4-4-2");

        foreach (var player in available)
            player.LeaveLineup();

        var taken = new HashSet<Guid>();
        var slot = 1;

        foreach (var role in shape)
        {
            var line = Simulation.RoleFitness.LineOf(role);

            var best = available
                .Where(p => !taken.Contains(p.Id))
                .OrderByDescending(p => p.Position == line)
                .ThenByDescending(p => Simulation.RoleFitness.For(p.Role, role))
                .ThenByDescending(p => p.Overall)
                .FirstOrDefault();

            if (best is null)
                break;

            taken.Add(best.Id);
            best.IsStarter = true;
            best.LineupSlot = slot++;
        }
    }

    /// <summary>
    /// Clears last year's results and draws a new calendar. The clubs are the
    /// same, so the league they play in is the same; only the fixtures and the
    /// table start again.
    /// </summary>
    private async Task RedrawFixturesAsync(
        Season season, List<Competition> competitions, List<Team> clubs, CancellationToken ct)
    {
        var oldMatches = await db.Matches
            .Where(m => m.Competition.SeasonId == season.Id)
            .ToListAsync(ct);

        db.Matches.RemoveRange(oldMatches);

        foreach (var competition in competitions)
        {
            if (competition.Kind != CompetitionKind.League)
            {
                // A cup is drawn afresh when the manager starts it, so last
                // year's bracket is cleared away. Its entries go with it
                // through the cascade rather than being removed separately,
                // which would delete the same rows twice.
                db.Competitions.Remove(competition);
                continue;
            }

            competition.CurrentWeek = 1;
            competition.WinnerTeamId = null;
            competition.PrizesPaid = false;

            var teamIds = competition.Entries.Select(e => e.TeamId).ToList();

            db.Matches.AddRange(scheduleGenerator
                .Generate(teamIds)
                .Select(f => new Match
                {
                    CompetitionId = competition.Id,
                    WeekNumber = f.WeekNumber,
                    HomeTeamId = f.HomeTeamId,
                    AwayTeamId = f.AwayTeamId
                }));
        }

        // Last year's business is history; this year's market starts clean.
        var offers = await db.TransferOffers
            .Where(o => o.SeasonId == season.Id && o.Status == OfferStatus.Pending)
            .ToListAsync(ct);

        foreach (var offer in offers)
        {
            offer.Status = OfferStatus.Expired;
            offer.ResponseReason = "Mövsüm bitdi.";
        }
    }
}
