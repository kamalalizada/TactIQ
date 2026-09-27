using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TactIQ.Application.Seasons;
using TactIQ.Application.Statistics;

namespace TactIQ.API.Controllers;

[ApiController]
[Authorize]
[Route("api/seasons")]
public class SeasonsController(ISeasonService seasons) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new InvalidOperationException("Authenticated user has no id claim.");

    /// <summary>Starts a save: three domestic leagues, every club, full squads.</summary>
    [HttpPost]
    public async Task<ActionResult<SeasonSummaryDto>> Create(CreateSeasonRequest request, CancellationToken ct)
    {
        var season = await seasons.CreateAsync(UserId, request, ct);
        return CreatedAtAction(nameof(Get), new { seasonId = season.Id }, season);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeasonSummaryDto>>> List(CancellationToken ct) =>
        Ok(await seasons.ListAsync(UserId, ct));

    [HttpGet("{seasonId:guid}")]
    public async Task<ActionResult<SeasonSummaryDto>> Get(Guid seasonId, CancellationToken ct)
    {
        var season = await seasons.GetAsync(UserId, seasonId, ct);
        return season is null ? NotFound() : Ok(season);
    }

    /// <summary>Every club in the save, optionally narrowed to one country.</summary>
    [HttpGet("{seasonId:guid}/teams")]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> Teams(
        Guid seasonId,
        [FromQuery] string? country,
        CancellationToken ct) =>
        Ok(await seasons.GetTeamsAsync(UserId, seasonId, country, ct));

    /// <summary>Picks the club the manager runs, before the season kicks off.</summary>
    [HttpPatch("{seasonId:guid}/user-team")]
    public async Task<ActionResult<SeasonSummaryDto>> SetUserTeam(
        Guid seasonId,
        SetUserTeamRequest request,
        CancellationToken ct) =>
        Ok(await seasons.SetUserTeamAsync(UserId, seasonId, request.TeamId, ct));

    [HttpGet("{seasonId:guid}/statistics")]
    public async Task<ActionResult<LeagueStatisticsDto>> Statistics(
        Guid seasonId,
        [FromQuery] Guid? competitionId,
        CancellationToken ct) =>
        Ok(await seasons.GetStatisticsAsync(UserId, seasonId, competitionId, ct));

    [HttpGet("{seasonId:guid}/head-to-head")]
    public async Task<ActionResult<HeadToHeadSummary>> HeadToHead(
        Guid seasonId,
        [FromQuery] Guid teamA,
        [FromQuery] Guid teamB,
        CancellationToken ct) =>
        Ok(await seasons.GetHeadToHeadAsync(UserId, seasonId, teamA, teamB, ct));

    /// <summary>
    /// Plays the next week of every league at once. Pass includeUserMatch=true
    /// to settle the manager's own match instead of leaving it to be played.
    /// </summary>
    [HttpPost("{seasonId:guid}/simulate-week")]
    public async Task<ActionResult<IReadOnlyList<SimulateWeekResult>>> SimulateWeek(
        Guid seasonId,
        [FromQuery] bool includeUserMatch,
        CancellationToken ct) =>
        Ok(await seasons.SimulateAllLeaguesAsync(UserId, seasonId, includeUserMatch, ct));

    [HttpDelete("{seasonId:guid}")]
    public async Task<IActionResult> Delete(Guid seasonId, CancellationToken ct) =>
        await seasons.DeleteAsync(UserId, seasonId, ct) ? NoContent() : NotFound();
}

[ApiController]
[Authorize]
[Route("api/competitions")]
public class CompetitionsController(ISeasonService seasons) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new InvalidOperationException("Authenticated user has no id claim.");

    [HttpGet("{competitionId:guid}/standings")]
    public async Task<ActionResult<IReadOnlyList<StandingsRowDto>>> Standings(Guid competitionId, CancellationToken ct) =>
        Ok(await seasons.GetStandingsAsync(UserId, competitionId, ct));

    [HttpGet("{competitionId:guid}/fixtures")]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> Fixtures(
        Guid competitionId,
        [FromQuery] int? week,
        CancellationToken ct) =>
        Ok(await seasons.GetFixturesAsync(UserId, competitionId, week, ct));

    /// <summary>Plays one week of a single league.</summary>
    [HttpPost("{competitionId:guid}/simulate-week")]
    public async Task<ActionResult<SimulateWeekResult>> SimulateWeek(
        Guid competitionId,
        [FromQuery] bool includeUserMatch,
        CancellationToken ct) =>
        Ok(await seasons.SimulateWeekAsync(UserId, competitionId, includeUserMatch, ct));
}
