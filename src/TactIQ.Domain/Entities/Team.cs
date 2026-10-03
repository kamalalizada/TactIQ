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

    /// <summary>
    /// The shape the manager wants to play, as "4-4-2". Kept on the club so a
    /// chosen line-up survives between matches; the per-match tactics record
    /// still holds whatever was actually used on the day.
    /// </summary>
    public string Formation { get; set; } = "4-4-2";

    /// <summary>
    /// What the club can spend on transfers, in euros. Selling adds to it and
    /// buying takes from it, so a manager who wants a striker has to find the
    /// money somewhere.
    /// </summary>
    public decimal Budget { get; set; }

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
