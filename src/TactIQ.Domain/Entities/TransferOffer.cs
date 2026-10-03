using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

/// <summary>
/// A bid one club has made to another for a player.
///
/// An offer outlives its answer: a manager should be able to look back at what
/// was turned down and for how much, so the row stays with its status set
/// rather than being deleted.
/// </summary>
public class TransferOffer
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>The club being asked to sell.</summary>
    public Guid FromTeamId { get; set; }

    /// <summary>The club bidding.</summary>
    public Guid ToTeamId { get; set; }

    public decimal Amount { get; set; }

    public OfferStatus Status { get; set; } = OfferStatus.Pending;

    /// <summary>The season week the bid was made in, so it can be expired.</summary>
    public int WeekMade { get; set; }

    /// <summary>Why a club said no, in the manager's language.</summary>
    public string? ResponseReason { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>True while the offer is still waiting on an answer.</summary>
    public bool IsOpen => Status == OfferStatus.Pending;
}
