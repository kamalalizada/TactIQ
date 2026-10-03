using TactIQ.Domain.Entities;

namespace TactIQ.Application.Transfers;

/// <summary>
/// What a club has to spend when a save begins.
///
/// Money follows standing: the clubs at the top of the richest leagues can buy
/// almost anyone, and the ones at the bottom are shopping in a different market
/// altogether. Tying the budget to squad value keeps that true without a second
/// set of numbers to maintain — a club that has spent well has more to spend.
/// </summary>
public static class ClubFinances
{
    /// <summary>
    /// A club starts with a share of what its squad is worth. A quarter is
    /// enough to sign one player of the standard it already has, which is what
    /// a window usually buys, rather than enough to rebuild the side.
    /// </summary>
    private const decimal ShareOfSquadValue = 0.25m;

    /// <summary>
    /// Even the smallest club can do some business, so nobody sits out the
    /// window entirely.
    /// </summary>
    private const decimal Minimum = 2_000_000m;

    public static decimal StartingBudget(IEnumerable<Player> squad)
    {
        var worth = squad.Sum(PlayerValuation.Of);

        return Math.Max(Minimum, Math.Round(worth * ShareOfSquadValue / 500_000m) * 500_000m);
    }
}
