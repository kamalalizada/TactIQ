using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TactIQ.Application.Careers;

namespace TactIQ.API.Controllers;

[ApiController]
[Authorize]
[Route("api/seasons/{seasonId:guid}/career")]
public class CareerController(ICareerService career) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new InvalidOperationException("Authenticated user has no id claim.");

    /// <summary>The manager's record, and whether the season can be closed.</summary>
    [HttpGet]
    public async Task<ActionResult<CareerDto>> Get(Guid seasonId, CancellationToken ct) =>
        Ok(await career.GetAsync(UserId, seasonId, ct));

    /// <summary>
    /// Closes the season and opens the next one: squads age, careers end,
    /// youth players come through and the fixtures are redrawn. The squad, the
    /// budget and the record of what happened all carry over.
    /// </summary>
    [HttpPost("next-season")]
    public async Task<ActionResult<SeasonRolloverDto>> NextSeason(Guid seasonId, CancellationToken ct) =>
        Ok(await career.AdvanceAsync(UserId, seasonId, ct));
}
