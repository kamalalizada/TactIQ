using TactIQ.Application.Seeding;
using TactIQ.Domain.Entities;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Transfers;

/// <summary>
/// What a squad must still look like after a sale.
///
/// A club that sold freely would end the window unable to field a side, or
/// with nobody in goal. These are the floors every club is held to — the user's
/// and the computer's alike — so a season cannot be broken by a transfer.
/// </summary>
public static class SquadLimits
{
    /// <summary>
    /// The fewest a club may carry: an eleven and the five changes a match
    /// allows. A seeded squad of nineteen can therefore sell three before the
    /// market closes to it, which is room to do business without any club ever
    /// being left unable to name a bench.
    /// </summary>
    public const int MinimumSquad = SquadRules.StarterCount + SquadRules.MaxSubstitutions;

    /// <summary>
    /// Beyond this a squad is unmanageable, and the wage bill fiction breaks.
    /// Wide enough for a real first team with its reserve keepers and academy
    /// players behind it, rather than the bare matchday twenty-five.
    /// </summary>
    public const int MaximumSquad = 32;

    /// <summary>
    /// The fewest a club may keep in each line: enough to fill the widest shape
    /// it might play and still have someone to bring on. Two keepers, because
    /// the one in goal gets injured like anybody else.
    /// </summary>
    private static readonly Dictionary<Position, int> Floors = new()
    {
        [Position.GK] = 2,
        [Position.DEF] = 4,
        [Position.MID] = 4,
        [Position.FWD] = 3
    };

    public static int FloorFor(Position position) => Floors[position];

    /// <summary>
    /// Why this club cannot sell this player, or null if it can. The message is
    /// shown to the manager, so it says what is actually in the way.
    /// </summary>
    public static string? WhyCannotSell(Team club, Player player)
    {
        if (club.Players.Count <= MinimumSquad)
            return $"Heyətdə {MinimumSquad} oyunçudan az qala bilməz.";

        var left = club.Players.Count(p => p.Position == player.Position && p.Id != player.Id);
        var floor = FloorFor(player.Position);

        return left < floor
            ? $"Bu mövqedə ən azı {floor} oyunçu qalmalıdır."
            : null;
    }

    /// <summary>Why this club cannot sign anyone else, or null if it can.</summary>
    public static string? WhyCannotBuy(Team club) =>
        club.Players.Count >= MaximumSquad
            ? $"Heyətdə {MaximumSquad} oyunçudan çox ola bilməz."
            : null;

    public static bool CanSell(Team club, Player player) => WhyCannotSell(club, player) is null;

    public static bool CanBuy(Team club) => WhyCannotBuy(club) is null;
}
