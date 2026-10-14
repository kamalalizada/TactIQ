using TactIQ.Application.Transfers;
using TactIQ.Domain.Entities;

namespace TactIQ.Application.Careers;

/// <summary>
/// What a year does to a player.
///
/// A squad that never changed would make every season the same season: the
/// same eleven, the same table, the same transfer targets. Age is what keeps a
/// save moving — the young get better and become worth buying, the old decline
/// and have to be replaced, and a manager who ignores both ends up with a
/// thirty-four-year-old defence and nothing behind it.
/// </summary>
public static class PlayerDevelopment
{
    /// <summary>Past this age a career ends, however good the player still is.</summary>
    public const int RetirementAge = 37;

    /// <summary>Below this rating a professional is finished whatever his age.</summary>
    public const int FloorRating = 52;

    /// <summary>Nobody grows beyond this in a career.</summary>
    private const int Ceiling = 94;

    /// <summary>What happened to one player over the summer.</summary>
    public readonly record struct Change(int Before, int After, bool Retired)
    {
        public int Delta => After - Before;
        public bool Improved => Delta > 0;
        public bool Declined => Delta < 0;
    }

    /// <summary>
    /// Ages a player a year and moves his rating with it.
    ///
    /// The swing is widest at the two ends of a career, where a season really
    /// can change what a player is, and narrowest through the middle years
    /// where he is simply himself.
    /// </summary>
    public static Change Advance(Player player, Random random)
    {
        var before = player.Overall;

        // Decided before anything is written, so a player who is leaving the
        // game is not also left with a changed row: a record cannot be both
        // updated and deleted in one save.
        if (player.Age + 1 > RetirementAge)
            return new Change(before, before, Retired: true);

        player.Age++;

        var delta = DeltaFor(player.Age, before, random);
        var after = Math.Clamp(before + delta, FloorRating - 6, Ceiling);

        // A player who has fallen below what the game is played at retires
        // rather than lingering in a squad nobody will pick him for. Again the
        // call comes before the write, so nothing is changed on the way out.
        if (after < FloorRating)
        {
            player.Age--;
            return new Change(before, before, Retired: true);
        }

        player.Overall = after;
        ScaleAttributes(player, before, after);
        player.MarketValue = PlayerValuation.Revalue(
            player.MarketValue, before, player.Age - 1, after, player.Age, player.Position);

        return new Change(before, after, Retired: false);
    }

    /// <summary>
    /// How much a player of this age moves in a year. A rating near the top is
    /// harder to add to than one in the middle, so the same age gains less the
    /// better the player already is.
    /// </summary>
    private static int DeltaFor(int age, int overall, Random random)
    {
        var (low, high) = age switch
        {
            <= 19 => (1, 5),
            <= 21 => (1, 4),
            <= 23 => (0, 3),

            // A footballer comes into his own in his middle twenties and holds
            // it to about twenty-eight; those are the seasons a club buys.
            <= 27 => (0, 2),
            <= 29 => (-1, 1),

            <= 31 => (-2, 0),
            <= 33 => (-3, -1),
            _ => (-5, -1)
        };

        var delta = random.Next(low, high + 1);

        // Room at the top is scarce: improving an 88 is far harder than a 68,
        // and nobody at the very top improves by accident.
        if (delta > 0 && overall >= 88)
            delta = 0;
        else if (delta > 0 && overall >= 84)
            delta = random.Next(0, 2);

        return delta;
    }

    /// <summary>
    /// Moves the attributes with the rating, so a player who improved is better
    /// at what he does rather than carrying the same numbers under a new total.
    /// Pace is the first thing a footballer loses, and the last he regains.
    /// </summary>
    private static void ScaleAttributes(Player player, int before, int after)
    {
        var delta = after - before;
        if (delta == 0)
            return;

        var pace = delta < 0 ? delta - 1 : delta;

        player.Pace = Clamp(player.Pace + pace);
        player.Shooting = Clamp(player.Shooting + delta);
        player.Passing = Clamp(player.Passing + delta);
        player.Dribbling = Clamp(player.Dribbling + delta);
        player.Defending = Clamp(player.Defending + delta);

        // Strength holds up where speed does not, so it declines more slowly.
        player.Physical = Clamp(player.Physical + (delta < 0 ? delta + 1 : delta));
    }

    private static int Clamp(int value) => Math.Clamp(value, 20, 99);
}
