namespace TactIQ.Domain.Enums;

public enum CompetitionKind
{
    /// <summary>A double round-robin domestic league.</summary>
    League = 0,

    /// <summary>Group stage followed by two-legged knockout rounds.</summary>
    ChampionsLeague = 1,

    /// <summary>Straight knockout played after the domestic season.</summary>
    DomesticCup = 2
}
