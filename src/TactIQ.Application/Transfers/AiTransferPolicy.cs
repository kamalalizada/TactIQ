using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Transfers;

/// <summary>
/// How a club run by the computer does business.
///
/// A club does not sell on price alone. It asks what the player is to the side:
/// the one who makes the difference is not for sale at any sensible figure, a
/// regular goes for a clear premium, and a reserve is let go for close to his
/// value. It also asks who is left — a club will not sell its last fit
/// centre-back however good the money is.
/// </summary>
public static class AiTransferPolicy
{
    /// <summary>What a club wants above the asking price, by how much it needs the player.</summary>
    private const decimal StarPremium = 1.80m;
    private const decimal RegularPremium = 1.35m;
    private const decimal ReservePremium = 1.00m;

    /// <summary>
    /// A player this far below his club's best is a reserve; this far above its
    /// average is the one it is built around.
    /// </summary>
    private const int StarMargin = 2;

    public readonly record struct Verdict(bool Accepted, string? Reason);

    /// <summary>What the selling club makes of a bid.</summary>
    public static Verdict ConsiderOffer(Player player, Team club, decimal amount)
    {
        if (SquadLimits.WhyCannotSell(club, player) is { } kept)
            return new Verdict(false, kept);

        var asking = AskingPrice(player, club);

        if (amount < asking)
            return new Verdict(false, $"Klub {Money(asking)} istəyir.");

        return new Verdict(true, null);
    }

    /// <summary>
    /// What this club would take for this player. It is the market value marked
    /// up by how much the side depends on him, so the same player costs more to
    /// prise out of a squad that has nobody else like him.
    /// </summary>
    public static decimal AskingPrice(Player player, Team club)
    {
        var value = PlayerValuation.Of(player);
        var premium = StandingOf(player, club) switch
        {
            Standing.Star => StarPremium,
            Standing.Regular => RegularPremium,
            _ => ReservePremium
        };

        return Math.Round(value * premium / 100_000m) * 100_000m;
    }

    private enum Standing { Star, Regular, Reserve }

    private static Standing StandingOf(Player player, Team club)
    {
        var squad = club.Players;

        if (squad.Count == 0)
            return Standing.Regular;

        var best = squad.Max(p => p.Overall);
        var average = squad.Average(p => p.Overall);

        if (player.Overall >= best - StarMargin)
            return Standing.Star;

        return player.Overall >= average ? Standing.Regular : Standing.Reserve;
    }

    /// <summary>
    /// What a club would pay for a player it wants: enough over the asking
    /// price to be taken seriously, and never more than it has.
    /// </summary>
    public static decimal BidFor(Player player, Team seller, Team buyer, Random random)
    {
        var asking = AskingPrice(player, seller);
        var keen = 1.00m + (decimal)random.NextDouble() * 0.15m;

        return Math.Min(buyer.Budget, Math.Round(asking * keen / 100_000m) * 100_000m);
    }

    /// <summary>
    /// Whether this club would want this player at all: he has to be better
    /// than what it already has in that position, and within its means.
    /// </summary>
    public static bool Wants(Player player, Team buyer)
    {
        if (!SquadLimits.CanBuy(buyer))
            return false;

        var bestInPosition = buyer.Players
            .Where(p => p.Position == player.Position)
            .Select(p => p.Overall)
            .DefaultIfEmpty(0)
            .Max();

        return player.Overall > bestInPosition;
    }

    /// <summary>
    /// The line this club is weakest in, which is where it will shop. Null when
    /// its squad is already full and nothing needs doing.
    /// </summary>
    public static Position? WeakestLine(Team club)
    {
        if (!SquadLimits.CanBuy(club))
            return null;

        var lines = club.Players
            .GroupBy(p => p.Position)
            .Select(g => (Position: g.Key, Strength: g.OrderByDescending(p => p.Overall)
                                                      .Take(4)
                                                      .Average(p => p.Overall)))
            .ToList();

        return lines.Count == 0 ? null : lines.OrderBy(l => l.Strength).First().Position;
    }

    private static string Money(decimal amount) => amount >= 1_000_000m
        ? $"{amount / 1_000_000m:0.#} mln €"
        : $"{amount / 1_000m:0} min €";
}
