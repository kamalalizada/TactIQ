using TactIQ.Application.Statistics;

namespace TactIQ.Application.Seasons;

public interface ISeasonService
{
    Task<SeasonSummaryDto> CreateAsync(string userId, CreateSeasonRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<SeasonSummaryDto>> ListAsync(string userId, CancellationToken ct = default);

    Task<SeasonSummaryDto?> GetAsync(string userId, Guid seasonId, CancellationToken ct = default);

    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(string userId, Guid seasonId, string? country, CancellationToken ct = default);

    /// <summary>
    /// Finds clubs and players by name, for the search box every page carries.
    /// A short query matches anywhere in the name, so "sal" finds Salah.
    /// </summary>
    Task<SearchResultDto> SearchAsync(string userId, Guid seasonId, string query, CancellationToken ct = default);

    /// <summary>
    /// Picks the club the manager takes charge of. Only allowed before the
    /// season is under way, since a mid-season switch would leave the previous
    /// club's results standing against a team the manager no longer runs.
    /// </summary>
    Task<SeasonSummaryDto> SetUserTeamAsync(string userId, Guid seasonId, Guid teamId, CancellationToken ct = default);

    /// <summary>
    /// Sets the managed club's shape and the eleven who start in it. The
    /// line-up has to match the formation line for line, and every player has
    /// to be fit to play.
    /// </summary>
    Task<TeamDto> SetLineupAsync(
        string userId,
        Guid seasonId,
        string formation,
        IReadOnlyList<Guid> starterIds,
        CancellationToken ct = default);

    Task<IReadOnlyList<StandingsRowDto>> GetStandingsAsync(string userId, Guid competitionId, CancellationToken ct = default);

    Task<IReadOnlyList<MatchDto>> GetFixturesAsync(string userId, Guid competitionId, int? week, CancellationToken ct = default);

    /// <summary>
    /// Plays one week of a domestic league. The manager's own match is left
    /// pending unless <paramref name="includeUserMatch"/> asks for it to be
    /// simulated along with the rest.
    /// </summary>
    Task<SimulateWeekResult> SimulateWeekAsync(
        string userId,
        Guid competitionId,
        bool includeUserMatch = false,
        CancellationToken ct = default);

    /// <summary>Plays the next week of every league that still has one.</summary>
    Task<IReadOnlyList<SimulateWeekResult>> SimulateAllLeaguesAsync(
        string userId,
        Guid seasonId,
        bool includeUserMatch = false,
        CancellationToken ct = default);

    Task<LeagueStatisticsDto> GetStatisticsAsync(string userId, Guid seasonId, Guid? competitionId, CancellationToken ct = default);

    Task<HeadToHeadSummary> GetHeadToHeadAsync(string userId, Guid seasonId, Guid teamAId, Guid teamBId, CancellationToken ct = default);

    Task<bool> DeleteAsync(string userId, Guid seasonId, CancellationToken ct = default);
}
