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

    /// <summary>Plays the current round, leaving your own tie to be played live.</summary>
    [HttpPost("play-round")]
    public async Task<ActionResult<CupRoundResult>> PlayRound(Guid seasonId, CancellationToken ct) =>
        Ok(await cup.PlayRoundAsync(UserId, seasonId, ct));
}
