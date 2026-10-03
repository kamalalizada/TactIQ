namespace TactIQ.Application.Transfers;

/// <summary>Buying and selling players during a season's transfer windows.</summary>
public interface ITransferService
{
    /// <summary>Whether the market is open, and what the managed club can spend.</summary>
    Task<TransferWindowDto> GetWindowAsync(string userId, Guid seasonId, CancellationToken ct = default);

    /// <summary>Players at other clubs that the managed club could bid for.</summary>
    Task<IReadOnlyList<MarketPlayerDto>> SearchMarketAsync(
        string userId, Guid seasonId, MarketQuery query, CancellationToken ct = default);

    /// <summary>Every bid the managed club has made or received this season.</summary>
    Task<IReadOnlyList<TransferOfferDto>> GetOffersAsync(
        string userId, Guid seasonId, CancellationToken ct = default);

    /// <summary>Bids for a player, so a manager can see what he is being offered.</summary>
    Task<IReadOnlyList<TransferDto>> GetHistoryAsync(
        string userId, Guid seasonId, CancellationToken ct = default);

    /// <summary>Bid for a player at another club.</summary>
    Task<TransferOfferDto> MakeOfferAsync(
        string userId, Guid seasonId, MakeOfferRequest request, CancellationToken ct = default);

    /// <summary>Accept or turn down a bid for one of the managed club's players.</summary>
    Task<TransferOfferDto> RespondToOfferAsync(
        string userId, Guid seasonId, Guid offerId, bool accept, CancellationToken ct = default);

    /// <summary>Pull a bid that has not been answered yet.</summary>
    Task<TransferOfferDto> WithdrawOfferAsync(
        string userId, Guid seasonId, Guid offerId, CancellationToken ct = default);
}
