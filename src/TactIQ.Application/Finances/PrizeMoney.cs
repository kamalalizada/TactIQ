using TactIQ.Domain.Enums;

namespace TactIQ.Application.Finances;

/// <summary>
/// What clubs earn from playing.
///
/// Without this a budget only ever moves when somebody sells, so a club that
/// keeps its squad together slowly becomes unable to do anything at all.
/// Results paying their own way closes the loop: a good season funds the next
/// window, and the league a club plays in decides the scale of it, as the
/// television money does in the real game.
/// </summary>
public static class PrizeMoney
{
    /// <summary>
    /// What one league is worth, from a single result up to winning it.
    /// Premier League money dwarfs the rest, which is what makes a club there
    /// able to buy from anywhere.
    /// </summary>
    private readonly record struct LeaguePurse(
        decimal Win,
        decimal Draw,
        decimal Champions,
        decimal Bottom);

    private static readonly Dictionary<string, LeaguePurse> Purses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["England"] = new(1_200_000m, 400_000m, 50_000_000m, 5_000_000m),
        ["Spain"] = new(900_000m, 300_000m, 40_000_000m, 4_000_000m),
        ["Germany"] = new(850_000m, 280_000m, 38_000_000m, 3_800_000m),
        ["Italy"] = new(800_000m, 270_000m, 35_000_000m, 3_500_000m),
        ["Turkey"] = new(400_000m, 150_000m, 18_000_000m, 2_000_000m)
    };

    /// <summary>A league nobody has written a purse for, so a save never earns nothing.</summary>
    private static readonly LeaguePurse Default = new(400_000m, 150_000m, 15_000_000m, 1_500_000m);

    /// <summary>What a club takes from one league match.</summary>
    public static decimal ForResult(string? country, int goalsFor, int goalsAgainst)
    {
        var purse = PurseFor(country);

        if (goalsFor > goalsAgainst)
            return purse.Win;

        return goalsFor == goalsAgainst ? purse.Draw : 0m;
    }

    /// <summary>
    /// What finishing in a given place pays. First place takes the full purse
    /// and last a tenth of it, with every place between them worth something —
    /// so the closing weeks matter to a club in mid-table as well as the two
    /// at the top.
    /// </summary>
    public static decimal ForLeaguePosition(string? country, int position, int teamCount)
    {
        if (teamCount <= 1)
            return 0m;

        var purse = PurseFor(country);
        var place = Math.Clamp(position, 1, teamCount);

        // 1st pays Champions, last pays Bottom, the rest fall evenly between.
        var share = (decimal)(teamCount - place) / (teamCount - 1);
        var prize = purse.Bottom + (purse.Champions - purse.Bottom) * share;

        return Round(prize);
    }

    /// <summary>
    /// What a run in the Champions League pays. Each round is worth more than
    /// the one before it, and the whole run beats winning a domestic league —
    /// which is why a club chases it.
    /// </summary>
    public static decimal ForCupRound(CupRound round, bool won = false) => round switch
    {
        CupRound.GroupStage => 15_000_000m,
        CupRound.RoundOf16 => 10_000_000m,
        CupRound.QuarterFinal => 12_000_000m,
        CupRound.SemiFinal => 15_000_000m,
        CupRound.Final => won ? 45_000_000m : 20_000_000m,
        _ => 0m
    };

    /// <summary>
    /// What a club has earned from reaching a round, counting everything it
    /// banked on the way there.
    /// </summary>
    public static decimal ForCupRun(CupRound reached, bool won)
    {
        var total = 0m;

        foreach (var round in Enum.GetValues<CupRound>())
        {
            if (round > reached)
                break;

            total += ForCupRound(round, won && round == CupRound.Final);
        }

        return total;
    }

    private static LeaguePurse PurseFor(string? country) =>
        country is not null && Purses.TryGetValue(country, out var purse) ? purse : Default;

    /// <summary>Prize money is announced in round figures, not to the euro.</summary>
    private static decimal Round(decimal amount) =>
        Math.Round(amount / 100_000m) * 100_000m;
}
