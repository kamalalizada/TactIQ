using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TactIQ.Application.Cup;

namespace TactIQ.API.Controllers;

[ApiController]
[Authorize]
[Route("api/leagues/{leagueId:guid}/cup")]
public class CupController(ICupService cup) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new InvalidOperationException("Authenticated user has no id claim.");

    /// <summary>Draws the quarter-finals. Only available once the league is over.</summary>
    [HttpPost("start")]
    public async Task<ActionResult<CupBracketDto>> Start(Guid leagueId, CancellationToken ct) =>
        Ok(await cup.StartAsync(UserId, leagueId, ct));

    [HttpGet]
    public async Task<ActionResult<CupBracketDto>> Bracket(Guid leagueId, CancellationToken ct) =>
        Ok(await cup.GetBracketAsync(UserId, leagueId, ct));

    /// <summary>Plays the current round, leaving your own tie to be played live.</summary>
    [HttpPost("play-round")]
    public async Task<ActionResult<CupRoundResult>> PlayRound(Guid leagueId, CancellationToken ct) =>
        Ok(await cup.PlayRoundAsync(UserId, leagueId, ct));
}
