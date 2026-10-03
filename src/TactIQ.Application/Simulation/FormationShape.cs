using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

/// <summary>
/// What each shirt in a formation is asked to do, in the order a line-up is
/// read: the keeper first, then every line left to right.
///
/// A count of defenders is not enough to tell a 4-3-3 from a 4-4-2 — both field
/// four at the back. What separates them is the job each of those four does, and
/// whether the men in front of them are wingers or wide midfielders. A player
/// standing in a slot that is not their own role plays below their rating, so
/// this table is what makes the choice of shape matter.
/// </summary>
public static class FormationShape
{
    private static readonly Dictionary<string, PlayerRole[]> Slots =
        new(StringComparer.Ordinal)
        {
            ["4-4-2"] =
            [
                PlayerRole.GK,
                PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
                PlayerRole.LM, PlayerRole.CM, PlayerRole.CM, PlayerRole.RM,
                PlayerRole.ST, PlayerRole.ST
            ],
            ["4-3-3"] =
            [
                PlayerRole.GK,
                PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
                PlayerRole.CM, PlayerRole.CDM, PlayerRole.CM,
                PlayerRole.LW, PlayerRole.ST, PlayerRole.RW
            ],
            ["4-2-3-1"] =
            [
                PlayerRole.GK,
                PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
                PlayerRole.CDM, PlayerRole.CDM,
                PlayerRole.LM, PlayerRole.CAM, PlayerRole.RM,
                PlayerRole.ST
            ],
            ["3-5-2"] =
            [
                PlayerRole.GK,
                PlayerRole.CB, PlayerRole.CB, PlayerRole.CB,
                PlayerRole.LM, PlayerRole.CM, PlayerRole.CDM, PlayerRole.CM, PlayerRole.RM,
                PlayerRole.ST, PlayerRole.ST
            ],
            ["5-3-2"] =
            [
                PlayerRole.GK,
                PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
                PlayerRole.CM, PlayerRole.CDM, PlayerRole.CM,
                PlayerRole.ST, PlayerRole.ST
            ],
            ["4-5-1"] =
            [
                PlayerRole.GK,
                PlayerRole.LB, PlayerRole.CB, PlayerRole.CB, PlayerRole.RB,
                PlayerRole.LM, PlayerRole.CM, PlayerRole.CDM, PlayerRole.CM, PlayerRole.RM,
                PlayerRole.ST
            ],
            ["3-4-3"] =
            [
                PlayerRole.GK,
                PlayerRole.CB, PlayerRole.CB, PlayerRole.CB,
                PlayerRole.LM, PlayerRole.CM, PlayerRole.CM, PlayerRole.RM,
                PlayerRole.LW, PlayerRole.ST, PlayerRole.RW
            ],
        };

    public static IReadOnlyCollection<string> Available => Slots.Keys;

    public static bool IsKnown(string formation) => Slots.ContainsKey(formation);

    /// <summary>The role each shirt asks for, slot one being the keeper.</summary>
    public static IReadOnlyList<PlayerRole> Of(string formation) =>
        Slots.TryGetValue(formation, out var roles)
            ? roles
            : throw new ArgumentException($"Unknown formation '{formation}'.", nameof(formation));

    /// <summary>
    /// The role a given shirt is asked to play. Where the shape does not say —
    /// an unknown formation, or a substitute who stands nowhere yet — the
    /// player keeps their own role and so plays at full strength.
    /// </summary>
    public static PlayerRole RoleForSlot(string formation, int lineupSlot, PlayerRole fallback)
    {
        if (!Slots.TryGetValue(formation, out var roles))
            return fallback;

        return lineupSlot >= 1 && lineupSlot <= roles.Length
            ? roles[lineupSlot - 1]
            : fallback;
    }
}
