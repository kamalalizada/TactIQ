namespace TactIQ.Domain.Enums;

/// <summary>Where a bid for a player has got to.</summary>
public enum OfferStatus
{
    /// <summary>Sent, and waiting on the selling club.</summary>
    Pending = 0,

    /// <summary>Agreed; the player has moved.</summary>
    Accepted = 1,

    /// <summary>Turned down, on money or because the club would not sell.</summary>
    Rejected = 2,

    /// <summary>Pulled by the club that made it, before an answer came.</summary>
    Withdrawn = 3,

    /// <summary>Still unanswered when the window shut.</summary>
    Expired = 4
}
