namespace TactIQ.Application.Live;

public interface ILiveMatchService
{
    Task<LiveStateDto> StartAsync(string userId, Guid matchId, StartLiveRequest request, CancellationToken ct = default);

    LiveStateDto? GetState(Guid matchId);

    Task<LiveStateDto> UpdateTacticsAsync(string userId, Guid matchId, TacticsUpdateRequest request, CancellationToken ct = default);

    LiveStateDto SetPaused(string userId, Guid matchId, bool paused);

    /// <summary>Switches between 1x, 2x and 4x while the match is running.</summary>
    LiveStateDto SetSpeed(string userId, Guid matchId, int speedMultiplier);

    /// <summary>Skips to the final whistle, simulating every remaining minute at once.</summary>
    Task<LiveStateDto> FinishNowAsync(string userId, Guid matchId, CancellationToken ct = default);
}
