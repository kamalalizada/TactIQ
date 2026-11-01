using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

/// <summary>
/// Reads a job off where a player stands. A manager places each starter
/// anywhere on the pitch; the spot, not the player, decides the role — a
/// centre-back carried into goal keeps goal, and is rated as a keeper.
///
/// Coordinates are percentages of the pitch as the manager sees it, attacking
/// upwards: <c>x</c> runs from the left touchline (0) to the right (100), and
/// <c>y</c> from the side's own goal line (0) to the opponent's (100).
///
/// The frontend draws the same zones, so the two must change together.
/// </summary>
public static class PitchZones
{
    /// <summary>The goalmouth: only a keeper stands this deep and this central.</summary>
    private const double KeeperDepth = 13;
    private const double KeeperLeft = 25;
    private const double KeeperRight = 75;

    /// <summary>The back line runs from the goalmouth to here.</summary>
    private const double DefenceDepth = 35;
    /// <summary>A defender this close to a touchline is a full-back.</summary>
    private const double FullBackLane = 28;
    /// <summary>
    /// A wide defender pushed beyond here is a wing-back: high enough to be
    /// the flank's whole width, still behind the midfield line. The squad
    /// screen draws a full-back at 24, so this leaves him room to be nudged
    /// about without quietly changing job.
    /// </summary>
    private const double WingBackDepth = 30;

    /// <summary>Midfield runs from the back line to here; beyond it is attack.</summary>
    private const double MidfieldDepth = 68;
    /// <summary>A midfielder this close to a touchline plays wide.</summary>
    private const double WideMidfieldLane = 25;
    /// <summary>Central midfield splits into holding, central and attacking bands.</summary>
    private const double HoldingDepth = 46;
    private const double CentralDepth = 58;

    /// <summary>A forward this close to a touchline is a winger.</summary>
    private const double WingerLane = 30;

    public const double Min = 0;
    public const double Max = 100;

    public static bool IsOnPitch(double x, double y) =>
        x is >= Min and <= Max && y is >= Min and <= Max;

    /// <summary>The role a player standing at (<paramref name="x"/>, <paramref name="y"/>) fills.</summary>
    public static PlayerRole RoleAt(double x, double y)
    {
        if (y < KeeperDepth && x is >= KeeperLeft and <= KeeperRight)
            return PlayerRole.GK;

        if (y < DefenceDepth)
        {
            if (x >= FullBackLane && x <= Max - FullBackLane)
                return PlayerRole.CB;

            // Wide and deep is a full-back; wide and pushed on is a wing-back,
            // which is the same player given the whole flank.
            var left = x < FullBackLane;

            if (y < WingBackDepth)
                return left ? PlayerRole.LB : PlayerRole.RB;

            return left ? PlayerRole.LWB : PlayerRole.RWB;
        }

        if (y < MidfieldDepth)
        {
            if (x < WideMidfieldLane) return PlayerRole.LM;
            if (x > Max - WideMidfieldLane) return PlayerRole.RM;

            return y < HoldingDepth ? PlayerRole.CDM
                : y < CentralDepth ? PlayerRole.CM
                : PlayerRole.CAM;
        }

        return x < WingerLane ? PlayerRole.LW
            : x > Max - WingerLane ? PlayerRole.RW
            : PlayerRole.ST;
    }
}

/// <summary>
/// What a starting eleven must look like however freely it is arranged: one
/// keeper, a back line of at least three, and no flank doubled up.
/// </summary>
public static class LineupRules
{
    public const int MinimumDefenders = 3;

    /// <summary>Roles that hold one flank, so a side has room for one of each.</summary>
    private static readonly PlayerRole[] OnePerSide =
    [
        PlayerRole.LB, PlayerRole.RB,
        PlayerRole.LWB, PlayerRole.RWB,
        PlayerRole.LW, PlayerRole.RW
    ];

    /// <summary>Why these roles cannot take the field together, or null if they can.</summary>
    public static string? Problem(IReadOnlyCollection<PlayerRole> roles)
    {
        var keepers = roles.Count(r => r == PlayerRole.GK);
        if (keepers != 1)
            return $"A line-up needs exactly one goalkeeper, but {keepers} stand in goal.";

        var defenders = roles.Count(r => RoleFitness.LineOf(r) == Position.DEF);
        if (defenders < MinimumDefenders)
            return $"A line-up needs at least {MinimumDefenders} defenders, but only {defenders} are at the back.";

        foreach (var role in OnePerSide)
        {
            if (roles.Count(r => r == role) > 1)
                return $"Only one player can play {role}.";
        }

        return null;
    }
}
