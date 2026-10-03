using TactIQ.Domain.Enums;

namespace TactIQ.Application.Transfers;

/// <summary>
/// When the market is open.
///
/// A manager who could buy at any time would never have to live with a
/// mistake: a bad run would simply be bought out of. Two windows — one before
/// the season, one at the halfway point — make a squad a decision rather than a
/// running total, and give the January scramble its weight.
/// </summary>
public static class TransferWindowRules
{
    /// <summary>Weeks the summer window stays open, counted from the start.</summary>
    public const int SummerLastWeek = 1;

    /// <summary>How long the winter window lasts once it opens.</summary>
    public const int WinterLength = 2;

    /// <summary>
    /// The week the winter window opens, halfway through the league season.
    /// </summary>
    public static int WinterOpensAt(int totalWeeks) => Math.Max(2, totalWeeks / 2);

    /// <summary>Which window, if any, is open in the given week.</summary>
    public static TransferWindow At(int week, int totalWeeks)
    {
        if (week <= SummerLastWeek)
            return TransferWindow.Summer;

        var winterOpens = WinterOpensAt(totalWeeks);

        return week >= winterOpens && week < winterOpens + WinterLength
            ? TransferWindow.Winter
            : TransferWindow.Closed;
    }

    public static bool IsOpen(int week, int totalWeeks) =>
        At(week, totalWeeks) != TransferWindow.Closed;

    /// <summary>
    /// The last week of the window now open, so a manager knows how long they
    /// have left. Null when the market is shut.
    /// </summary>
    public static int? ClosesAfter(int week, int totalWeeks) => At(week, totalWeeks) switch
    {
        TransferWindow.Summer => SummerLastWeek,
        TransferWindow.Winter => WinterOpensAt(totalWeeks) + WinterLength - 1,
        _ => null
    };

    /// <summary>
    /// The week the market next opens, or null once the winter window has been
    /// and gone — after that the squad is settled for the rest of the season.
    /// </summary>
    public static int? OpensAt(int week, int totalWeeks)
    {
        if (IsOpen(week, totalWeeks))
            return null;

        var winterOpens = WinterOpensAt(totalWeeks);

        return week < winterOpens ? winterOpens : null;
    }
}
