using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TactIQ.Application.Live;

namespace TactIQ.API.Controllers;

[ApiController]
[Authorize]
[Route("api/matches")]
public class MatchesController(ILiveMatchService live) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new InvalidOperationException("Authenticated user has no id claim.");

    /// <summary>Kicks off the manager's own match as a live, ticking session.</summary>
    [HttpPost("{matchId:guid}/start-live")]
    public async Task<ActionResult<LiveStateDto>> StartLive(Guid matchId, StartLiveRequest request, CancellationToken ct) =>
        Ok(await live.StartAsync(UserId, matchId, request, ct));

    [HttpGet("{matchId:guid}/live")]
    public ActionResult<LiveStateDto> GetLive(Guid matchId)
    {
        var state = live.GetState(matchId);
        return state is null ? NotFound() : Ok(state);
    }

    [HttpPatch("{matchId:guid}/tactics")]
    public async Task<ActionResult<LiveStateDto>> UpdateTactics(Guid matchId, TacticsUpdateRequest request, CancellationToken ct) =>
        Ok(await live.UpdateTacticsAsync(UserId, matchId, request, ct));

    [HttpPost("{matchId:guid}/speed")]
    public ActionResult<LiveStateDto> SetSpeed(Guid matchId, SetSpeedRequest request) =>
        Ok(live.SetSpeed(UserId, matchId, request.SpeedMultiplier));

    [HttpPost("{matchId:guid}/pause")]
    public ActionResult<LiveStateDto> Pause(Guid matchId) => Ok(live.SetPaused(UserId, matchId, true));

    [HttpPost("{matchId:guid}/resume")]
    public ActionResult<LiveStateDto> Resume(Guid matchId) => Ok(live.SetPaused(UserId, matchId, false));

    /// <summary>Skips the remaining minutes and settles the match immediately.</summary>
    [HttpPost("{matchId:guid}/finish")]
    public async Task<ActionResult<LiveStateDto>> Finish(Guid matchId, CancellationToken ct) =>
        Ok(await live.FinishNowAsync(UserId, matchId, ct));
}
