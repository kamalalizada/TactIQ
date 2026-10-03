using Microsoft.EntityFrameworkCore;
using TactIQ.Application.Abstractions;
using TactIQ.Application.Scheduling;
using TactIQ.Application.Standings;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Careers;

public interface ICareerService
{
    /// <summary>What the manager has won, and whether the year can be closed.</summary>
    Task<CareerDto> GetAsync(string userId, Guid seasonId, CancellationToken ct = default);

    /// <summary>Closes the season and opens the next one.</summary>
    Task<SeasonRolloverDto> AdvanceAsync(string userId, Guid seasonId, CancellationToken ct = default);
}

public class CareerService(
    IAppDbContext db,
    IScheduleGenerator scheduleGenerator,
    IStandingsCalculator standings) : ICareerService
{
    /// <summary>How many risers and fallers are worth showing; a list of sixty is noise.</summary>
    private const int Highlights = 8;

    private readonly Random _random = new();

    public async Task<CareerDto> GetAsync(string userId, Guid seasonId, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, tracked: false, ct);

        var history = await db.SeasonRecords
            .AsNoTracking()
            .Where(r => r.SeasonId == seasonId)
            .OrderByDescending(r => r.Year)
            .ToListAsync(ct);

        var competitions = await db.Competitions
            .AsNoTracking()
            .Where(c => c.SeasonId == seasonId)
            .ToListAsync(ct);

        var clubName = season.UserTeamId is { } teamId
            ? await db.Teams.Where(t => t.Id == teamId).Select(t => t.Name).FirstOrDefaultAsync(ct)
            : null;

        var records = history.Select(r => ToDto(r, season.UserTeamId)).ToList();

        return new CareerDto(
            season.Year,
            competitions.Count > 0 && competitions.All(c => c.IsFinished),
            clubName,
            records.Count(r => r.UserWon),
            records);
    }

    public async Task<SeasonRolloverDto> AdvanceAsync(
        string userId, Guid seasonId, CancellationToken ct = default)
    {
        var season = await OwnedSeasonAsync(userId, seasonId, tracked: true, ct);

        var summary = await new SeasonRollover(db, scheduleGenerator, standings)
            .AdvanceAsync(season, _random, ct);

        // Only the manager's own club is worth reporting on by name: sixty
        // squads of risers and fallers is a wall of text nobody reads.
        var mine = season.UserTeamId is { } teamId
            ? await db.Teams.Where(t => t.Id == teamId).Select(t => t.Name).FirstOrDefaultAsync(ct)
            : null;

        return new SeasonRolloverDto(
            summary.Year,
            season.Year,
            summary.Records.Select(r => ToDto(r, season.UserTeamId)).ToList(),
            summary.Retired
                .Where(r => mine is null || r.TeamName == mine)
                .Select(r => new RetirementDto(r.Name, r.Age, r.Overall, r.TeamName, r.ReplacedBy))
                .ToList(),
            Pick(summary.Improved, mine, best: true),
            Pick(summary.Declined, mine, best: false));
    }

    private static IReadOnlyList<DevelopmentDto> Pick(
        IReadOnlyList<SeasonRollover.DevelopedPlayer> players, string? club, bool best)
    {
        var ordered = players.Where(p => club is null || p.TeamName == club);

        ordered = best
            ? ordered.OrderByDescending(p => p.After - p.Before).ThenByDescending(p => p.After)
            : ordered.OrderBy(p => p.After - p.Before).ThenByDescending(p => p.Before);

        return ordered
            .Take(Highlights)
            .Select(p => new DevelopmentDto(p.Name, p.Age, p.Before, p.After, p.TeamName))
            .ToList();
    }

    private static SeasonRecordDto ToDto(SeasonRecord record, Guid? userTeamId) => new(
        record.Year,
        record.CompetitionName,
        record.Country,
        record.ChampionTeamId,
        record.ChampionTeamName,
        record.UserRank,
        record.UserPoints,
        record.UserBudget,
        userTeamId is not null && record.ChampionTeamId == userTeamId);

    private async Task<Season> OwnedSeasonAsync(
        string userId, Guid seasonId, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.Seasons : db.Seasons.AsNoTracking();

        return await query.FirstOrDefaultAsync(s => s.Id == seasonId && s.UserId == userId, ct)
               ?? throw new KeyNotFoundException($"Season {seasonId} was not found.");
    }
}
