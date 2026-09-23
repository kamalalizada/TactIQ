using TactIQ.Domain.Enums;

namespace TactIQ.Domain.Entities;

public class League
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public string SeasonName { get; set; } = string.Empty;
    public int TeamCount { get; set; } = 8;
    public int CurrentWeek { get; set; } = 1;

    public GameMode GameMode { get; set; }
    public Guid? UserTeamId { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Team> Teams { get; set; } = [];
    public List<Match> Matches { get; set; } = [];

    /// <summary>The cup starts once the league season is over.</summary>
    public CupRound? CurrentCupRound { get; set; }

    public Guid? CupWinnerTeamId { get; set; }

    public int TotalWeeks => 2 * (TeamCount - 1);

    /// <summary>True once every league week has been played. The cup may still be running.</summary>
    public bool IsFinished => CurrentWeek > TotalWeeks;

    public bool CupStarted => CurrentCupRound is not null || CupWinnerTeamId is not null;
    public bool CupFinished => CupWinnerTeamId is not null;
}
