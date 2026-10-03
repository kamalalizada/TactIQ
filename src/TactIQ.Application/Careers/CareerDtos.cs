namespace TactIQ.Application.Careers;

/// <summary>How one league finished in one year of a career.</summary>
public record SeasonRecordDto(
    int Year,
    string CompetitionName,
    string? Country,
    Guid? ChampionTeamId,
    string? ChampionTeamName,
    /// <summary>Where the managed club finished, if it played in this league.</summary>
    int? UserRank,
    int? UserPoints,
    decimal? UserBudget,
    /// <summary>True when the manager's own club won it.</summary>
    bool UserWon);

/// <summary>A player whose career ended, and the youngster who took his place.</summary>
public record RetirementDto(string Name, int Age, int Overall, string TeamName, string? ReplacedBy);

/// <summary>A player whose rating moved over the summer.</summary>
public record DevelopmentDto(string Name, int Age, int Before, int After, string TeamName);

/// <summary>What a year did, shown to the manager when the next one begins.</summary>
public record SeasonRolloverDto(
    /// <summary>The year just closed.</summary>
    int Year,
    /// <summary>The year now beginning.</summary>
    int NextYear,
    IReadOnlyList<SeasonRecordDto> Records,
    IReadOnlyList<RetirementDto> Retired,
    /// <summary>The biggest risers at the managed club, best first.</summary>
    IReadOnlyList<DevelopmentDto> Improved,
    /// <summary>The steepest decliners at the managed club.</summary>
    IReadOnlyList<DevelopmentDto> Declined);

/// <summary>A manager's record across every season he has played.</summary>
public record CareerDto(
    int CurrentYear,
    /// <summary>True once every competition is finished and the year can be closed.</summary>
    bool CanAdvance,
    string? UserTeamName,
    int TitlesWon,
    IReadOnlyList<SeasonRecordDto> History);
