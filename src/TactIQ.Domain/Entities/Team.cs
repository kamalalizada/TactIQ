using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

public class Team
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }

    /// <summary>The league this club belongs to, which also decides who it can
    /// meet domestically.</summary>
    public string Country { get; set; } = string.Empty;

    public List<Player> Players { get; set; } = [];

    public double AttackRating => RatingFor(
        [Position.FWD, Position.MID],
        p => (p.Shooting + p.Pace + p.Dribbling) / 3.0);

    public double DefenseRating => RatingFor(
        [Position.DEF, Position.GK, Position.MID],
        p => (p.Defending + p.Physical + p.Pace) / 3.0);

    public double TeamOverall
    {
        get
        {
            var starters = Starters();
            return starters.Count == 0 ? 0 : starters.Average(p => p.Overall);
        }
    }

    private List<Player> Starters() => Players.Where(p => p.IsStarter).ToList();

    private double RatingFor(Position[] positions, Func<Player, double> score)
    {
        var relevant = Starters().Where(p => positions.Contains(p.Position)).ToList();
        return relevant.Count == 0 ? 0 : relevant.Average(score);
    }
}
