using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TactIQ.Application.Cup;

namespace TactIQ.API.Controllers;

[ApiController]
[Authorize]
[Route("api/seasons/{seasonId:guid}/champions-league")]
public class CupController(ICupService cup) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new InvalidOperationException("Authenticated user has no id claim.");

    /// <summary>Draws the group stage. Only available once every league has finished.</summary>
    [HttpPost("start")]
    public async Task<ActionResult<CupBracketDto>> Start(Guid seasonId, CancellationToken ct) =>
        Ok(await cup.StartAsync(UserId, seasonId, ct));

    [HttpGet]
    public async Task<ActionResult<CupBracketDto>> Bracket(Guid seasonId, CancellationToken ct) =>
        Ok(await cup.GetBracketAsync(UserId, seasonId, ct));

    /// <summary>
    /// Plays the current round. Your own tie is left to be played live; pass
    /// includeUserMatch=true to have it settled with the rest instead.
    /// </summary>
    [HttpPost("play-round")]
    public async Task<ActionResult<CupRoundResult>> PlayRound(
        Guid seasonId,
        [FromQuery] bool includeUserMatch,
        CancellationToken ct) =>
        Ok(await cup.PlayRoundAsync(UserId, seasonId, includeUserMatch, ct));

    /// <summary>
    /// Simulates all remaining matches in the cup until it finishes, 
    /// or until a match involving the user's team is reached (if includeUserMatch is false).
    /// </summary>
    [HttpPost("play-all")]
    public async Task<ActionResult<CupRoundResult>> PlayAll(
        Guid seasonId,
        [FromQuery] bool includeUserMatch,
        CancellationToken ct) =>
        Ok(await cup.PlayAllAsync(UserId, seasonId, includeUserMatch, ct));
}
