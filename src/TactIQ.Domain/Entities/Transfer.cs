using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

/// <summary>
/// A move that went through. The player's own row says which club they are at
/// now; this says where they came from, what was paid and when — which is the
/// only record of a squad that no longer exists in that form.
/// </summary>
public class Transfer
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public Guid PlayerId { get; set; }

    /// <summary>Kept alongside the id: a player may move on again later.</summary>
    public string PlayerName { get; set; } = string.Empty;

    public Guid FromTeamId { get; set; }
    public string FromTeamName { get; set; } = string.Empty;

    public Guid ToTeamId { get; set; }
    public string ToTeamName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int Week { get; set; }

    public TransferWindow Window { get; set; }

    public DateTime CompletedAt { get; set; }
}
