namespace TactIQ.Domain.Enums;

/// <summary>
/// Where a player actually plays, as a football game records it. The broader
/// <see cref="Position"/> still decides which line of a formation they fill;
/// this says whereabouts in that line they belong, so a left-back played on
/// the right is a worse left-back rather than simply a defender.
/// </summary>
public enum PlayerRole
{
    GK = 0,

    // Defence, left to right.
    LB = 1,
    CB = 2,
    RB = 3,

    // Midfield, deepest to most advanced.
    CDM = 4,
    CM = 5,
    CAM = 6,
    LM = 7,
    RM = 8,

    // Attack.
    LW = 9,
    ST = 10,
    RW = 11,

    // Wing-backs, who hold a flank on their own: a full-back's defending with
    // a winger's ground to cover. Numbered after the rest so that the roles
    // already stored against players keep their meaning.
    LWB = 12,
    RWB = 13
}
