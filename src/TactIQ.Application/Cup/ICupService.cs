namespace TactIQ.Application.Cup;

public interface ICupService
{
    /// <summary>Draws the group stage from the leading clubs of every league.</summary>
    Task<CupBracketDto> StartAsync(string userId, Guid seasonId, CancellationToken ct = default);

    Task<CupBracketDto> GetBracketAsync(string userId, Guid seasonId, CancellationToken ct = default);

    /// <summary>
    /// Plays the current round. The manager's own match is left pending unless
    /// <paramref name="includeUserMatch"/> asks for it to be settled along with
    /// the rest, which is what lets a round finish without them playing it live.
    /// </summary>
    Task<CupRoundResult> PlayRoundAsync(
        string userId,
        Guid seasonId,
        bool includeUserMatch = false,
        CancellationToken ct = default);

    /// <summary>
    /// Repeatedly plays rounds until the competition finishes, or until a match involving
    /// the manager is reached (if includeUserMatch is false).
    /// </summary>
    Task<CupRoundResult> PlayAllAsync(
        string userId,
        Guid seasonId,
        bool includeUserMatch = false,
        CancellationToken ct = default);
}
