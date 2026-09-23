namespace TactIQ.Application.Simulation;

public static class EngineConstants
{
    public const double HomeAdvantage = 1.07;

    /// <summary>How far a side's rating drifts on the day. Kept well below the gap
    /// between good and poor squads, or results stop tracking quality.</summary>
    public const double FormSwing = 4.0;
    public const int RegularTimeMinutes = 90;

    public const double DefensiveAttackPenalty = 0.88;
    public const double DefensiveDefenseBonus = 1.12;
    public const double AttackingAttackBonus = 1.12;
    public const double AttackingDefensePenalty = 0.88;

    /// <summary>Attacking moves a team starts per match when evenly matched.
    /// Tuned so league scoring lands near two and a half goals a game.</summary>
    public const double BaseAttacksPerMatch = 32.0;

    /// <summary>Share of attacking moves that become a shot at even strength.</summary>
    public const double BaseChanceConversion = 0.42;

    /// <summary>Stamina points a player burns per minute on the pitch.</summary>
    public const double StaminaPerMinute = 0.55;

    /// <summary>Condition recovered between matches. Deliberately short of what a
    /// full ninety minutes costs, so an unrotated eleven wears down over a season
    /// instead of turning up fresh every week.</summary>
    public const int ConditionRecoveryPerWeek = 32;

    /// <summary>Attacking moves broken up by a foul. Most cost nothing worse than
    /// a free kick; the rates below decide how many turn into cards.</summary>
    public const double FoulChancePerAttack = 0.22;

    public const double YellowFromFoul = 0.45;
    public const double StraightRedFromFoul = 0.002;

    /// <summary>Odds a booked player commits the foul that gets him sent off.</summary>
    public const double SecondYellowChance = 0.08;
    public const double InjuryChancePerAttack = 0.02;

    /// <summary>Yellow cards in a season before a one-match ban.</summary>
    public const int YellowCardsForBan = 5;

    public const int MatchesBannedForRedCard = 1;
}
