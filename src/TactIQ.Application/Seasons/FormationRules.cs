using TactIQ.Application.Simulation;
using TactIQ.Domain.Enums;

namespace TactIQ.Application.Seasons;

/// <summary>
/// The shapes a manager may pick, and how many players each line needs. A
/// formation is written the way football does — defenders-midfielders-forwards,
/// with the keeper left implicit.
/// </summary>
public static class FormationRules
{
    public const int StarterCount = 11;

    private static readonly Dictionary<string, (int Defenders, int Midfielders, int Forwards)> Shapes =
        new(StringComparer.Ordinal)
        {
            ["4-4-2"] = (4, 4, 2),
            ["4-3-3"] = (4, 3, 3),
            ["4-2-3-1"] = (4, 5, 1),
            ["3-5-2"] = (3, 5, 2),
            ["5-3-2"] = (5, 3, 2),
            ["4-5-1"] = (4, 5, 1),
            ["3-4-3"] = (3, 4, 3),
        };

    public static IReadOnlyCollection<string> Available => Shapes.Keys;

    public static bool IsKnown(string formation) => Shapes.ContainsKey(formation);

    /// <summary>How many players each position needs for the given shape.</summary>
    public static IReadOnlyDictionary<Position, int> Requirements(string formation)
    {
        if (!Shapes.TryGetValue(formation, out var shape))
            throw new ArgumentException($"Unknown formation '{formation}'.", nameof(formation));

        return new Dictionary<Position, int>
        {
            [Position.GK] = 1,
            [Position.DEF] = shape.Defenders,
            [Position.MID] = shape.Midfielders,
            [Position.FWD] = shape.Forwards
        };
    }

    /// <summary>
    /// The role each slot of a shape asks for, in line-up order. Slot one is
    /// the keeper, so the list is read with <c>Player.LineupSlot</c> minus one.
    /// </summary>
    public static IReadOnlyList<PlayerRole> SlotRoles(string formation) =>
        FormationShape.Of(formation);
}
