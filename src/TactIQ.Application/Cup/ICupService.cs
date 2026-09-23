namespace TactIQ.Application.Cup;

public interface ICupService
{
    /// <summary>Draws the quarter-finals once the league season is over.</summary>
    Task<CupBracketDto> StartAsync(string userId, Guid leagueId, CancellationToken ct = default);

    Task<CupBracketDto> GetBracketAsync(string userId, Guid leagueId, CancellationToken ct = default);

    /// <summary>Plays the current round, leaving the manager's own tie to be played live.</summary>
    Task<CupRoundResult> PlayRoundAsync(string userId, Guid leagueId, CancellationToken ct = default);
}
