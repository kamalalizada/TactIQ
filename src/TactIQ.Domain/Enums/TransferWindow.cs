namespace TactIQ.Domain.Enums;

/// <summary>
/// When clubs may buy and sell. A season opens the market before a ball is
/// kicked and again at the halfway point; the rest of the time a manager lives
/// with the squad they have, which is what makes the two windows matter.
/// </summary>
public enum TransferWindow
{
    Closed = 0,
    Summer = 1,
    Winter = 2
}
