using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

public class Player
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public Position Position { get; set; }
    public int ShirtNumber { get; set; }

    public int Pace { get; set; }
    public int Shooting { get; set; }
    public int Passing { get; set; }
    public int Dribbling { get; set; }
    public int Defending { get; set; }
    public int Physical { get; set; }

    public bool IsStarter { get; set; }

    public int Overall =>
        (int)Math.Round((Pace + Shooting + Passing + Dribbling + Defending + Physical) / 6.0);
}
