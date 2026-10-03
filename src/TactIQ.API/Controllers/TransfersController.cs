using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TactIQ.Application.Transfers;
using TactIQ.Domain.Enums;

namespace TactIQ.API.Controllers;

[ApiController]
[Authorize]
[Route("api/seasons/{seasonId:guid}/transfers")]
public class TransfersController(ITransferService transfers) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new InvalidOperationException("Authenticated user has no id claim.");

    /// <summary>Whether the market is open, and what the managed club can spend.</summary>
    [HttpGet("window")]
    public async Task<ActionResult<TransferWindowDto>> Window(Guid seasonId, CancellationToken ct) =>
        Ok(await transfers.GetWindowAsync(UserId, seasonId, ct));

    /// <summary>
    /// Players at other clubs, newest signings aside. Every filter is optional,
    /// so an empty query returns the best players in the season.
    /// </summary>
    [HttpGet("market")]
    public async Task<ActionResult<IReadOnlyList<MarketPlayerDto>>> Market(
        Guid seasonId,
        [FromQuery] Position? position,
        [FromQuery] int? minOverall,
        [FromQuery] int? maxAge,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? search,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(await transfers.SearchMarketAsync(
            UserId,
            seasonId,
            new MarketQuery(position, minOverall, maxAge, maxPrice, search, take),
            ct));

    /// <summary>Bids the managed club has made and received.</summary>
    [HttpGet("offers")]
    public async Task<ActionResult<IReadOnlyList<TransferOfferDto>>> Offers(
        Guid seasonId, CancellationToken ct) =>
        Ok(await transfers.GetOffersAsync(UserId, seasonId, ct));

    /// <summary>Every move completed this season, whoever made it.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<TransferDto>>> History(
        Guid seasonId, CancellationToken ct) =>
        Ok(await transfers.GetHistoryAsync(UserId, seasonId, ct));

    /// <summary>
    /// Bid for a player. The selling club answers at once, so the result says
    /// whether the signing went through or what it would have taken.
    /// </summary>
    [HttpPost("offers")]
    public async Task<ActionResult<TransferOfferDto>> MakeOffer(
        Guid seasonId, MakeOfferRequest request, CancellationToken ct) =>
        Ok(await transfers.MakeOfferAsync(UserId, seasonId, request, ct));

    /// <summary>Accept or turn down a bid for one of your players.</summary>
    [HttpPatch("offers/{offerId:guid}")]
    public async Task<ActionResult<TransferOfferDto>> Respond(
        Guid seasonId, Guid offerId, RespondToOfferRequest request, CancellationToken ct) =>
        Ok(await transfers.RespondToOfferAsync(UserId, seasonId, offerId, request.Accept, ct));

    /// <summary>Pull one of your own bids before it is answered.</summary>
    [HttpDelete("offers/{offerId:guid}")]
    public async Task<ActionResult<TransferOfferDto>> Withdraw(
        Guid seasonId, Guid offerId, CancellationToken ct) =>
        Ok(await transfers.WithdrawOfferAsync(UserId, seasonId, offerId, ct));
}
