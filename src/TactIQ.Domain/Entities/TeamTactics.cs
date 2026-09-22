using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

public class TeamTactics
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public Guid TeamId { get; set; }
    public int Minute { get; set; }

    public string Formation { get; set; } = "4-4-2";
    public TacticalStyle Style { get; set; } = TacticalStyle.Balanced;

    public string? PlayerChangesJson { get; set; }
}
