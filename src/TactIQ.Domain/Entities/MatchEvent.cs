using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

public class MatchEvent
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public int Minute { get; set; }
    public MatchEventType EventType { get; set; }

    public Guid TeamId { get; set; }
    public Guid? PlayerId { get; set; }

    /// <summary>Set on goals that came from an assist.</summary>
    public Guid? AssistPlayerId { get; set; }

    public string Description { get; set; } = string.Empty;
}
