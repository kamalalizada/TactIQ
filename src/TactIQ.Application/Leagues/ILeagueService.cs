using TactIQ.Application.Statistics;

namespace TactIQ.Application.Leagues;

public interface ILeagueService
{
    Task<LeagueSummaryDto> CreateAsync(string userId, CreateLeagueRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<LeagueSummaryDto>> ListAsync(string userId, CancellationToken ct = default);

    Task<LeagueSummaryDto?> GetAsync(string userId, Guid leagueId, CancellationToken ct = default);

    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(string userId, Guid leagueId, CancellationToken ct = default);

    Task<IReadOnlyList<StandingsRowDto>> GetStandingsAsync(string userId, Guid leagueId, CancellationToken ct = default);

    Task<IReadOnlyList<MatchDto>> GetFixturesAsync(string userId, Guid leagueId, int? week, CancellationToken ct = default);

    Task<SimulateWeekResult> SimulateWeekAsync(string userId, Guid leagueId, CancellationToken ct = default);

    Task<LeagueStatisticsDto> GetStatisticsAsync(string userId, Guid leagueId, CancellationToken ct = default);

    Task<HeadToHeadSummary> GetHeadToHeadAsync(string userId, Guid leagueId, Guid teamAId, Guid teamBId, CancellationToken ct = default);

    Task<bool> DeleteAsync(string userId, Guid leagueId, CancellationToken ct = default);
}
