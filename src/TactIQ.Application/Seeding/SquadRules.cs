namespace TactIQ.Application.Seeding;

/// <summary>Squad shape and in-match limits shared by seeding and the live match engine.</summary>
public static class SquadRules
{
    public const int StarterCount = 11;
    public const int BenchCount = 8;
    public const int SquadSize = StarterCount + BenchCount;

    /// <summary>Substitutions a team may make in a single match.</summary>
    public const int MaxSubstitutions = 5;
}
