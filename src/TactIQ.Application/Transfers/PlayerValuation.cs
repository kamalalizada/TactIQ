using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Transfers;

/// <summary>
/// What a player is worth on the market.
///
/// Football's prices are not linear in ability: the gap between a good player
/// and a very good one costs far more than the gap between an average player
/// and a good one, because there are only so many of the very good ones. So
/// the rating is raised to a power rather than scaled, and the result is then
/// bent by age — a 22-year-old at the same rating is an asset for a decade,
/// while a 34-year-old is a season or two and then nothing.
/// </summary>
public static class PlayerValuation
{
    /// <summary>A rating below this is worth little more than squad filler.</summary>
    private const int Floor = 55;

    /// <summary>Euros a player at the floor is worth.</summary>
    private const decimal Base = 150_000m;

    /// <summary>
    /// How sharply price climbs with ability. Each rating point adds about a
    /// fifth again, so 70 is worth roughly €2m, 80 near €12m, 85 about €28m and
    /// 91 near €85m — the shape of a real market, where the handful of players
    /// at the top cost many times what a merely very good one does.
    /// </summary>
    private const double Steepness = 1.19;

    /// <summary>Nobody is priced beyond this, however the numbers fall.</summary>
    private const decimal Ceiling = 200_000_000m;

    public static decimal Of(Player player) =>
        Of(player.Overall, player.Age, player.Position);

    public static decimal Of(int overall, int age, Position position)
    {
        if (overall <= Floor)
            return Base;

        var ability = (decimal)Math.Pow(Steepness, overall - Floor) * Base;
        var value = ability * (decimal)AgeFactor(age) * (decimal)PositionFactor(position);

        // Prices are quoted in round numbers, not to the euro.
        var rounded = Math.Round(value / 100_000m) * 100_000m;

        return Math.Clamp(rounded, Base, Ceiling);
    }

    /// <summary>
    /// What age does to a price. A player comes into their worth in their
    /// early twenties, holds it to about twenty-seven, and then loses it — at
    /// first slowly, and after thirty quickly, because a buying club is paying
    /// for the seasons still to come.
    /// </summary>
    private static double AgeFactor(int age) => age switch
    {
        <= 19 => 1.30,
        20 => 1.28,
        21 => 1.25,
        22 => 1.18,
        23 => 1.10,
        >= 24 and <= 27 => 1.00,
        28 => 0.92,
        29 => 0.82,
        30 => 0.68,
        31 => 0.54,
        32 => 0.42,
        33 => 0.30,
        34 => 0.21,
        35 => 0.14,
        _ => 0.08
    };

    /// <summary>
    /// What the position does. Goals are the scarcest thing in football and
    /// priced accordingly; goalkeepers last longer and change hands for less,
    /// so the same rating costs a club rather less in goal than up front.
    /// </summary>
    private static double PositionFactor(Position position) => position switch
    {
        Position.GK => 0.75,
        Position.DEF => 0.92,
        Position.MID => 1.00,
        _ => 1.15
    };
}
