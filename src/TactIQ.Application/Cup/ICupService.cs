namespace TactIQ.Application.Cup;

public interface ICupService
{
    /// <summary>Draws the group stage from the leading clubs of every league.</summary>
    Task<CupBracketDto> StartAsync(string userId, Guid seasonId, CancellationToken ct = default);

    Task<CupBracketDto> GetBracketAsync(string userId, Guid seasonId, CancellationToken ct = default);

    /// <summary>Plays the current round, leaving the manager's own tie to be played live.</summary>
    Task<CupRoundResult> PlayRoundAsync(string userId, Guid seasonId, CancellationToken ct = default);
}
