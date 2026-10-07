using TactIQ.Domain.Enums;

namespace TactIQ.Application.Simulation;

/// <summary>
/// How well a player copes in a role that is not their own.
///
/// Football games express this as a penalty on the rating: a player in their
/// own role is at full strength, one a step away loses a little, and one asked
/// to do an unrelated job loses a lot. A right-back at left-back is close to
/// himself; a striker at centre-back is not.
/// </summary>
public static class RoleFitness
{
    /// <summary>Full strength, for a player in their own role.</summary>
    public const double Native = 1.00;

    /// <summary>A neighbouring role: the same job, shifted across or along.</summary>
    private const double Adjacent = 0.94;

    /// <summary>
    /// A wide player sent to the other flank: still the same job, but on the
    /// wrong foot, cutting in where they would go outside.
    /// </summary>
    private const double OtherFlank = 0.90;

    /// <summary>The same line, but a different kind of job within it.</summary>
    private const double SameLine = 0.87;

    /// <summary>The next line over: asked to defend or attack more than usual.</summary>
    private const double NearbyLine = 0.78;

    /// <summary>Anything else: defence asked to attack, or the reverse.</summary>
    private const double Foreign = 0.62;

    /// <summary>
    /// Goalkeeping, which is a different sport. Nobody deputises well in either
    /// direction, so an outfield player in goal and a keeper up front both lose
    /// more than any move between outfield lines costs.
    /// </summary>
    private const double Goalkeeping = 0.45;

    /// <summary>Roles that read as the same job, so they cost almost nothing.</summary>
    private static readonly Dictionary<PlayerRole, PlayerRole[]> Neighbours = new()
    {
        [PlayerRole.GK] = [],
        [PlayerRole.LB] = [PlayerRole.RB, PlayerRole.LM],
        [PlayerRole.CB] = [PlayerRole.CDM],
        [PlayerRole.RB] = [PlayerRole.LB, PlayerRole.RM],
        [PlayerRole.CDM] = [PlayerRole.CM, PlayerRole.CB],
        [PlayerRole.CM] = [PlayerRole.CDM, PlayerRole.CAM],
        [PlayerRole.CAM] = [PlayerRole.CM, PlayerRole.ST],
        [PlayerRole.LM] = [PlayerRole.LW, PlayerRole.LB],
        [PlayerRole.RM] = [PlayerRole.RW, PlayerRole.RB],
        [PlayerRole.LW] = [PlayerRole.LM, PlayerRole.RW],
        [PlayerRole.RW] = [PlayerRole.RM, PlayerRole.LW],
        [PlayerRole.ST] = [PlayerRole.CAM],
    };

    /// <summary>The line of the pitch a role belongs to.</summary>
    public static Position LineOf(PlayerRole role) => role switch
    {
        PlayerRole.GK => Position.GK,
        PlayerRole.LB or PlayerRole.CB or PlayerRole.RB => Position.DEF,
        PlayerRole.CDM or PlayerRole.CM or PlayerRole.CAM or PlayerRole.LM or PlayerRole.RM =>
            Position.MID,
        _ => Position.FWD
    };

    private enum Flank { Left, Right }

    /// <summary>Which flank a wide attacking role works, or null for anyone else.</summary>
    private static Flank? FlankOf(PlayerRole role) => role switch
    {
        PlayerRole.LW or PlayerRole.LM => Flank.Left,
        PlayerRole.RW or PlayerRole.RM => Flank.Right,
        _ => null
    };

    /// <summary>
    /// The share of a player's ability that survives being played in
    /// <paramref name="playedAs"/> when their own role is <paramref name="natural"/>.
    /// </summary>
    public static double For(PlayerRole natural, PlayerRole playedAs)
    {
        if (natural == playedAs)
            return Native;

        // Winger and wide midfielder are one job on a given flank: a left
        // winger dropped into left midfield, or the reverse, is still at home.
        var naturalFlank = FlankOf(natural);
        var playedFlank = FlankOf(playedAs);

        if (naturalFlank is not null && playedFlank is not null)
            return naturalFlank == playedFlank ? Native : OtherFlank;

        if (natural == PlayerRole.GK || playedAs == PlayerRole.GK)
            return Goalkeeping;

        if (Neighbours[natural].Contains(playedAs))
            return Adjacent;

        var naturalLine = LineOf(natural);
        var playedLine = LineOf(playedAs);

        if (naturalLine == playedLine)
            return SameLine;

        // Defence to attack, or the reverse, is two lines away.
        var distance = Math.Abs((int)naturalLine - (int)playedLine);
        return distance == 1 ? NearbyLine : Foreign;
    }
}
