using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TactIQ.Application.Leagues;
using TactIQ.Application.Statistics;

namespace TactIQ.API.Controllers;

[ApiController]
[Authorize]
[Route("api/leagues")]
public class LeaguesController(ILeagueService leagues) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new InvalidOperationException("Authenticated user has no id claim.");

    [HttpPost]
    public async Task<ActionResult<LeagueSummaryDto>> Create(CreateLeagueRequest request, CancellationToken ct)
    {
        var league = await leagues.CreateAsync(UserId, request, ct);
        return CreatedAtAction(nameof(Get), new { leagueId = league.Id }, league);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeagueSummaryDto>>> List(CancellationToken ct) =>
        Ok(await leagues.ListAsync(UserId, ct));

    [HttpGet("{leagueId:guid}")]
    public async Task<ActionResult<LeagueSummaryDto>> Get(Guid leagueId, CancellationToken ct)
    {
        var league = await leagues.GetAsync(UserId, leagueId, ct);
        return league is null ? NotFound() : Ok(league);
    }

    [HttpGet("{leagueId:guid}/teams")]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> Teams(Guid leagueId, CancellationToken ct) =>
        Ok(await leagues.GetTeamsAsync(UserId, leagueId, ct));

    [HttpGet("{leagueId:guid}/standings")]
    public async Task<ActionResult<IReadOnlyList<StandingsRowDto>>> Standings(Guid leagueId, CancellationToken ct) =>
        Ok(await leagues.GetStandingsAsync(UserId, leagueId, ct));

    [HttpGet("{leagueId:guid}/fixtures")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> Fixtures(Guid leagueId, [FromQuery] int? week, CancellationToken ct) =>
        Ok(await leagues.GetFixturesAsync(UserId, leagueId, week, ct));

    [HttpPost("{leagueId:guid}/simulate-week")]
    public async Task<ActionResult<SimulateWeekResult>> SimulateWeek(Guid leagueId, CancellationToken ct) =>
        Ok(await leagues.SimulateWeekAsync(UserId, leagueId, ct));

    /// <summary>Scoring charts, discipline and recent form for the whole league.</summary>
    [HttpGet("{leagueId:guid}/statistics")]
    public async Task<ActionResult<LeagueStatisticsDto>> Statistics(Guid leagueId, CancellationToken ct) =>
        Ok(await leagues.GetStatisticsAsync(UserId, leagueId, ct));

    [HttpGet("{leagueId:guid}/head-to-head")]
    public async Task<ActionResult<HeadToHeadSummary>> HeadToHead(
        Guid leagueId,
        [FromQuery] Guid teamA,
        [FromQuery] Guid teamB,
        CancellationToken ct) =>
        Ok(await leagues.GetHeadToHeadAsync(UserId, leagueId, teamA, teamB, ct));

    [HttpDelete("{leagueId:guid}")]
    public async Task<IActionResult> Delete(Guid leagueId, CancellationToken ct) =>
        await leagues.DeleteAsync(UserId, leagueId, ct) ? NoContent() : NotFound();
}
